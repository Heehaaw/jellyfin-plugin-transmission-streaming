namespace Shared.Infrastructure.IntegrationTests.Packaging;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Owns an isolated assembly-adjacent metadata location for reader tests.</summary>
internal sealed class ReleaseMetadataFiles : IDisposable
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public ReleaseMetadataFiles()
    {
        Directory.CreateDirectory(directory);
    }

    public string AssemblyFilePath => Path.Combine(directory, "Prototype.dll");

    public void Write<T>(T source)
    {
        var contents = JsonSerializer.Serialize(source, Options);
        WriteRaw(contents);
    }

    public void WriteRaw(string source)
    {
        var path = Path.Combine(directory, "meta.json");
        File.WriteAllText(path, source);
    }

    public void RemoveDirectory()
    {
        Directory.Delete(directory, recursive: true);
    }

    public void Dispose()
    {
        // xUnit also disposes generated arguments after the test's local ownership scope.
        if (Directory.Exists(directory))
        {
            RemoveDirectory();
        }
    }
}
