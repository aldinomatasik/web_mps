using System.Globalization;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Models;

namespace WebMPS.Services;

public class MpsPdfSplitterService : IMpsPdfSplitterService
{
    private readonly ILogger<MpsPdfSplitterService> _logger;

    private const double PageWidthPt = 420.0;
    private const double MarginX = 22.0;
    private const double ContentWidth = PageWidthPt - (MarginX * 2); // 376.0 pt

    public MpsPdfSplitterService(ILogger<MpsPdfSplitterService> logger)
    {
        _logger = logger;
    }

    public byte[] CreateSplitPdf(Stream sourcePdfStream, IReadOnlyList<MpsSlip>? slips = null)
    {
        using var outputDoc = new PdfDocument();

        if (slips != null && slips.Count > 0)
        {
            foreach (var slip in slips)
            {
                RenderCleanSlipPage(outputDoc, slip);
            }
        }
        else
        {
            using var inputDoc = PdfReader.Open(sourcePdfStream, PdfDocumentOpenMode.Import);
            for (int p = 0; p < inputDoc.PageCount; p++)
            {
                var origPage = inputDoc.Pages[p];
                for (int q = 1; q <= 4; q++)
                {
                    AddCroppedQuadrantPage(outputDoc, origPage, q);
                }
            }
        }

        using var ms = new MemoryStream();
        outputDoc.Save(ms);
        return ms.ToArray();
    }

