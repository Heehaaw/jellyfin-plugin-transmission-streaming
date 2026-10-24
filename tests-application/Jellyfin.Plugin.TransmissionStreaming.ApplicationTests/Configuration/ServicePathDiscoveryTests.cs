namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Configuration;

using AutoFixture;
using AutoFixture.AutoMoq;
using FluentAssertions;
using global::TransmissionStreaming.TestSupport.Testing;
using Hosting;
using System.Text.Json.Nodes;
using Xunit;

/// <summary>Exercises authenticated discovery and manual-override persistence in the actual Jellyfin host.</summary>
public sealed class ServicePathDiscoveryTests(JellyfinTestServer sut, ITestOutputHelper output) : IClassFixture<JellyfinTestServer>
{
    [Theory, AutoMoqData]
    public async Task Should_DiscoverUsingUnsavedConnectionsAndRetainManualMappings(ServiceConnectionSource source)
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var configurationPath = $"/Plugins/{PluginBundle.BuildOutput.Id}/Configuration";
        var configuration = (await sut.Api.RequestAsync(configurationPath))!.AsObject();
        configuration["Enabled"] = false;
        configuration["TransmissionUrl"] = "http://unconfigured.invalid/transmission/rpc";
        await sut.Api.RequestAsync(configurationPath, configuration);
        var transmission = source with { Url = sut.Swarm.ContainerRpcUrl.AbsoluteUri, Username = "", Password = "" };
        var radarr = source with { Url = sut.Arr.ContainerUrl.AbsoluteUri, ApiKey = sut.Arr.Key };
        var manualRoot = sut.ImportedMoviesPath + "/" + source.Username;

        try
        {
            // Act
            var discovered = await sut.Api.RequestAsync("/TransmissionStreaming/configuration/paths/Transmission", transmission);
            var library = await sut.Api.RequestAsync("/TransmissionStreaming/configuration/paths/Radarr", radarr);
            var unchanged = await sut.Api.RequestAsync(configurationPath);
            var mappings = JellyfinApi.Field(discovered, "Mappings")!.DeepClone().AsArray();
            mappings[0]!["LocalRoot"] = manualRoot;
            configuration["PathMappings"] = mappings;
            await sut.Api.RequestAsync(configurationPath, configuration);
            await sut.RestartAsync();
            var retained = await sut.Api.RequestAsync(configurationPath);

            // Assert
            var remotePaths = JellyfinApi.Field(discovered, "Mappings")!.AsArray()
                .Select(mapping => JellyfinApi.Value<string>(mapping, "RemoteRoot")).ToArray();
            remotePaths.Should().Contain(sut.Swarm.RemoteRoot + "/completed");
            remotePaths.Should().Contain(sut.Swarm.RemoteRoot + "/incomplete");
            JellyfinApi.Field(library, "Mappings")!.AsArray().Should().ContainSingle();
            JellyfinApi.Value<string>(unchanged, "TransmissionUrl").Should().Be(configuration["TransmissionUrl"]!.GetValue<string>());
            JellyfinApi.Value<bool>(retained, "Enabled").Should().BeFalse();
            var saved = JellyfinApi.Field(retained, "PathMappings")!.AsArray()[0];
            JellyfinApi.Value<string>(saved, "LocalRoot").Should().Be(manualRoot);
            JellyfinApi.Value<string>(saved, "RemoteRoot").Should().Be(remotePaths[0]);
        }
        catch
        {
            output.WriteLine(await sut.DiagnosticsAsync());
            throw;
        }
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}

public sealed record ServiceConnectionSource(string Url, string Username, string Password, string ApiKey);
