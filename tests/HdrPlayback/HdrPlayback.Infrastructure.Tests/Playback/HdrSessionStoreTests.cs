namespace HdrPlayback.Infrastructure.Tests.Playback;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Xunit;
using HdrPlayback.Core.Playback;
using HdrPlayback.Infrastructure.Playback;
using Microsoft.Extensions.Time.Testing;

public sealed class HdrSessionStoreTests
{
    [Theory, AutoMoqData]
    internal void Should_IsolatePlaybackSessions(HdrSessionKey source, HdrSessionStore sut)
    {
        // Arrange
        sut.Enable(source);
        var other = source with
        {
            PlaySessionId = source.PlaySessionId + "-other"
        };

        // Act
        var selected = sut.Contains(source);
        var unrelated = sut.Contains(other);

        // Assert
        selected.Should().BeTrue();
        unrelated.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_ExpireAbandonedSessions(HdrSessionKey source, [Frozen] FakeTimeProvider clock, HdrSessionStore sut)
    {
        // Arrange
        sut.Enable(source);
        clock.Advance(TimeSpan.FromHours(7));

        // Act
        var result = sut.Contains(source);

        // Assert
        result.Should().BeFalse();
    }

    [Theory, AutoMoqData]
    internal void Should_RetainActiveSessionsAcrossLongPlayback(HdrSessionKey source, [Frozen] FakeTimeProvider clock, HdrSessionStore sut)
    {
        // Arrange
        sut.Enable(source);
        clock.Advance(TimeSpan.FromHours(5));
        _ = sut.Contains(source);
        clock.Advance(TimeSpan.FromHours(5));

        // Act
        var result = sut.Contains(source);

        // Assert
        result.Should().BeTrue();
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
        var clock = fixture.Freeze<FakeTimeProvider>();
        fixture.Register<TimeProvider>(() => clock);
        return fixture;
    }
    );
}
