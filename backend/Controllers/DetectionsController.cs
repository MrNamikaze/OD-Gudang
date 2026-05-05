using backend.Configuration;
using backend.Models.Detections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text;

namespace backend.Controllers;

[ApiController]
[Route("api/detections")]
[Authorize(Policy = "AuthenticatedUser")]
public sealed class DetectionsController : ControllerBase
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly DetectionIngestOptions _ingestOptions;
    private readonly IWebHostEnvironment _environment;

    public DetectionsController(
        NpgsqlDataSource dataSource,
        IOptions<DetectionIngestOptions> ingestOptions,
        IWebHostEnvironment environment)
    {
        _dataSource = dataSource;
        _ingestOptions = ingestOptions.Value;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DetectionResponse>>> Get(
        [FromQuery] DateTime? startUtc,
        [FromQuery] DateTime? endUtc,
        [FromQuery] string? cameraId,
        [FromQuery] string? objectName)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        var sqlBuilder = new StringBuilder();
        sqlBuilder.AppendLine("SELECT id, timestamp_utc, object_name, camera_id, image_path, confidence, zone");
        sqlBuilder.AppendLine("FROM detection_records");
        sqlBuilder.AppendLine("WHERE 1 = 1");

        await using var command = new NpgsqlCommand { Connection = connection };

        if (startUtc.HasValue)
        {
            sqlBuilder.AppendLine("AND timestamp_utc >= @startUtc");
            command.Parameters.AddWithValue("startUtc", startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            sqlBuilder.AppendLine("AND timestamp_utc <= @endUtc");
            command.Parameters.AddWithValue("endUtc", endUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(cameraId))
        {
            sqlBuilder.AppendLine("AND camera_id = @cameraId");
            command.Parameters.AddWithValue("cameraId", cameraId);
        }

        if (!string.IsNullOrWhiteSpace(objectName))
        {
            sqlBuilder.AppendLine("AND object_name = @objectName");
            command.Parameters.AddWithValue("objectName", objectName);
        }

        sqlBuilder.AppendLine("ORDER BY timestamp_utc DESC");
        sqlBuilder.AppendLine("LIMIT 200;");

        command.CommandText = sqlBuilder.ToString();

        var results = new List<DetectionResponse>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new DetectionResponse
            {
                Id = reader.GetGuid(0),
                TimestampUtc = reader.GetDateTime(1),
                ObjectName = reader.GetString(2),
                CameraId = reader.GetString(3),
                ImagePath = reader.GetString(4),
                Confidence = reader.GetDecimal(5),
                Zone = reader.GetString(6)
            });
        }

        return Ok(results);
    }

    [HttpPost("ingest")]
    [AllowAnonymous]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<DetectionResponse>> Ingest([FromForm] IngestDetectionRequest request)
    {
        var providedApiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedApiKey) || providedApiKey != _ingestOptions.ApiKey)
        {
            return Unauthorized(new { message = "API key tidak valid." });
        }

        if (request.Screenshot is null || request.Screenshot.Length == 0)
        {
            return BadRequest(new { message = "File screenshot wajib dikirim." });
        }

        var uploadsRoot = Path.Combine(_environment.ContentRootPath, "App_Data", "Uploads", "detections");
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(request.Screenshot.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = System.IO.File.Create(absolutePath))
        {
            await request.Screenshot.CopyToAsync(stream);
        }

        var imagePath = $"/uploads/detections/{fileName}";
        var response = new DetectionResponse
        {
            Id = Guid.NewGuid(),
            TimestampUtc = request.TimestampUtc,
            ObjectName = request.ObjectName,
            CameraId = request.CameraId,
            ImagePath = imagePath,
            Confidence = request.Confidence,
            Zone = request.Zone
        };

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO detection_records (
                id, timestamp_utc, object_name, camera_id, image_path, confidence, zone, created_at_utc
            ) VALUES (
                @id, @timestampUtc, @objectName, @cameraId, @imagePath, @confidence, @zone, @createdAtUtc
            );
            """, connection);

        command.Parameters.AddWithValue("id", response.Id);
        command.Parameters.AddWithValue("timestampUtc", response.TimestampUtc);
        command.Parameters.AddWithValue("objectName", response.ObjectName);
        command.Parameters.AddWithValue("cameraId", response.CameraId);
        command.Parameters.AddWithValue("imagePath", response.ImagePath);
        command.Parameters.AddWithValue("confidence", response.Confidence);
        command.Parameters.AddWithValue("zone", response.Zone);
        command.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<DetectionResponse>> Create([FromBody] CreateDetectionRequest request)
    {
        var response = new DetectionResponse
        {
            Id = Guid.NewGuid(),
            TimestampUtc = request.TimestampUtc,
            ObjectName = request.ObjectName,
            CameraId = request.CameraId,
            ImagePath = request.ImagePath,
            Confidence = request.Confidence,
            Zone = request.Zone
        };

        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO detection_records (
                id, timestamp_utc, object_name, camera_id, image_path, confidence, zone, created_at_utc
            ) VALUES (
                @id, @timestampUtc, @objectName, @cameraId, @imagePath, @confidence, @zone, @createdAtUtc
            );
            """, connection);

        command.Parameters.AddWithValue("id", response.Id);
        command.Parameters.AddWithValue("timestampUtc", response.TimestampUtc);
        command.Parameters.AddWithValue("objectName", response.ObjectName);
        command.Parameters.AddWithValue("cameraId", response.CameraId);
        command.Parameters.AddWithValue("imagePath", response.ImagePath);
        command.Parameters.AddWithValue("confidence", response.Confidence);
        command.Parameters.AddWithValue("zone", response.Zone);
        command.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync();

        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }
}
