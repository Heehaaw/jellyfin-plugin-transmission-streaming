namespace TransmissionStreaming.TestSupport.Torrents;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Net;

internal static class SwarmContainers
{
    private const string TransmissionImage = "lscr.io/linuxserver/transmission:4.1.3@sha256:55aaeedf9fc354d34136387e15ee0ba4679096f20398dd2875867e5cd5cf8be5";
    private const string SeedImage = "mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317";
    private const string ResourcePrefix = "TransmissionStreaming.TestSupport.Torrents.Seed.";
    private const string EntryScript = "/Seed/Seed.cs";

    public static IContainer Seed(string seedRoot, INetwork network)
    {
        var builder = new ContainerBuilder(SeedImage)
            .WithCreateParameterModifier(parameters => parameters.User = ContainerIdentity.User)
            .WithNetwork(network)
            .WithNetworkAliases(SwarmSettings.SeedHost, SwarmSettings.ArrHost, SwarmSettings.CatalogHost)
            .WithEnvironment("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
            .WithEnvironment("DOTNET_NOLOGO", "1")
            .WithEnvironment("DOTNET_CLI_HOME", "/tmp")
            .WithEnvironment("NUGET_PACKAGES", "/tmp/nuget")
            .WithEnvironment("XDG_DATA_HOME", "/tmp/data")
            .WithEnvironment("XDG_CACHE_HOME", "/tmp/cache")
            .WithBindMount(seedRoot, "/fixture")
            .WithBindMount(Directory.GetParent(seedRoot)!.FullName, SwarmSettings.RemoteRoot)
            .WithCommand("dotnet", "run", "--file", EntryScript)
            .WithPortBinding(SwarmSettings.HttpPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(SwarmSettings.HttpPort).ForPath("/health")));

        // File-based includes resolve beside the entry script inside the container.
        var assembly = typeof(SwarmContainers).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var resource = assembly.GetManifestResourceStream(name)!;
            using var content = new MemoryStream();
            resource.CopyTo(content);
            builder = builder.WithResourceMapping(content.ToArray(), "/Seed/" + name[ResourcePrefix.Length..]);
        }

        return builder.Build();
    }

    public static IContainer Transmission(string workspace, INetwork network)
    {
        return new ContainerBuilder(TransmissionImage)
            .WithCreateParameterModifier(parameters => parameters.User = ContainerIdentity.User)
            .WithNetwork(network)
            .WithNetworkAliases(SwarmSettings.TransmissionHost)
            .WithBindMount(workspace, SwarmSettings.RemoteRoot)
            .WithEntrypoint("/usr/bin/transmission-daemon")
            .WithCommand("--foreground", "--config-dir", SwarmSettings.RemoteRoot + "/.transmission", "--log-level", "warn")
            .WithPortBinding(SwarmSettings.RpcPort, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                    .ForPort(SwarmSettings.RpcPort).ForPath(SwarmSettings.RpcPath).ForStatusCode(HttpStatusCode.Conflict)
                )
            )
            .Build();
    }
}
