namespace Jellyfin.Plugin.HdrPlayback.Tests.Transcoding;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::HdrPlayback.Core.Playback;
using global::HdrPlayback.Core.Transcoding;
using Jellyfin.Plugin.HdrPlayback.Transcoding;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using Moq;
using Xunit;

public sealed class HdrTranscodeManagerTests
{
    [Theory, AutoMoqData]
    internal void Should_ExtendSelectedJobAndRetainNativeOwnership(
        StreamState source,
        string output,
        string arguments,
        Guid userId,
        CancellationTokenSource cancellation,
        [Frozen] Mock<ITranscodeManager> native,
        [Frozen] Mock<IHdrSessionStore> sessions,
        [Frozen] Mock<IHdrCommandTransformer> transformer,
        HdrTranscodeManager sut)
    {
        // Arrange
        using var owned = cancellation;
        var key = new HdrSessionKey(userId, source.Request.DeviceId, source.Request.PlaySessionId ?? "");
        var converted = arguments + "-converted";
        var expected = Task.FromResult<TranscodingJob>(null!);
        sessions.Setup(store => store.Contains(key)).Returns(true);
        transformer.Setup(adapter => adapter.Transform(arguments)).Returns(converted);
        native.Setup(manager => manager.StartFfMpeg(source, output, converted, userId, source.TranscodingType, cancellation, null)).Returns(expected);

        // Act
        var result = sut.StartFfMpeg(source, output, arguments, userId, source.TranscodingType, cancellation);

        // Assert
        result.Should().BeSameAs(expected);
        native.Verify(manager => manager.StartFfMpeg(source, output, converted, userId, source.TranscodingType, cancellation, null), Times.Once);
    }

    [Theory, AutoMoqData]
    internal void Should_ForwardUnselectedTranscodeUnchanged(
        StreamState source,
        string output,
        string arguments,
        Guid userId,
        CancellationTokenSource cancellation,
        [Frozen] Mock<ITranscodeManager> native,
        [Frozen] Mock<IHdrSessionStore> sessions,
        [Frozen] Mock<IHdrCommandTransformer> transformer,
        HdrTranscodeManager sut)
    {
        // Arrange
        using var owned = cancellation;
        var key = new HdrSessionKey(userId, source.Request.DeviceId, source.Request.PlaySessionId ?? "");
        sessions.Setup(store => store.Contains(key)).Returns(false);
        native.Setup(manager => manager.StartFfMpeg(source, output, arguments, userId, source.TranscodingType, cancellation, null))
            .Returns(Task.FromResult<TranscodingJob>(null!));

        // Act
        _ = sut.StartFfMpeg(source, output, arguments, userId, source.TranscodingType, cancellation);

        // Assert
        native.Verify(manager => manager.StartFfMpeg(source, output, arguments, userId, source.TranscodingType, cancellation, null), Times.Once);
        transformer.Verify(adapter => adapter.Transform(It.IsAny<string>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RetainSelectionWhenNativeSeekingRestartsAJob(
        HdrSessionKey source,
        [Frozen] Mock<ITranscodeManager> native,
        [Frozen] Mock<IHdrSessionStore> sessions,
        HdrTranscodeManager sut)
    {
        // Arrange
        native.Setup(manager => manager.KillTranscodingJobs(source.DeviceId, source.PlaySessionId, It.IsAny<Func<string, bool>>())).Returns(Task.CompletedTask);

        // Act
        await sut.KillTranscodingJobs(source.DeviceId, source.PlaySessionId, _ => false);

        // Assert
        native.Verify(manager => manager.KillTranscodingJobs(source.DeviceId, source.PlaySessionId, It.IsAny<Func<string, bool>>()), Times.Once);
        sessions.Verify(store => store.Remove(It.IsAny<HdrSessionKey>()), Times.Never);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture
        {
            RepeatCount = 1
        };
        fixture.Customize(new AutoMoqCustomization
        {
            ConfigureMembers = true,
            GenerateDelegates = true
        });
        fixture.Customize<StreamState>(composer => composer.OmitAutoProperties().With(state => state.Request).With(state => state.MediaSource));
        fixture.Customize<StreamingRequestDto>(composer => composer.OmitAutoProperties().With(request => request.DeviceId).With(request => request.PlaySessionId));
        return fixture;
    }
    );
}
