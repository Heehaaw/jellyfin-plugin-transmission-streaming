namespace Jellyfin.Plugin.TransmissionStreaming.Tests.Library;

using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Xunit2;
using FluentAssertions;
using Jellyfin.Plugin.TransmissionStreaming.Library;
using Xunit;

public sealed class ProjectionOverviewTests
{
    [Theory, AutoMoqData]
    internal void Should_AppendTheActivityLinkBelowTheSynopsis(string source, ProjectionOverview sut)
    {
        // Arrange
        var publicUrl = "https://jellyfin.invalid/base";
        var expectedAddress = publicUrl + "/web/#/configurationpage?name=transmission-streaming";

        // Act
        var result = sut.Format(source, publicUrl);

        // Assert
        result.Should().StartWith(source);
        result.Should().Contain("href=\"" + expectedAddress + "\"");
        result.Should().Contain("View download status</a>");
    }

    [Theory, AutoMoqData]
    internal void Should_LeaveRepeatedReconciliationUnchanged(string source, ProjectionOverview sut)
    {
        // Arrange
        var publicUrl = "https://jellyfin.invalid/base/";
        var first = sut.Format(source, publicUrl);

        // Act
        var repeated = sut.Format(first, publicUrl);

        // Assert
        repeated.Should().Be(first);
    }

    [Theory, AutoMoqData]
    internal void Should_RefreshOnlyItsOwnLinkAndPreserveOtherContent(string source, string annotation, ProjectionOverview sut)
    {
        // Arrange
        var oldOrigin = "https://old-jellyfin.invalid";
        var newOrigin = "https://new-jellyfin.invalid/base";
        var userLink = "<a href=\"https://example.invalid\">" + source + "</a>";
        var synopsis = source + userLink;
        var previous = sut.Format(synopsis, oldOrigin) + annotation;

        // Act
        var result = sut.Format(previous, newOrigin);

        // Assert
        result.Should().StartWith(synopsis + annotation);
        result.Should().Contain(userLink);
        result.Should().Contain(newOrigin);
        result.Should().NotContain(oldOrigin);
    }

    [Theory, AutoMoqData]
    internal void Should_PreserveTheSynopsisWhenTheOriginIsUnavailableOrUnsafe(string source, ProjectionOverview sut)
    {
        // Arrange
        string?[] origins =
            [null, "", "javascript:alert(1)", "https://user:password@jellyfin.invalid", "https://jellyfin.invalid/?unexpected=true", "https://jellyfin.invalid/#unexpected"];

        // Act
        var results = origins.Select(origin => sut.Format(source, origin)).ToArray();

        // Assert
        results.Should().OnlyContain(result => result == source);
    }

    [Theory, AutoMoqData]
    internal void Should_AddTheLinkWhenThereIsNoSynopsis(ProjectionOverview sut)
    {
        // Arrange
        string? synopsis = null;
        var publicUrl = "https://jellyfin.invalid";

        // Act
        var result = sut.Format(synopsis, publicUrl);

        // Assert
        result.Should().Contain("View download status</a>");
        result.Should().Contain(publicUrl);
    }

    private sealed class AutoMoqDataAttribute() : AutoDataAttribute(() =>
    {
        var fixture = new Fixture { RepeatCount = 1 };
        fixture.Customize(new AutoMoqCustomization { ConfigureMembers = true, GenerateDelegates = true });
        return fixture;
    });
}
