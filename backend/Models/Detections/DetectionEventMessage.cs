namespace backend.Models.Detections;

public sealed class DetectionEventMessage
{
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string CameraId { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Zone { get; set; } = string.Empty;
    public string ImageFileName { get; set; } = string.Empty;
    public string ImageBase64 { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
}
