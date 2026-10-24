namespace TransmissionStreaming.TestSupport.Torrents.Seed;

using System.Net.Sockets;

internal static class BitTorrentPeer
{
    public static async Task AcceptAsync(TcpListener listener, SeedControl control, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var peer = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = ServeAsync(peer, control.Current, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
    }

    private static async Task ServeAsync(TcpClient peer, SeedScenario scenario, CancellationToken stoppingToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, scenario.CancellationToken);
        var cancellationToken = lifetime.Token;
        using (peer)
        {
            try
            {
                var stream = peer.GetStream();
                if (!await BitTorrentProtocol.HandshakeAsync(stream, scenario.Configuration.Hash, cancellationToken))
                {
                    return;
                }
                await BitTorrentTransfer.CopyAsync(stream, scenario, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (Exception exception)
            {
                scenario.PeerFailure = $"Controlled peer failed ({exception.GetType().Name}).";
                Console.Error.WriteLine(scenario.PeerFailure);
            }
        }
    }
}
