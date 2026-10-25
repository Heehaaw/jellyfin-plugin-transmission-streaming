namespace HdrPlayback.Logic.Tests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Xunit;
using HdrPlayback.Logic.Playback;

public sealed class SelectHdrModeTests
{
    [Theory, AutoMoqData]
    internal async Task Should_EnableSelectedSupportedConversion(SelectHdrMode.Query source, SelectHdrMode.Handler sut)
    {
        // Arrange
        source = Supported(source);

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Available.Should().BeTrue();
        result.Enabled.Should().BeTrue();
        result.Reason.Should().BeNull();
    }

    [Theory, AutoMoqData]
    internal async Task Should_LeaveUnselectedPlaybackUnchanged(SelectHdrMode.Query source, SelectHdrMode.Handler sut)
    {
        // Arrange
        source = Supported(source);
        source = source with
        {
            Context = source.Context with
            {
                Requested = false
            }
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Available.Should().BeTrue();
        result.Enabled.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RespectTranscodingPermission(SelectHdrMode.Query source, SelectHdrMode.Handler sut)
    {
        // Arrange
        source = Supported(source);
        source = source with
        {
            Context = source.Context with
            {
                CanTranscode = false
            }
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeFalse();
        result.Available.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RequireHdrClientSupport(SelectHdrMode.Query source, SelectHdrMode.Handler sut)
    {
        // Arrange
        source = Supported(source);
        source = source with
        {
            Context = source.Context with
            {
                ClientSupportsHdr = false
            }
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeFalse();
        result.Available.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal async Task Should_RequireConfiguredPipeline(SelectHdrMode.Query source, SelectHdrMode.Handler sut)
    {
        // Arrange
        source = Supported(source);
        source = source with
        {
            Context = source.Context with
            {
                PipelineSupported = false
            }
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeFalse();
        result.Available.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal async Task Should_KeepNativeHdrOutsideConversion(SelectHdrMode.Query source, SelectHdrMode.Handler sut)
    {
        // Arrange
        source = Supported(source);
        source = source with
        {
            Context = source.Context with
            {
                SourceIsSdr = false
            }
        };

        // Act
        var result = await sut.Handle(source, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeFalse();
        result.Available.Should().BeFalse();
    }

    private static SelectHdrMode.Query Supported(SelectHdrMode.Query source)
    {
        return source with
        {
            Context = source.Context with
            {
                Requested = true,
                SourceIsSdr = true,
                ClientSupportsHdr = true,
                CanTranscode = true,
                PipelineSupported = true
            }
        };
    }

    [Theory, AutoMoqData]
    internal async Task Should_OfferPreplayChoiceBeforeClientNegotiation(SelectHdrMode.Query source, SelectHdrMode.Handler sut)
    {
        // Arrange
        var query = source with
        {
            Context = source.Context with
            {
                Requested = false,
                SourceIsSdr = true,
                ClientSupportsHdr = null,
                CanTranscode = true,
                PipelineSupported = true
            }
        };

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Available.Should().BeTrue();
        result.Enabled.Should().BeFalse();
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
        return fixture;
    }
    );
}
