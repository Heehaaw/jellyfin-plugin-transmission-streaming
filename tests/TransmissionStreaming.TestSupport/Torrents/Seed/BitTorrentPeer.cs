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
                _ = ServeAsync
                (
                    peer: peer,
                    scenario: control.Current,
                    stoppingToken: cancellationToken
                );
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task ServeAsync(TcpClient peer, SeedScenario scenario, CancellationToken stoppingToken)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource
        (
            token1: stoppingToken,
            token2: scenario.CancellationToken
        );
        var cancellationToken = lifetime.Token;
        using (peer)
        {
            try
            {
                var stream = peer.GetStream();
                var accepted = await BitTorrentProtocol.HandshakeAsync
                (
                    stream: stream,
                    hash: scenario.Configuration.Hash,
                    cancellationToken: cancellationToken
                );
                if (!accepted)
                {
                    return;
                }

                await BitTorrentTransfer.CopyAsync
                (
                    stream: stream,
                    control: scenario,
                    cancellationToken: cancellationToken
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
            catch (Exception exception)
            {
                scenario.PeerFailure = $"Controlled peer failed ({exception.GetType().Name}).";
                Console.Error.WriteLine(scenario.PeerFailure);
            }
        }
    }
}
