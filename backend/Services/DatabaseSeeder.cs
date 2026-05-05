using Npgsql;

namespace backend.Services;

public sealed class DatabaseSeeder
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseSeeder(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task SeedAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        await using (var createCommand = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS detection_records (
                id UUID PRIMARY KEY,
                timestamp_utc TIMESTAMPTZ NOT NULL,
                object_name VARCHAR(120) NOT NULL,
                camera_id VARCHAR(80) NOT NULL,
                image_path VARCHAR(500) NOT NULL,
                confidence NUMERIC(10,4) NOT NULL,
                zone VARCHAR(120) NOT NULL,
                created_at_utc TIMESTAMPTZ NOT NULL
            );
            """, connection))
        {
            await createCommand.ExecuteNonQueryAsync();
        }

        await using var countCommand = new NpgsqlCommand("SELECT COUNT(*) FROM detection_records;", connection);
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        if (count > 0)
        {
            return;
        }

        await using var insertCommand = new NpgsqlCommand("""
            INSERT INTO detection_records (
                id, timestamp_utc, object_name, camera_id, image_path, confidence, zone, created_at_utc
            )
            VALUES
                (@id1, @timestamp1, @object1, @camera1, @image1, @confidence1, @zone1, @created1),
                (@id2, @timestamp2, @object2, @camera2, @image2, @confidence2, @zone2, @created2);
            """, connection);

        insertCommand.Parameters.AddWithValue("id1", Guid.NewGuid());
        insertCommand.Parameters.AddWithValue("timestamp1", DateTime.UtcNow.AddMinutes(-42));
        insertCommand.Parameters.AddWithValue("object1", "person");
        insertCommand.Parameters.AddWithValue("camera1", "CAM_01");
        insertCommand.Parameters.AddWithValue("image1", "/images/cam01-1.jpg");
        insertCommand.Parameters.AddWithValue("confidence1", 0.97m);
        insertCommand.Parameters.AddWithValue("zone1", "Inbound");
        insertCommand.Parameters.AddWithValue("created1", DateTime.UtcNow);

        insertCommand.Parameters.AddWithValue("id2", Guid.NewGuid());
        insertCommand.Parameters.AddWithValue("timestamp2", DateTime.UtcNow.AddMinutes(-18));
        insertCommand.Parameters.AddWithValue("object2", "forklift");
        insertCommand.Parameters.AddWithValue("camera2", "CAM_02");
        insertCommand.Parameters.AddWithValue("image2", "/images/cam02-1.jpg");
        insertCommand.Parameters.AddWithValue("confidence2", 0.93m);
        insertCommand.Parameters.AddWithValue("zone2", "Aisle A");
        insertCommand.Parameters.AddWithValue("created2", DateTime.UtcNow);

        await insertCommand.ExecuteNonQueryAsync();
    }
}
