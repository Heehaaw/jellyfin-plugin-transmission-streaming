namespace TransmissionStreaming.Core.Tests.Transmission;

using FluentAssertions;
using global::TransmissionStreaming.Core.Transmission;
using Xunit;

public sealed class TransmissionConnectionTests
{
    [Theory, AutoMoqData]
    public void Should_KeepCredentialsOutOfDiagnosticText(TransmissionConnection source)
    {
        // Act
        var display = source.ToString();

        // Assert
        display.Contains(source.Password, StringComparison.Ordinal).Should().BeFalse();
        display.Contains(source.Username, StringComparison.Ordinal).Should().BeFalse();
    }
}
