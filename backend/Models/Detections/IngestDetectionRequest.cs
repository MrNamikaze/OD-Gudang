using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace backend.Models.Detections;

public sealed class IngestDetectionRequest
{
    [Required]
    public DateTime TimestampUtc { get; set; }

    [Required]
    [StringLength(120)]
    public string ObjectName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string CameraId { get; set; } = string.Empty;

    [Range(0, 1)]
    public decimal Confidence { get; set; }

    [StringLength(120)]
    public string Zone { get; set; } = string.Empty;

    [Required]
    public IFormFile Screenshot { get; set; } = default!;
}
