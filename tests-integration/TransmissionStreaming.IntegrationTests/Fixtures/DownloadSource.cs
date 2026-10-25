namespace TransmissionStreaming.IntegrationTests.Fixtures;

using Logic.Discovery;

internal sealed record DownloadSource
{
    public byte[] Media
    {
        get;
        init;
    } = [];

    public string FileName
    {
        get;
        init;
    } = "";

    public DiscoverDownloads.Command Discover
    {
        get;
        init;
    } = new();
}