    public byte[] CreateSingleSlipPdf(Stream sourcePdfStream, int pageNumber1Based, int quadrant1To4, MpsSlip? slip = null)
    {
        using var outputDoc = new PdfDocument();

        if (slip != null)
        {
            RenderCleanSlipPage(outputDoc, slip);
        }
        else
        {
            using var inputDoc = PdfReader.Open(sourcePdfStream, PdfDocumentOpenMode.Import);
            if (pageNumber1Based < 1 || pageNumber1Based > inputDoc.PageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber1Based));
            }
            AddCroppedQuadrantPage(outputDoc, inputDoc.Pages[pageNumber1Based - 1], quadrant1To4);
        }

        using var ms = new MemoryStream();
        outputDoc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Renders an individual slip cleanly using the exact balanced structure and spacing of the web View layout.
    /// </summary>
    private static void RenderCleanSlipPage(PdfDocument targetDoc, MpsSlip slip)
    {
        bool hasVendorRow = slip.IsVmi || !string.IsNullOrWhiteSpace(slip.VendorCode) || !string.IsNullOrWhiteSpace(slip.VendorName);
        int orderRows = slip.PeggedOrders.Count;

        // Calculate exact compact page height so there is zero wasted whitespace
        double totalHeight = 20.0      // top padding
                           + 26.0      // header row + 1A box
                           + 60.0      // 4 meta rows + gap
                           + 37.0      // part header + subheader + part row
                           + (hasVendorRow ? 12.0 : 0.0)
                           + 10.0      // gap
                           + 13.0      // order header
                           + (Math.Max(1, orderRows) * 12.0)
                           + 14.0      // gap
                           + 50.0      // signature boxes
                           + 12.0      // gap
                           + 38.0      // barcode + slip number
                           + 18.0;     // bottom padding

        var page = targetDoc.AddPage();
        page.Width = XUnit.FromPoint(PageWidthPt);
        page.Height = XUnit.FromPoint(Math.Max(300.0, totalHeight));

        using var gfx = XGraphics.FromPdfPage(page);
        var black = XBrushes.Black;
        var blue = new XSolidBrush(XColor.FromArgb(2, 132, 199));
        var slate = new XSolidBrush(XColor.FromArgb(71, 85, 105));
        var blackPen = new XPen(XColors.Black, 1.3);
        var tagPen = new XPen(XColors.Black, 1.6);

        var fontTitle = new XFont("Arial", 10.0, XFontStyle.Bold);
        var fontTag = new XFont("Arial", 11.0, XFontStyle.Bold);
        var fontMetaLabel = new XFont("Arial", 8.2, XFontStyle.Bold);
        var fontMetaVal = new XFont("Arial", 8.2, XFontStyle.Regular);
        var fontTableHead = new XFont("Arial", 8.0, XFontStyle.Bold);
        var fontTableSub = new XFont("Arial", 7.4, XFontStyle.Regular);
        var fontTableRow = new XFont("Arial", 8.0, XFontStyle.Regular);
        var fontBarcodeText = new XFont("Arial", 9.5, XFontStyle.Bold);

        double y = 20.0;
        double rightEdge = MarginX + ContentWidth; // 398.0

        // 1. Header Row: Title & [1A] Box
        gfx.DrawString(slip.Title, fontTitle, black, new XPoint(MarginX, y + 10));

        string tagText = string.IsNullOrWhiteSpace(slip.Tag) ? "1A" : slip.Tag.Trim();
        var tagRect = new XRect(rightEdge - 26.0, y - 2.0, 26.0, 18.0);
        gfx.DrawRectangle(tagPen, tagRect);
        gfx.DrawString(tagText, fontTag, black, tagRect, XStringFormats.Center);

        y += 28.0;

        // 2. Meta Info Grid (4 cleanly aligned rows matching the View layout)
        const double leftValX = MarginX + 70.0;  // 92.0
        const double rightLblX = 242.0;
        const double rightValX = rightLblX + 70.0; // 312.0

        DrawMetaPair(gfx, "PLANNER:", slip.Planner, MarginX, leftValX, y, fontMetaLabel, fontMetaVal, black);
        y += 13.0;

        DrawMetaPair(gfx, "SLIP NUMBER:", slip.SlipNumber, MarginX, leftValX, y, fontMetaLabel, fontMetaVal, black);
        DrawMetaPair(gfx, "SUPPLY DATE:", slip.SupplyDate, rightLblX, rightValX, y, fontMetaLabel, fontMetaVal, black);
        y += 13.0;

        DrawMetaPair(gfx, "FROM:", slip.FromLocation, MarginX, leftValX, y, fontMetaLabel, fontMetaVal, black);
        DrawMetaPair(gfx, "REQ. DATE:", slip.ReqDate, rightLblX, rightValX, y, fontMetaLabel, fontMetaVal, black);
        y += 13.0;

        DrawMetaPair(gfx, "TO:", slip.ToLocation, MarginX, leftValX, y, fontMetaLabel, fontMetaVal, black);
        y += 20.0;

        // 3. Part Material Table (Clean non-colliding columns)
        const double col1X = MarginX;         // 22.0 (Part Code / Order)
        const double col1MaxW = 90.0;
        const double col2X = MarginX + 94.0;  // 116.0 (Description / Pegged Material)
        const double col2MaxW = 174.0;
        const double col3RightX = 348.0;      // QTY / REQ right edge
        double col4RightX = rightEdge;        // 398.0 UNIT right edge

        gfx.DrawString("PART CODE", fontTableHead, black, new XPoint(col1X, y));
        gfx.DrawString("DESCRIPTION", fontTableHead, black, new XPoint(col2X, y));
        DrawRightAligned(gfx, "QTY", fontTableHead, black, col3RightX, y);
        DrawRightAligned(gfx, "UNIT", fontTableHead, black, col4RightX, y);
        y += 12.0;

        gfx.DrawString("VMI    VENDOR", fontTableSub, black, new XPoint(col1X, y));
        gfx.DrawString("VENDOR DESCRIPTION", fontTableSub, black, new XPoint(col2X, y));
        y += 13.0;

        DrawFittedString(gfx, slip.PartCode, fontTableRow, black, col1X, y, col1MaxW);
        DrawFittedString(gfx, slip.PartDescription, fontTableRow, black, col2X, y, col2MaxW);
        DrawRightAligned(gfx, slip.Qty.ToString("0.000", CultureInfo.InvariantCulture), fontTableRow, black, col3RightX, y);
        DrawRightAligned(gfx, slip.Unit, fontTableRow, black, col4RightX, y);
        y += 12.0;

        if (hasVendorRow)
        {
            if (slip.IsVmi)
            {
                gfx.DrawString("X", fontTableRow, black, new XPoint(col1X, y));
            }
            DrawFittedString(gfx, slip.VendorCode, fontTableRow, black, col1X + 20.0, y, col1MaxW - 20.0);
            DrawFittedString(gfx, slip.VendorName, fontTableRow, black, col2X, y, col2MaxW);
            y += 12.0;
        }

        y += 10.0;

        // 4. Pegged Orders Table
        gfx.DrawString("ORDER", fontTableHead, black, new XPoint(col1X, y));
        gfx.DrawString("PEGGED MATERIAL", fontTableHead, black, new XPoint(col2X, y));
        DrawRightAligned(gfx, "REQ", fontTableHead, black, col3RightX, y);
        DrawRightAligned(gfx, "UNIT", fontTableHead, black, col4RightX, y);
        y += 13.0;

        if (slip.PeggedOrders.Count == 0)
        {
            y += 12.0;
        }
        else
        {
            foreach (var order in slip.PeggedOrders)
            {
                DrawFittedString(gfx, order.OrderNumber, fontTableRow, black, col1X, y, col1MaxW);
                DrawFittedString(gfx, order.PeggedMaterial, fontTableRow, black, col2X, y, col2MaxW);
                DrawRightAligned(gfx, order.ReqQty.ToString("0.000", CultureInfo.InvariantCulture), fontTableRow, black, col3RightX, y);
                DrawRightAligned(gfx, order.Unit, fontTableRow, black, col4RightX, y);
                y += 12.0;
            }
        }

        y += 10.0;

        // 5. Dual Signature Boxes (DELIVERY SIGNATURE & RECEIVER SIGNATURE)
        string deliverySig = !string.IsNullOrWhiteSpace(slip.DeliverySignature)
            ? slip.DeliverySignature
            : (slip.IsScanned ? slip.Planner : string.Empty);

        string deliveryDate = (!string.IsNullOrWhiteSpace(deliverySig) || slip.IsScanned)
            ? (slip.ScannedAt ?? slip.ApprovedAt)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

        string receiverSig = slip.ReceiverSignature ?? string.Empty;
        string receiverDate = !string.IsNullOrWhiteSpace(receiverSig)
            ? slip.ApprovedAt?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

        const double boxW = 182.0;
        const double boxTitleH = 16.0;
        const double boxBodyH = 34.0;
        const double sigGap = 12.0;

        DrawSignatureBox(gfx, MarginX, y, boxW, boxTitleH, boxBodyH, "DELIVERY SIGNATURE", deliverySig, deliveryDate, blackPen, black, blue, slate);
        DrawSignatureBox(gfx, MarginX + boxW + sigGap, y, boxW, boxTitleH, boxBodyH, "RECEIVER SIGNATURE", receiverSig, receiverDate, blackPen, black, blue, slate);

        y += boxTitleH + boxBodyH + 12.0;

        // 6. Crisp Vector Code 39 Barcode + Slip Number
        DrawCode39Barcode(gfx, slip.Barcode, MarginX, y, 22.0);
        gfx.DrawString(slip.SlipNumber, fontBarcodeText, black, new XPoint(MarginX, y + 33.0));
    }

    private static void DrawMetaPair(
        XGraphics gfx,
        string label,
        string value,
        double labelX,
        double valueX,
        double baselineY,
        XFont labelFont,
        XFont valueFont,
        XBrush brush)
    {
        gfx.DrawString(label, labelFont, brush, new XPoint(labelX, baselineY));
        gfx.DrawString(value ?? string.Empty, valueFont, brush, new XPoint(valueX, baselineY));
    }

    private static void DrawRightAligned(XGraphics gfx, string text, XFont font, XBrush brush, double rightX, double baselineY)
    {
        if (string.IsNullOrEmpty(text)) return;
        double width = gfx.MeasureString(text, font).Width;
        gfx.DrawString(text, font, brush, new XPoint(rightX - width, baselineY));
    }

    private static void DrawFittedString(XGraphics gfx, string text, XFont font, XBrush brush, double x, double baselineY, double maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return;
        double w = gfx.MeasureString(text, font).Width;
        if (w <= maxWidth)
        {
            gfx.DrawString(text, font, brush, new XPoint(x, baselineY));
            return;
        }

        double scaledSize = font.Size * (maxWidth / w);
        if (scaledSize >= 6.4)
        {
            var smaller = new XFont(font.Name, scaledSize, font.Style);
            gfx.DrawString(text, smaller, brush, new XPoint(x, baselineY));
            return;
        }

        // Truncate cleanly with ellipsis if exceptionally long
        var minFont = new XFont(font.Name, 6.5, font.Style);
        string trimmed = text;
        while (trimmed.Length > 3 && gfx.MeasureString(trimmed + "...", minFont).Width > maxWidth)
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }
        gfx.DrawString(trimmed + "...", minFont, brush, new XPoint(x, baselineY));
    }

    private static void DrawSignatureBox(
        XGraphics gfx,
        double x,
        double y,
        double width,
        double titleH,
        double bodyH,
        string title,
        string name,
        string date,
        XPen borderPen,
        XBrush titleBrush,
        XBrush nameBrush,
        XBrush dateBrush)
    {
        double totalH = titleH + bodyH;
        double halfW = width / 2.0;

        // Outer box
        gfx.DrawRectangle(borderPen, new XRect(x, y, width, totalH));
        // Header bottom line
        gfx.DrawLine(borderPen, x, y + titleH, x + width, y + titleH);
        // Center vertical divider inside body
        gfx.DrawLine(borderPen, x + halfW, y + titleH, x + halfW, y + totalH);

        var titleFont = new XFont("Arial", 7.8, XFontStyle.Bold);
        gfx.DrawString(title, titleFont, titleBrush, new XRect(x, y, width, titleH), XStringFormats.Center);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var baseFont = new XFont("Arial", 8.0, XFontStyle.Bold);
            double measured = gfx.MeasureString(name, baseFont).Width;
            double targetWidth = halfW - 6.0;
            var nameFont = measured > targetWidth
                ? new XFont("Arial", Math.Max(4.2, 8.0 * (targetWidth / measured)), XFontStyle.Bold)
                : baseFont;

            gfx.DrawString(name, nameFont, nameBrush, new XRect(x + 2, y + titleH, halfW - 4, bodyH), XStringFormats.Center);
        }

        if (!string.IsNullOrWhiteSpace(date))
        {
            var dateFont = new XFont("Arial", 7.5, XFontStyle.Regular);
            gfx.DrawString(date, dateFont, dateBrush, new XRect(x + halfW + 2, y + titleH, halfW - 4, bodyH), XStringFormats.Center);
        }
    }

    private static void DrawCode39Barcode(XGraphics gfx, string code, double startX, double startY, double height)
    {
        if (string.IsNullOrWhiteSpace(code)) return;

        string encoded = "*" + code.Trim().ToUpperInvariant() + "*";
        const double narrow = 1.05;
        const double wide = 2.4;
        const double gap = 1.05;

        double x = startX;
        foreach (char c in encoded)
        {
            if (!BarcodeHelper.Code39Patterns.TryGetValue(c, out var pattern))
                continue;

            for (int i = 0; i < pattern.Length; i++)
            {
                char elem = pattern[i];
                bool isBar = (i % 2 == 0);
                double w = (elem == 'B' || elem == 'W') ? wide : narrow;

                if (isBar)
                {
                    gfx.DrawRectangle(XBrushes.Black, x, startY, w, height);
                }
                x += w;
            }
            x += gap;
        }
    }

    private static void AddCroppedQuadrantPage(PdfDocument targetDoc, PdfPage sourcePage, int quadrant1To4)
    {
        var newPage = targetDoc.AddPage(sourcePage);

        double x0 = sourcePage.MediaBox.X1;
        double y0 = sourcePage.MediaBox.Y1;
        double x1 = sourcePage.MediaBox.X2;
        double y1 = sourcePage.MediaBox.Y2;

        if (Math.Abs(x1 - x0) < 1 || Math.Abs(y1 - y0) < 1)
        {
            x0 = 0;
            y0 = 0;
            x1 = sourcePage.Width.Point;
            y1 = sourcePage.Height.Point;
        }

        double midX = (x0 + x1) / 2.0;
        double midY = (y0 + y1) / 2.0;
        int rotate = ((sourcePage.Rotate % 360) + 360) % 360;

        PdfRectangle rect = rotate switch
        {
            90 => quadrant1To4 switch
            {
                1 => new PdfRectangle(new XPoint(x0, y0), new XPoint(midX, midY)),
                2 => new PdfRectangle(new XPoint(x0, midY), new XPoint(midX, y1)),
                3 => new PdfRectangle(new XPoint(midX, y0), new XPoint(x1, midY)),
                4 => new PdfRectangle(new XPoint(midX, midY), new XPoint(x1, y1)),
                _ => new PdfRectangle(new XPoint(x0, y0), new XPoint(midX, midY))
            },
            180 => quadrant1To4 switch
            {
                1 => new PdfRectangle(new XPoint(midX, y0), new XPoint(x1, midY)),
                2 => new PdfRectangle(new XPoint(x0, y0), new XPoint(midX, midY)),
                3 => new PdfRectangle(new XPoint(midX, midY), new XPoint(x1, y1)),
                4 => new PdfRectangle(new XPoint(x0, midY), new XPoint(midX, y1)),
                _ => new PdfRectangle(new XPoint(midX, y0), new XPoint(x1, midY))
            },
            270 => quadrant1To4 switch
            {
                1 => new PdfRectangle(new XPoint(midX, midY), new XPoint(x1, y1)),
                2 => new PdfRectangle(new XPoint(midX, y0), new XPoint(x1, midY)),
                3 => new PdfRectangle(new XPoint(x0, midY), new XPoint(midX, y1)),
                4 => new PdfRectangle(new XPoint(x0, y0), new XPoint(midX, midY)),
                _ => new PdfRectangle(new XPoint(midX, midY), new XPoint(x1, y1))
            },
            _ => quadrant1To4 switch
            {
                1 => new PdfRectangle(new XPoint(x0, midY), new XPoint(midX, y1)),
                2 => new PdfRectangle(new XPoint(midX, midY), new XPoint(x1, y1)),
                3 => new PdfRectangle(new XPoint(x0, y0), new XPoint(midX, midY)),
                4 => new PdfRectangle(new XPoint(midX, y0), new XPoint(x1, midY)),
                _ => new PdfRectangle(new XPoint(x0, midY), new XPoint(midX, y1))
            }
        };

        newPage.MediaBox = rect;
        newPage.CropBox = rect;
        newPage.TrimBox = rect;
        newPage.BleedBox = rect;
        newPage.ArtBox = rect;
    }
}
