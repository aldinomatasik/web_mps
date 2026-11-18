using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using WebMPS.Core.Interfaces.Services;
using WebMPS.Models;

namespace WebMPS.Services;

public class MpsParserService : IMpsParserService
{
    private readonly ILogger<MpsParserService> _logger;

    public MpsParserService(ILogger<MpsParserService> logger)
    {
        _logger = logger;
    }

    public List<MpsSlip> ParsePdf(Stream pdfStream, string filename)
    {
        var slips = new List<MpsSlip>();

        using var document = PdfDocument.Open(pdfStream);
        int totalPages = document.NumberOfPages;

        for (int p = 1; p <= totalPages; p++)
        {
            var page = document.GetPage(p);
            double midX = page.Width / 2.0;
            double midY = page.Height / 2.0;

            // Quadrant 1: Top-Left (x < midX, y >= midY)
            var q1Words = page.GetWords().Where(w => w.BoundingBox.Left < midX && w.BoundingBox.Bottom >= midY).ToList();
            var slip1 = ParseQuadrant(q1Words, p, 1, filename);
            if (slip1 != null) slips.Add(slip1);

            // Quadrant 2: Top-Right (x >= midX, y >= midY)
            var q2Words = page.GetWords().Where(w => w.BoundingBox.Left >= midX && w.BoundingBox.Bottom >= midY).ToList();
            var slip2 = ParseQuadrant(q2Words, p, 2, filename);
            if (slip2 != null) slips.Add(slip2);

            // Quadrant 3: Bottom-Left (x < midX, y < midY)
            var q3Words = page.GetWords().Where(w => w.BoundingBox.Left < midX && w.BoundingBox.Bottom < midY).ToList();
            var slip3 = ParseQuadrant(q3Words, p, 3, filename);
            if (slip3 != null) slips.Add(slip3);

            // Quadrant 4: Bottom-Right (x >= midX, y < midY)
            var q4Words = page.GetWords().Where(w => w.BoundingBox.Left >= midX && w.BoundingBox.Bottom < midY).ToList();
            var slip4 = ParseQuadrant(q4Words, p, 4, filename);
            if (slip4 != null) slips.Add(slip4);
        }

        return slips;
    }

    private MpsSlip? ParseQuadrant(List<Word> words, int pageNumber, int quadrant, string filename)
    {
        if (words.Count == 0) return null;

        var lines = GroupWordsIntoLines(words);
        if (lines.Count < 3) return null;

        var fullText = string.Join("\n", lines.Select(l => l.Text));
        if (!fullText.Contains("MATERIAL PICKING SLIP") && !fullText.Contains("SLIP NUMBER"))
        {
            return null;
        }

        var slip = new MpsSlip
        {
            OriginalPage = pageNumber,
            Quadrant = quadrant,
            SourceFile = filename
        };

        // Parse Tag e.g. 1A, 1B, 2
        var tagMatch = Regex.Match(fullText, @"(?:MATERIAL\s+PICKING\s+SLIP[^\n\r]*?)\s+([0-9]+[A-Za-z]?|[A-Za-z]+[0-9]?)");
        if (!tagMatch.Success)
        {
            tagMatch = Regex.Match(fullText, @"\b(1A|1B|2)\b", RegexOptions.IgnoreCase);
        }
        if (tagMatch.Success)
        {
            slip.Tag = tagMatch.Groups[1].Value.Trim().ToUpperInvariant();
        }

        // Parse Planner
        var plannerMatch = Regex.Match(fullText, @"PLANNER:\s*([^\n\r]+)");
        if (plannerMatch.Success)
        {
            slip.Planner = plannerMatch.Groups[1].Value.Trim();
        }

        // Parse Slip Number
        var slipMatch = Regex.Match(fullText, @"SLIP\s*NUMBER:\s*(\d+)");
        if (slipMatch.Success)
        {
            slip.SlipNumber = slipMatch.Groups[1].Value.Trim();
        }
        else
        {
            var barcodeMatch = Regex.Match(fullText, @"\*(\d+)\*");
            if (barcodeMatch.Success)
            {
                slip.SlipNumber = barcodeMatch.Groups[1].Value.Trim();
            }
        }

        // Parse Supply Date
        var supplyMatch = Regex.Match(fullText, @"SUPPLY\s*DATE:\s*([0-9\/\.\-]+)");
        if (supplyMatch.Success)
        {
            slip.SupplyDate = supplyMatch.Groups[1].Value.Trim();
        }

        // Parse Req Date
        var reqDateMatch = Regex.Match(fullText, @"REQ\.?\s*DATE:\s*([0-9\/\.\-]+)");
        if (reqDateMatch.Success)
        {
            slip.ReqDate = reqDateMatch.Groups[1].Value.Trim();
        }

        // Parse FROM
        var fromMatch = Regex.Match(fullText, @"FROM:\s*([^\n\r]+)");
        if (fromMatch.Success)
        {
            slip.FromLocation = fromMatch.Groups[1].Value.Trim();
        }

        // Parse TO
        var toMatch = Regex.Match(fullText, @"TO:\s*([^\n\r]+)");
        if (toMatch.Success)
        {
            slip.ToLocation = toMatch.Groups[1].Value.Trim();
        }

        // Parse Part and Order details
        ParsePartAndOrders(lines, slip);

        return slip;
    }

