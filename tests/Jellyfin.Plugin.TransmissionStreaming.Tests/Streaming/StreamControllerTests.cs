namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Streaming;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using global::TransmissionStreaming.Core.Projection;
using global::TransmissionStreaming.Logic.Status;
using global::TransmissionStreaming.Logic.Streaming;
using Jellyfin.Plugin.TransmissionStreaming.Streaming;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Reflection;
using Xunit;

public sealed class StreamControllerTests
{
    [Theory, AutoMoqData]
    internal async Task Should_ReturnTheApplicationStatus(
        StreamingStatus source,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        mediator.Setup(value => value.Send(It.IsAny<GetStatus.Query>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(source));

        // Act
        var result = await sut.Status(CancellationToken.None);

        // Assert
        result.Value.Should().BeSameAs(source);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReturnNotFoundBeforeTouchingTheResponse(
        Projection source,
        DefaultHttpContext context,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        sut.ControllerContext.HttpContext = context;
        mediator.Setup(value => value.Send(It.IsAny<GetStream.Query>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<Projection?>(null));

        // Act
        var result = await sut.Stream(source.Id, source.Secret, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        context.Response.ContentLength.Should().BeNull();
        mediator.Verify(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_RejectAnUnsatisfiableRange(
        Projection source,
        DefaultHttpContext context,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        sut.ControllerContext.HttpContext = context;
        context.Request.Headers.Range = $"bytes={source.Length}-";
        Authorize(mediator, source);
        var expectedCode = StatusCodes.Status416RangeNotSatisfiable;

        // Act
        var result = await sut.Stream(source.Id, source.Secret, CancellationToken.None);

        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(expectedCode);
        context.Response.Headers.ContentRange.ToString().Should().Be($"bytes */{source.Length}");
        mediator.Verify(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReportLengthWithoutStartingHeadPlayback(
        Projection source,
        DefaultHttpContext context,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        sut.ControllerContext.HttpContext = context;
        context.Request.Method = HttpMethods.Head;
        Authorize(mediator, source);
        var expectedCode = StatusCodes.Status200OK;
        var expectedRanges = "bytes";
        var expectedCache = "private, no-store";

        // Act
        var result = await sut.Stream(source.Id, source.Secret, CancellationToken.None);

        // Assert
        result.Should().BeOfType<EmptyResult>();
        context.Response.StatusCode.Should().Be(expectedCode);
        context.Response.ContentLength.Should().Be(source.Length);
        context.Response.Headers.AcceptRanges.ToString().Should().Be(expectedRanges);
        context.Response.Headers.CacheControl.ToString().Should().Be(expectedCache);
        mediator.Verify(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory, AutoMoqData]
    internal async Task Should_StreamTheRequestedRangeToTheHttpBody(
        Projection source,
        DefaultHttpContext context,
        MemoryStream output,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        sut.ControllerContext.HttpContext = context;
        context.Response.Body = output;
        var start = source.Length / 2;
        var end = source.Length - 1;
        context.Request.Headers.Range = $"bytes={start}-{end}";
        Authorize(mediator, source);
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(Unit.Value));
        var expectedCode = StatusCodes.Status206PartialContent;

        // Act
        var result = await sut.Stream(source.Id, source.Secret, CancellationToken.None);

        // Assert
        result.Should().BeOfType<EmptyResult>();
        context.Response.StatusCode.Should().Be(expectedCode);
        context.Response.ContentLength.Should().Be(end - start + 1);
        context.Response.Headers.ContentRange.ToString().Should().Be($"bytes {start}-{end}/{source.Length}");
        mediator.Verify(
            expression: value => value.Send(
                command: It.Is<StreamMedia.Command>(command =>
                    command.Projection == source && command.Destination == output && command.Offset == start && command.Count == end - start + 1
                ),
                cancellationToken: It.IsAny<CancellationToken>()
            ),
            times: Times.Once
        );
    }

    [Theory, AutoMoqData]
    internal async Task Should_UseTheMediaContainerContentType(
        Projection source,
        DefaultHttpContext context,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        source.SourceName = Path.ChangeExtension(source.SourceName, ".mp4");
        sut.ControllerContext.HttpContext = context;
        context.Request.Method = HttpMethods.Head;
        Authorize(mediator, source);
        var expectedType = "video/mp4";

        // Act
        await sut.Stream(source.Id, source.Secret, CancellationToken.None);

        // Assert
        context.Response.ContentType.Should().Be(expectedType);
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReturnUnavailableBeforeAnyVideoBytesAreSent(
        Projection source,
        DefaultHttpContext context,
        HttpRequestException failure,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        sut.ControllerContext.HttpContext = context;
        context.Request.Headers.Range = $"bytes=0-{source.Length - 1}";
        Authorize(mediator, source);
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<Unit>(failure));
        var expectedCode = StatusCodes.Status503ServiceUnavailable;

        // Act
        var result = await sut.Stream(source.Id, source.Secret, CancellationToken.None);

        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(expectedCode);
        context.Response.ContentLength.Should().BeNull();
        context.Response.Headers.Should().NotContainKey("Content-Range");
    }

    [Theory, AutoMoqData]
    internal async Task Should_ReturnUnavailableForAnUpstreamTimeout(
        Projection source,
        DefaultHttpContext context,
        TaskCanceledException failure,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        sut.ControllerContext.HttpContext = context;
        Authorize(mediator, source);
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<Unit>(failure));
        var expectedCode = StatusCodes.Status503ServiceUnavailable;

        // Act
        var result = await sut.Stream(source.Id, source.Secret, CancellationToken.None);

        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(expectedCode);
    }

    [Theory, AutoMoqData]
    internal async Task Should_StopQuietlyWhenTheClientCancels(
        Projection source,
        DefaultHttpContext context,
        OperationCanceledException failure,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        sut.ControllerContext.HttpContext = context;
        Authorize(mediator, source);
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<Unit>(failure));

        // Act
        var result = await sut.Stream(source.Id, source.Secret, cancellation.Token);

        // Assert
        result.Should().BeOfType<EmptyResult>();
    }

    [Theory, AutoMoqData]
    internal async Task Should_AbortAnAlreadyStartedResponse(
        Projection source,
        DefaultHttpContext context,
        IOException failure,
        Mock<IHttpResponseFeature> response,
        Mock<IHttpRequestLifetimeFeature> lifetime,
        [Frozen] Mock<IMediator> mediator,
        StreamController sut
    )
    {
        // Arrange
        response.SetupAllProperties();
        response.SetupGet(value => value.Headers).Returns(new HeaderDictionary());
        response.SetupGet(value => value.HasStarted).Returns(true);
        context.Features.Set(response.Object);
        context.Features.Set(lifetime.Object);
        sut.ControllerContext.HttpContext = context;
        Authorize(mediator, source);
        mediator.Setup(value => value.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<Unit>(failure));

        // Act
        var result = await sut.Stream(source.Id, source.Secret, CancellationToken.None);

        // Assert
        result.Should().BeOfType<EmptyResult>();
        lifetime.Verify(value => value.Abort(), Times.Once);
    }

    private static void Authorize(Mock<IMediator> mediator, Projection source)
    {
        mediator.Setup(value => value.Send(
                    query: It.Is<GetStream.Query>(query => query.Id == source.Id && query.Key == source.Secret),
                    cancellationToken: It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.FromResult<Projection?>(source));
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
        {
            var fixture = new Fixture
            {
                RepeatCount = 1
            };
            fixture.Customize(
                new AutoMoqCustomization
                {
                    ConfigureMembers = true,
                    GenerateDelegates = true
                }
            );
            fixture.Customize<StreamController>(composer => composer.OmitAutoProperties());
            fixture.Customize<DefaultHttpContext>(composer => composer.OmitAutoProperties());
            fixture.Customize<MemoryStream>(composer => composer.OmitAutoProperties());
            // AutoMoq cannot configure Stream methods with Span parameters.
            fixture.Register<Stream>(() => fixture.Create<MemoryStream>());
            return fixture;
        }
    );
}
