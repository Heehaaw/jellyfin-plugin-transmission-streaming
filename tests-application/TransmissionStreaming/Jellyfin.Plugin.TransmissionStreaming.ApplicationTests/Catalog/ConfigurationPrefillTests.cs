namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Catalog;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using global::TransmissionStreaming.TestSupport.Testing;
using Hosting;
using Xunit;

/// <summary>Verifies startup inference against real Jellyfin libraries and persistent plugin configuration.</summary>
public sealed class ConfigurationPrefillTests(ITestOutputHelper output)
{
    [Theory, AutoMoqData]
    internal async Task Should_PrefillLocalPathsAndRetainSavedOverridesAfterRestart(CatalogSource source, CatalogInstallation sut)
    {
        // Arrange
        await using var installation = sut;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var cancellationToken = deadline.Token;
        await sut.StartAsync(source, cancellationToken);
        var mediaRoot = await sut.AddEmptyMediaLibrariesAsync(cancellationToken);
        var configurationPath = $"/Plugins/{sut.Bundle.Id}/Configuration";
        var projectionSuffix = "/transmission-streaming/library";
        var suppliedRoot = mediaRoot + "/chosen-" + source.RunId.ToString("N");
        var suppliedProjection = suppliedRoot + "/projections";
        var suppliedUrl = $"https://{source.RunId:N}.invalid/jellyfin";

        try
        {
            // Act
            await sut.InstallAsync(cancellationToken);
            await sut.RestartAsync(cancellationToken);
            var inferred = await sut.Api.RequestAsync(configurationPath);
            var defaults = await sut.Api.RequestAsync("/TransmissionStreaming/configuration/defaults");
            var edited = inferred!.DeepClone().AsObject();
            edited["LocalMediaRoot"] = suppliedRoot;
            edited["ProjectionRoot"] = suppliedProjection;
            edited["JellyfinPublicUrl"] = suppliedUrl;
            await sut.Api.RequestAsync(configurationPath, edited);
            await sut.RestartAsync(cancellationToken);
            var retained = await sut.Api.RequestAsync(configurationPath);

            // Assert
            JellyfinApi.Value<string>(inferred, "LocalMediaRoot").Should().Be(mediaRoot);
            JellyfinApi.Value<string>(inferred, "ProjectionRoot").Should().EndWith(projectionSuffix);
            JellyfinApi.Value<bool>(inferred, "Enabled").Should().BeFalse();
            JellyfinApi.Value<string>(defaults, "LocalMediaRoot").Should().Be(mediaRoot);
            JellyfinApi.Value<string>(retained, "LocalMediaRoot").Should().Be(suppliedRoot);
            JellyfinApi.Value<string>(retained, "ProjectionRoot").Should().Be(suppliedProjection);
            JellyfinApi.Value<string>(retained, "JellyfinPublicUrl").Should().Be(suppliedUrl);
            JellyfinApi.Value<bool>(retained, "Enabled").Should().BeFalse();
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 1
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            return fixture;
        }
    );
}
