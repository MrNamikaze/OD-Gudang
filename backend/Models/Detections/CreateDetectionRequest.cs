using System.ComponentModel.DataAnnotations;

namespace backend.Models.Detections;

public sealed class CreateDetectionRequest
{
    [Required]
    public DateTime TimestampUtc { get; set; }

    [Required]
    [StringLength(120)]
    public string ObjectName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string CameraId { get; set; } = string.Empty;

    [StringLength(500)]
    public string ImagePath { get; set; } = string.Empty;

    [Range(0, 1)]
    public decimal Confidence { get; set; }

    [StringLength(120)]
    public string Zone { get; set; } = string.Empty;
}
