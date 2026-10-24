namespace TransmissionStreaming.Core.Tests.Configuration;

using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using Xunit;

public sealed class StreamingOptionsTests
{
    [Theory, AutoMoqData]
    public void Should_KeepAllServiceCredentialsOutOfDiagnosticText(StreamingOptions source)
    {
        // Act
        var display = source.ToString();

        // Assert
        display.Contains(source.TransmissionPassword, StringComparison.Ordinal).Should().BeFalse();
        display.Contains(source.RadarrApiKey, StringComparison.Ordinal).Should().BeFalse();
        display.Contains(source.SonarrApiKey, StringComparison.Ordinal).Should().BeFalse();
    }
}
