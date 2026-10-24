namespace Jellyfin.Plugin.TransmissionStreaming.ApplicationTests.Playback;

using FluentAssertions;
using Hosting;
using System.Net;
using System.Net.Http.Headers;

/// <summary>Captures byte delivery and decoding while the controlled seed withholds later pieces.</summary>
internal sealed record ProgressivePlayback(HttpStatusCode HeadStatus, long? Length, HttpStatusCode RangeStatus,
    long? RangeLength, byte[] FirstPiece, byte[] FullVideo, bool ReaderWaited, bool DecoderWaited,
    string ReferenceFrames, string StreamedFrames)
{
    public static async Task<ProgressivePlayback> CaptureAsync(JellyfinTestServer server, HttpClient client, string url,
        CancellationToken cancellationToken)
    {
        using var available = new HttpRequestMessage(HttpMethod.Get, url);
        available.Headers.Range = new RangeHeaderValue(0, server.Swarm.PieceSize - 1);
        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), cancellationToken);
        using var range = await client.SendAsync(available, cancellationToken);
        var firstPiece = await range.Content.ReadAsByteArrayAsync(cancellationToken);
        using var playback = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await using var body = await playback.Content.ReadAsStreamAsync(cancellationToken);
        var prefix = new byte[server.Swarm.PieceSize];
        await body.ReadExactlyAsync(prefix, cancellationToken);

        var nextByte = new byte[1];
        var pendingRead = body.ReadAsync(nextByte, cancellationToken).AsTask();
        var readerWaited = await WaitsForPiecesAsync(pendingRead, cancellationToken);
        var referenceFrames = await server.Decoder.FileHashAsync(server.ReferenceVideoPath, cancellationToken);
        var decodedStream = server.Decoder.StreamHashAsync(server.ProjectionFilePath, cancellationToken);
        var decoderWaited = await WaitsForPiecesAsync(decodedStream, cancellationToken);
        await server.Swarm.AllowAllPiecesAsync(cancellationToken);

        var streamedFrames = await decodedStream.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        var firstAfterWait = await pendingRead.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await using var received = new MemoryStream();
        await received.WriteAsync(prefix, cancellationToken);
        await received.WriteAsync(nextByte.AsMemory(0, firstAfterWait), cancellationToken);
        await body.CopyToAsync(received, cancellationToken);
        return new ProgressivePlayback(head.StatusCode, head.Content.Headers.ContentLength, range.StatusCode,
            range.Content.Headers.ContentRange?.Length, firstPiece, received.ToArray(), readerWaited,
            decoderWaited, referenceFrames, streamedFrames);
    }

    public void ShouldMatch(byte[] expected, int pieceSize)
    {
        HeadStatus.Should().Be(HttpStatusCode.OK);
        Length.Should().Be(expected.Length);
        RangeStatus.Should().Be(HttpStatusCode.PartialContent);
        RangeLength.Should().Be(expected.Length);
        FirstPiece.Should().Equal(expected.Take(pieceSize));
        ReaderWaited.Should().BeTrue("the local seed withheld every remaining piece");
        DecoderWaited.Should().BeTrue("decoding must wait for withheld pieces");
        StreamedFrames.Should().Be(ReferenceFrames, "both inputs must decode to the same video frames");
        FullVideo.Should().Equal(expected);
    }

    private static async Task<bool> WaitsForPiecesAsync(Task operation, CancellationToken cancellationToken)
    {
        // External processes use real time; this bounded observation proves they have not consumed unavailable bytes.
        return await Task.WhenAny(operation, Task.Delay(700, cancellationToken)) != operation;
    }
}