    private void ParsePartAndOrders(List<LineInfo> lines, MpsSlip slip)
    {
        int partHeaderIdx = -1;
        int orderHeaderIdx = -1;
        int sigHeaderIdx = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var text = lines[i].Text;
            if (text.Contains("PART CODE") || text.Contains("DESCRIPTION"))
            {
                partHeaderIdx = i;
            }
            else if (text.Contains("ORDER") && (text.Contains("PEGGED") || text.Contains("MATERIAL")))
            {
                orderHeaderIdx = i;
            }
            else if (text.Contains("DELIVERY SIGNATURE") || text.Contains("RECEIVER SIGNATURE"))
            {
                sigHeaderIdx = i;
            }
        }

        // Extract Part section (between partHeaderIdx and orderHeaderIdx)
        if (partHeaderIdx != -1)
        {
            int endPart = orderHeaderIdx != -1 ? orderHeaderIdx : lines.Count;
            var partLines = lines.Skip(partHeaderIdx + 1).Take(endPart - partHeaderIdx - 1).ToList();

            foreach (var pl in partLines)
            {
                var lineText = pl.Text.Trim();
                if (lineText.Contains("VMI") && lineText.Contains("VENDOR"))
                {
                    continue; // header line
                }

                // Check for VMI marker 'X'
                if (lineText.StartsWith("X ") || lineText.StartsWith("X\t") || lineText == "X")
                {
                    slip.IsVmi = true;
                }

                // Check for vendor code e.g. 21005551-A
                var vendorMatch = Regex.Match(lineText, @"([0-9]{8}-[A-Z])\s*(.*)");
                if (vendorMatch.Success)
                {
                    slip.VendorCode = vendorMatch.Groups[1].Value.Trim();
                    slip.VendorName = vendorMatch.Groups[2].Value.Trim();
                    continue;
                }

                // Look for quantity and unit: e.g. 500.000 PC or 200.000 PC
                var qtyMatch = Regex.Match(lineText, @"([0-9]+(?:\.[0-9]+)?)\s+(PC|RL|SET|BAG|BOX|M|KG)", RegexOptions.IgnoreCase);
                if (qtyMatch.Success)
                {
                    if (decimal.TryParse(qtyMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var q))
                    {
                        slip.Qty = q;
                    }
                    slip.Unit = qtyMatch.Groups[2].Value.ToUpper().Trim();

                    var beforeQty = lineText.Substring(0, qtyMatch.Index).Trim();
                    var tokens = beforeQty.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (tokens.Length > 0)
                    {
                        // Check if previous line left a partial part code ending with '-'
                        if (!string.IsNullOrEmpty(slip.PartCode) && slip.PartCode.EndsWith("-"))
                        {
                            // First token belongs to PartCode (e.g. "1")
                            slip.PartCode += tokens[0].Trim();
                            if (tokens.Length > 1)
                            {
                                slip.PartDescription = string.Join(" ", tokens.Skip(1)).Trim();
                            }
                        }
                        else if (string.IsNullOrEmpty(slip.PartCode))
                        {
                            slip.PartCode = tokens[0].Trim();
                            if (tokens.Length > 1)
                            {
                                slip.PartDescription = string.Join(" ", tokens.Skip(1)).Trim();
                            }
                        }
                        else
                        {
                            slip.PartDescription = beforeQty;
                        }
                    }
                }
                else
                {
                    // Line without Qty: might be partial PartCode e.g. "ACXD54-04960-" or single token "1"
                    var tokens = lineText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length > 0)
                    {
                        if (string.IsNullOrEmpty(slip.PartCode))
                        {
                            slip.PartCode = tokens[0].Trim();
                            if (tokens.Length > 1)
                            {
                                slip.PartDescription = string.Join(" ", tokens.Skip(1)).Trim();
                            }
                        }
                        else if (slip.PartCode.EndsWith("-"))
                        {
                            // Append continuation e.g. "1" or "A"
                            slip.PartCode += tokens[0].Trim();
                            if (tokens.Length > 1)
                            {
                                var desc = string.Join(" ", tokens.Skip(1)).Trim();
                                slip.PartDescription = string.IsNullOrEmpty(slip.PartDescription) ? desc : slip.PartDescription + " " + desc;
                            }
                        }
                        else
                        {
                            slip.PartDescription = string.IsNullOrEmpty(slip.PartDescription) ? lineText : slip.PartDescription + " " + lineText;
                        }
                    }
                }
            }
        }

        // Clean up PartDescription if it starts with hyphen continuation artifact
        if (!string.IsNullOrEmpty(slip.PartDescription) && slip.PartDescription.StartsWith("-"))
        {
            slip.PartDescription = slip.PartDescription.TrimStart('-', ' ').Trim();
        }

        // Extract Pegged Orders (between orderHeaderIdx and sigHeaderIdx)
        if (orderHeaderIdx != -1)
        {
            int endOrder = sigHeaderIdx != -1 ? sigHeaderIdx : lines.Count;
            var orderLines = lines.Skip(orderHeaderIdx + 1).Take(endOrder - orderHeaderIdx - 1).ToList();

            foreach (var ol in orderLines)
            {
                var text = ol.Text.Trim();
                if (string.IsNullOrEmpty(text)) continue;

                var orderMatch = Regex.Match(text, @"^(\d{8,12})\s+([A-Za-z0-9\-]+)\s+([0-9]+(?:\.[0-9]+)?)\s+([A-Za-z]+)");
                if (orderMatch.Success)
                {
                    var po = new MpsPeggedOrder
                    {
                        OrderNumber = orderMatch.Groups[1].Value,
                        PeggedMaterial = orderMatch.Groups[2].Value,
                        Unit = orderMatch.Groups[4].Value
                    };
                    if (decimal.TryParse(orderMatch.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var oq))
                    {
                        po.ReqQty = oq;
                    }
                    slip.PeggedOrders.Add(po);
                }
            }
        }
    }

    private List<LineInfo> GroupWordsIntoLines(List<Word> words)
    {
        var orderedWords = words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();

        var lines = new List<LineInfo>();
        foreach (var word in orderedWords)
        {
            var existingLine = lines.FirstOrDefault(l => Math.Abs(l.Y - word.BoundingBox.Bottom) < 4.0);
            if (existingLine == null)
            {
                var newLine = new LineInfo { Y = word.BoundingBox.Bottom };
                newLine.Words.Add(word);
                lines.Add(newLine);
            }
            else
            {
                existingLine.Words.Add(word);
            }
        }

        foreach (var line in lines)
        {
            line.Words = line.Words.OrderBy(w => w.BoundingBox.Left).ToList();
            line.Text = string.Join(" ", line.Words.Select(w => w.Text));
        }

        return lines.OrderByDescending(l => l.Y).ToList();
    }

    private class LineInfo
    {
        public double Y { get; set; }
        public List<Word> Words { get; set; } = new();
        public string Text { get; set; } = "";
    }
}
