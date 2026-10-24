namespace TransmissionStreaming.Core.Tests.Arr;

using FluentAssertions;
using global::TransmissionStreaming.Core.Arr;
using Xunit;

public sealed class ArrConnectionTests
{
    [Theory, AutoMoqData]
    public void Should_KeepCredentialsOutOfDiagnosticText(ArrConnection source)
    {
        // Act
        var display = source.ToString();

        // Assert
        display.Contains(source.ApiKey, StringComparison.Ordinal).Should().BeFalse();
    }
}
