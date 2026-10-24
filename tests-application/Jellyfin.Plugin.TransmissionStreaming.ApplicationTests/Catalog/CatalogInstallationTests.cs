namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Hosting;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

public sealed class CatalogInstallationTests(ITestOutputHelper output)
{
    [Theory, AutoMoqData]
    internal async Task Should_InstallFromRepositoryAndLoadAfterRestart(CatalogSource source, CatalogInstallation sut)
    {
        // Arrange
        await using var installation = sut;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var cancellationToken = deadline.Token;
        try
        {
            var publishedRepository = Environment.GetEnvironmentVariable("TRANSMISSION_STREAMING_TEST_REPOSITORY");
            var repository = string.IsNullOrWhiteSpace(publishedRepository) ? null : new Uri(publishedRepository, UriKind.Absolute);
            await sut.StartAsync(source, cancellationToken, repository: repository);
            var expectedPluginStatus = "Active";
            var preinstalled = await sut.Api.RequestAsync("/Plugins");
            var package = (await sut.PackagesAsync(cancellationToken))!.AsArray().Single();

            // Act
            await sut.InstallAsync(cancellationToken);
            var pending = await sut.Api.RequestAsync("/System/Info");
            var installedFiles = Directory.EnumerateFiles(sut.InstalledDirectory).Select(Path.GetFileName).ToArray();
            await sut.RestartAsync(cancellationToken);
            var status = await sut.Api.RequestAsync("/TransmissionStreaming/status");
            var plugins = await sut.Api.RequestAsync("/Plugins");
            var loaded = plugins!.AsArray().Single(plugin => PluginId(plugin) == Guid.Parse(sut.Bundle.Id));

            // Assert
            preinstalled!.AsArray().Should().NotContain(plugin => PluginId(plugin) == Guid.Parse(sut.Bundle.Id));
            Guid.Parse(JellyfinApi.Value<string>(package, "guid")).Should().Be(Guid.Parse(sut.Bundle.Id));
            JellyfinApi.Value<bool>(pending, "HasPendingRestart").Should().BeTrue();
            installedFiles.Should().BeEquivalentTo(sut.Bundle.Assemblies.Append("meta.json"));
            JellyfinApi.Value<string>(loaded, "Version").Should().Be(sut.Bundle.Version);
            JellyfinApi.Value<string>(loaded, "Status").Should().Be(expectedPluginStatus);
            JellyfinApi.Value<bool>(status, "Enabled").Should().BeFalse("repository installation must retain the safe disabled default");
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectPackageWithWrongChecksum(CatalogSource source, CatalogInstallation sut)
    {
        // Arrange
        await using var installation = sut;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var cancellationToken = deadline.Token;
        try
        {
            await sut.StartAsync(source, cancellationToken, corruptChecksum: true);

            // Act
            var failure = await Record.ExceptionAsync(() => sut.InstallAsync(cancellationToken));
            var plugins = await sut.Api.RequestAsync("/Plugins");

            // Assert
            failure.Should().BeOfType<HttpRequestException>();
            Directory.Exists(sut.InstalledDirectory).Should().BeFalse();
            plugins!.AsArray().Should().NotContain(plugin => PluginId(plugin) == Guid.Parse(sut.Bundle.Id));
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    [Theory, AutoMoqData]
    internal async Task Should_LoadLocalDevelopmentBuild(CatalogSource source, CatalogInstallation sut)
    {
        // Arrange
        await using var installation = sut;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var cancellationToken = deadline.Token;
        var expectedPluginStatus = "Active";
        try
        {
            // Act
            await sut.StartDevelopmentAsync(source, cancellationToken);
            var status = await sut.Api.RequestAsync("/TransmissionStreaming/status");
            var plugins = await sut.Api.RequestAsync("/Plugins");
            var loaded = plugins!.AsArray().Single(plugin => PluginId(plugin) == Guid.Parse(sut.Bundle.Id));

            // Assert
            JellyfinApi.Value<string>(loaded, "Version").Should().Be(sut.Bundle.Version);
            JellyfinApi.Value<string>(loaded, "Status").Should().Be(expectedPluginStatus);
            JellyfinApi.Value<bool>(status, "Enabled").Should().BeFalse();
            foreach (var assembly in sut.Bundle.Assemblies)
            {
                var debugBytes = await File.ReadAllBytesAsync(Path.Combine(sut.DebugOutputDirectory!, assembly), cancellationToken);
                var installedBytes = await File.ReadAllBytesAsync(Path.Combine(sut.InstalledDirectory, assembly), cancellationToken);
                SHA256.HashData(installedBytes).Should().Equal(SHA256.HashData(debugBytes), "the installer must copy the actual Debug build");
            }
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    private static Guid PluginId(JsonNode? plugin)
    {
        return Guid.Parse(JellyfinApi.Value<string>(plugin, "Id"));
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
