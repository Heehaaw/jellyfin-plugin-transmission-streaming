namespace TransmissionStreaming.Infrastructure.Persistence;

using Core.Configuration;
using Core.Projection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using ProjectionEntry = Core.Projection.Projection;

/// <summary>Persists active stream identities and a bounded history of successful native handoffs.</summary>
internal sealed class ProjectionStore : IProjectionStore
{
    private readonly string connectionString;

    public ProjectionStore(IOptionsMonitor<StreamingOptions> options)
    {
        var stateDirectory = options.CurrentValue.StateDirectory;
        Directory.CreateDirectory(stateDirectory);
        var path = Path.Combine(stateDirectory, "projections.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
                              CREATE TABLE IF NOT EXISTS projections (id TEXT PRIMARY KEY, payload TEXT NOT NULL);
                              CREATE TABLE IF NOT EXISTS completed_projections (id TEXT PRIMARY KEY, payload TEXT NOT NULL, completed_at TEXT NOT NULL);
                              """;
        command.ExecuteNonQuery();
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public IReadOnlyList<ProjectionEntry> All()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM projections ORDER BY id";
        using var reader = command.ExecuteReader();
        var result = new List<ProjectionEntry>();
        while (reader.Read())
        {
            result.Add(JsonSerializer.Deserialize<ProjectionEntry>(reader.GetString(0))!);
        }

        return result;
    }

    public IReadOnlyList<CompletedProjection> Completed()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM completed_projections ORDER BY completed_at DESC, id DESC";
        using var reader = command.ExecuteReader();
        var result = new List<CompletedProjection>();
        while (reader.Read())
        {
            result.Add(JsonSerializer.Deserialize<CompletedProjection>(reader.GetString(0))!);
        }

        return result;
    }

    public void Complete(CompletedProjection entry, int historyLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(historyLimit);
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // A failed archive, retirement or trim leaves the active entry and prior history intact for retry.
        command.CommandText = """
                              INSERT INTO completed_projections (id, payload, completed_at) VALUES ($id, $payload, $completedAt)
                              ON CONFLICT(id) DO UPDATE SET payload = excluded.payload, completed_at = excluded.completed_at;
                              DELETE FROM projections WHERE id = $id;
                              DELETE FROM completed_projections WHERE id NOT IN (
                                  SELECT id FROM completed_projections ORDER BY completed_at DESC, id DESC LIMIT $limit
                              );
                              """;
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(entry));
        // Normalize the indexed key so lexical SQLite ordering also compares different source UTC offsets correctly.
        command.Parameters.AddWithValue("$completedAt", entry.CompletedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$limit", historyLimit);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public ProjectionEntry? Get(string id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM projections WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<ProjectionEntry>(json) : null;
    }

    public void Save(ProjectionEntry projection)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO projections VALUES ($id, $payload) ON CONFLICT(id) DO UPDATE SET payload = excluded.payload";
        command.Parameters.AddWithValue("$id", projection.Id);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(projection));
        command.ExecuteNonQuery();
    }

    public void Remove(string id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM projections WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
