namespace Jellyfin.Plugin.TransmissionStreaming.Persistence;

using Jellyfin.Plugin.TransmissionStreaming;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using Microsoft.Data.Sqlite;
using System.Text.Json;


/// <summary>Durable stream identities; completion never replaces a projected entry.</summary>
public sealed class ProjectionStore
{
    private readonly string connectionString;

    public ProjectionStore(Plugin plugin)
    {
        Directory.CreateDirectory(plugin.StateDirectory);
        var path = Path.Combine(plugin.StateDirectory, "projections.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS projections (id TEXT PRIMARY KEY, payload TEXT NOT NULL)";
        command.ExecuteNonQuery();
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public IReadOnlyList<Projection> All()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM projections ORDER BY id";
        using var reader = command.ExecuteReader();
        var result = new List<Projection>();
        while (reader.Read()) result.Add(JsonSerializer.Deserialize<Projection>(reader.GetString(0))!);
        return result;
    }

    public Projection? Get(string id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM projections WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<Projection>(json) : null;
    }

    public void Save(Projection projection)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO projections VALUES ($id, $payload) ON CONFLICT(id) DO UPDATE SET payload = excluded.payload";
        command.Parameters.AddWithValue("$id", projection.Id);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(projection));
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
