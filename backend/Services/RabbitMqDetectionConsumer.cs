using System.Text;
using System.Text.Json;
using backend.Configuration;
using backend.Models.Detections;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace backend.Services;

public sealed class RabbitMqDetectionConsumer : BackgroundService
{
    private readonly ILogger<RabbitMqDetectionConsumer> _logger;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IWebHostEnvironment _environment;
    private readonly RabbitMqOptions _options;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqDetectionConsumer(
        ILogger<RabbitMqDetectionConsumer> logger,
        NpgsqlDataSource dataSource,
        IWebHostEnvironment environment,
        IOptions<RabbitMqOptions> options)
    {
        _logger = logger;
        _dataSource = dataSource;
        _environment = environment;
        _options = options.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        _channel.BasicQos(0, 1, false);

        _logger.LogInformation("RabbitMQ consumer connected to queue {QueueName}", _options.QueueName);
        return base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel is null)
        {
            throw new InvalidOperationException("RabbitMQ channel belum siap.");
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, eventArgs) =>
        {
            var rawMessage = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            try
            {
                var payload = JsonSerializer.Deserialize<DetectionEventMessage>(
                    rawMessage,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (payload is null)
                {
                    throw new InvalidOperationException("Payload queue kosong atau tidak valid.");
                }

                await PersistDetectionAsync(payload, stoppingToken);
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Gagal memproses message RabbitMQ: {RawMessage}", rawMessage);
                _channel.BasicNack(eventArgs.DeliveryTag, false, false);
            }
        };

        _channel.BasicConsume(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer);

        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task PersistDetectionAsync(DetectionEventMessage payload, CancellationToken cancellationToken)
    {
        var uploadsRoot = Path.Combine(_environment.ContentRootPath, "App_Data", "Uploads", "detections");
        Directory.CreateDirectory(uploadsRoot);

        var originalName = string.IsNullOrWhiteSpace(payload.ImageFileName)
            ? "capture.jpg"
            : payload.ImageFileName;
        var safeFileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Path.GetFileName(originalName)}";
        var absolutePath = Path.Combine(uploadsRoot, safeFileName);
        var imageBytes = Convert.FromBase64String(payload.ImageBase64);

        await File.WriteAllBytesAsync(absolutePath, imageBytes, cancellationToken);

        var publicImagePath = $"/uploads/detections/{safeFileName}";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO detection_records (
                id, timestamp_utc, object_name, camera_id, image_path, confidence, zone, created_at_utc
            ) VALUES (
                @id, @timestampUtc, @objectName, @cameraId, @imagePath, @confidence, @zone, @createdAtUtc
            );
            """, connection);

        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("timestampUtc", payload.TimestampUtc);
        command.Parameters.AddWithValue("objectName", payload.ObjectName);
        command.Parameters.AddWithValue("cameraId", payload.CameraId);
        command.Parameters.AddWithValue("imagePath", publicImagePath);
        command.Parameters.AddWithValue("confidence", payload.Confidence);
        command.Parameters.AddWithValue("zone", payload.Zone);
        command.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);

        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation(
            "Detection event {EventId} stored for object {ObjectName} from camera {CameraId}",
            payload.EventId,
            payload.ObjectName,
            payload.CameraId);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
