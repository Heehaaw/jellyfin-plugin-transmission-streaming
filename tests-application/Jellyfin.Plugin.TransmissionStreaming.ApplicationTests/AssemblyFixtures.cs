using Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Hosting;
using Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Ui;
using TransmissionStreaming.TestSupport.Torrents;
using Xunit;

[assembly: AssemblyFixture(typeof(DownloadServices))]
[assembly: AssemblyFixture(typeof(JellyfinBaseline))]
[assembly: AssemblyFixture(typeof(HeadlessBrowser))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
