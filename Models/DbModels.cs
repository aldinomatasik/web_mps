namespace WebMPS.Models;

public class UploadRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int TotalSlips { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.Now;
    public string UploadedBy { get; set; } = "Operator";
    public string Status { get; set; } = "Success";

    public double FileSizeMb => Math.Round(FileSizeBytes / (1024.0 * 1024.0), 2);
}

public class ApprovalLog
{
    public int Id { get; set; }
    public string SlipId { get; set; } = string.Empty;
    public string SlipNumber { get; set; } = string.Empty;
    public string Action { get; set; } = "Approved"; // Approved, Batch Approved, Reset
    public string ApproverName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
