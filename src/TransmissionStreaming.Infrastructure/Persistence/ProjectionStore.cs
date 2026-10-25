namespace TransmissionStreaming.Infrastructure.Persistence;

using Core.Configuration;
using Core.Projection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Text.Json;
using ProjectionEntry = Core.Projection.Projection;

/// <summary>Durable stream identities; completion never replaces a projected entry.</summary>
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
        command.CommandText = "CREATE TABLE IF NOT EXISTS projections (id TEXT PRIMARY KEY, payload TEXT NOT NULL)";
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
