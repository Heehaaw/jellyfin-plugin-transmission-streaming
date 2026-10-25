namespace HdrPlayback.IntegrationTests.Packaging;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Testing;
using TransmissionStreaming.Packaging;
using Xunit;

public sealed class CatalogTests
{
    [Theory, AutoMoqData]
    internal async Task Should_PreserveBothPluginsAndTheirReleaseHistory(
        PluginDescriptor source,
        CatalogPackage[] previous,
        byte[] payload,
        CatalogFiles files)
    {
        // Arrange
        await using var owned = files;
        var prior = previous[0].Versions[0] with
        {
            Version = "0.0.0.0"
        };
        previous[0] = previous[0] with
        {
            Guid = source.Guid,
            Versions = [prior]
        };
        var other = previous[1];
        var previousUrl = await files.ServeAsync(previous);
        await File.WriteAllBytesAsync(files.ArchivePath, payload, TestContext.Current.CancellationToken);
        var download = new Uri("https://example.test/" + source.ArchiveName);
        var options = new PackageOptions(download, previousUrl, null, files.ArchivePath)
        {
            Plugin = "hdr-playback"
        };

        // Act
        var result = await Catalog.WriteAsync(source, files.ArchivePath, options, TestContext.Current.CancellationToken);
        var contents = await File.ReadAllTextAsync(result, TestContext.Current.CancellationToken);
        var manifest = JsonSerializer.Deserialize<CatalogPackage[]>(contents, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        // Assert
        manifest.Should().HaveCount(previous.Length);
        manifest.Single(package => package.Guid == other.Guid).Should().BeEquivalentTo(other);
        var updated = manifest.Single(package => package.Guid == source.Guid);
        updated.Versions.Should().ContainEquivalentOf(prior);
        var release = updated.Versions.Single(version => version.Version == source.Version);
        release.SourceUrl.Should().Be(download);
        release.Checksum.Should().Be(Convert.ToHexStringLower(MD5.HashData(payload)));
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture
        {
            RepeatCount = 2
        };
        fixture.Customize(new AutoMoqCustomization
        {
            ConfigureMembers = true,
            GenerateDelegates = true
        });
        fixture.Register(() => new Version(1, fixture.Create<byte>(), fixture.Create<byte>(), 0));
        fixture.Customize<PluginDescriptor>(composer => composer.With(value => value.Version, () => fixture.Create<Version>().ToString()));
        return fixture;
    }
    );
}

/// <summary>Owns a loopback catalog and generated package bytes for repository-protocol checks.</summary>
internal sealed class CatalogFiles : IAsyncDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("hdr-catalog-tests-").FullName;
    private WebApplication? server;

    public string ArchivePath => Path.Combine(directory, "plugin.zip");

    public async Task<Uri> ServeAsync(CatalogPackage[] manifest)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        server = builder.Build();
        server.MapGet("/", () => Results.Json(manifest));
        await server.StartAsync();
        return new Uri(server.Urls.Single());
    }

    public async ValueTask DisposeAsync()
    {
        if (server is not null)
        {
            await server.DisposeAsync();
        }

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
