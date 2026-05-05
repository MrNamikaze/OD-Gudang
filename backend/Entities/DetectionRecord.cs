namespace backend.Entities;

public sealed class DetectionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string CameraId { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Zone { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
