namespace TransmissionStreaming.Core.Tests.Projection;

using FluentAssertions;
using Xunit;
using ProjectionEntry = global::TransmissionStreaming.Core.Projection.Projection;

public sealed class ProjectionTests
{
    [Theory, AutoMoqData]
    public void Should_DescribeStateWithoutExposingPlaybackCapability(ProjectionEntry source)
    {
        // Act
        var display = source.ToString();

        // Assert
        display.Should().Contain(source.Id).And.Contain(source.Status);
        display.Contains(source.Secret, StringComparison.Ordinal).Should().BeFalse();
    }
}
