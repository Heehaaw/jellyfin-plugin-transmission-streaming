using Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;
using TransmissionStreaming.TestSupport.Torrents;
using Xunit;

[assembly: AssemblyFixture(typeof(DownloadServices))]
[assembly: AssemblyFixture(typeof(JellyfinBaseline))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
