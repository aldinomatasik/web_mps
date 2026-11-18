namespace WebMPS.Models;

public static class ApprovalStatusConstants
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class ApprovalRoleConstants
{
    public const string Receiver = "Receiver";
    public const string Delivery = "Delivery";
    public const string Admin = "Admin";
}

public class MpsPeggedOrder
{
    public string OrderNumber { get; set; } = string.Empty;
    public string PeggedMaterial { get; set; } = string.Empty;
    public decimal ReqQty { get; set; }
    public string Unit { get; set; } = "PC";
}

public class MpsSlip
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UploadId { get; set; } = string.Empty;
    public int OriginalPage { get; set; }
    public int Quadrant { get; set; } // 1: TL, 2: TR, 3: BL, 4: BR

    public string QuadrantLabel => Quadrant switch
    {
        1 => "Top-Left",
        2 => "Top-Right",
        3 => "Bottom-Left",
        4 => "Bottom-Right",
        _ => "Unknown"
    };

    public string Title { get; set; } = "MATERIAL PICKING SLIP - 20PC (PMI AC (MFG))";
    public string Tag { get; set; } = "1A";
    public string Planner { get; set; } = string.Empty;
    public string SlipNumber { get; set; } = string.Empty;
    public string SupplyDate { get; set; } = string.Empty;
    public string ReqDate { get; set; } = string.Empty;
    public string FromLocation { get; set; } = "PT01 (RAW MATERIAL)";
    public string ToLocation { get; set; } = "H100 (CS LINE)";

    public string PartCode { get; set; } = string.Empty;
    public string PartDescription { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string Unit { get; set; } = "PC";

    public bool IsVmi { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;

    public List<MpsPeggedOrder> PeggedOrders { get; set; } = new();

    public string Barcode => !string.IsNullOrEmpty(SlipNumber) ? SlipNumber : string.Empty;

    // Approval tracking
    public string ApprovalStatus { get; set; } = ApprovalStatusConstants.Pending;
    public string? DeliverySignature { get; set; }
    public string? ReceiverSignature { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? Notes { get; set; }
    public string SourceFile { get; set; } = string.Empty;

    // Scan checklist tracking (Serah Terima CS Line)
    public bool IsScanned { get; set; }
    public DateTime? ScannedAt { get; set; }
    public string? ScannedBy { get; set; }

    public bool IsApproved => ApprovalStatus.Equals(ApprovalStatusConstants.Approved, StringComparison.OrdinalIgnoreCase);
}

public class SlipSummaryStats
{
    public int TotalCount { get; set; }
    public int ApprovedCount { get; set; }
    public int WaitingHandoverCount { get; set; }
    public int PendingCount => Math.Max(0, TotalCount - ApprovedCount);
}
