namespace TransmissionStreaming.Core.Tests.Configuration;

using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using Xunit;

public sealed class ServiceConnectionTests
{
    [Theory, AutoMoqData]
    public void Should_KeepServiceCredentialsOutOfDiagnosticText(ServiceConnection source)
    {
        // Arrange
        var credentials = new[] { source.Username, source.Password, source.ApiKey };

        // Act
        var display = source.ToString();

        // Assert
        credentials.Any(value => display.Contains(value, StringComparison.Ordinal)).Should().BeFalse();
    }
}
