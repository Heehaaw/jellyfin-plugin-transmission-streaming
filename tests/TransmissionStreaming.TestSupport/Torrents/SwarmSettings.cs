namespace TransmissionStreaming.TestSupport.Torrents;

/// <summary>Addresses and paths shared by the test host, daemon, and controlled peer.</summary>
internal static class SwarmSettings
{
    public const int PieceSize = 16 * 1024;
    public const int RpcPort = 9091;
    public const int HttpPort = 8080;
    public const int PeerPort = 6881;
    public const string TransmissionHost = "transmission";
    public const string SeedHost = "seed";
    public const string ArrHost = "arr";
    public const string RpcPath = "/transmission/rpc";
    public const string RemoteRoot = "/data";
    public const string CompletedDirectory = "completed";
    public const string IncompleteDirectory = "incomplete";
    public const string ArrKey = "synthetic-arr-key";
    public static string CompletedRoot => RemoteRoot + "/" + CompletedDirectory;
    public static string IncompleteRoot => RemoteRoot + "/" + IncompleteDirectory;
    public static Uri ContainerRpcUrl => new UriBuilder("http", TransmissionHost, RpcPort, RpcPath).Uri;
    public static Uri ContainerArrUrl => new UriBuilder("http", ArrHost, HttpPort).Uri;
}
