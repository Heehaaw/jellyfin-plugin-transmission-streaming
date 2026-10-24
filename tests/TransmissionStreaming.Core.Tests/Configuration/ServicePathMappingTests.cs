namespace TransmissionStreaming.Core.Tests.Configuration;

using FluentAssertions;
using global::TransmissionStreaming.Core.Configuration;
using System.Xml.Serialization;
using Xunit;

public sealed class ServicePathMappingTests
{
    [Theory, AutoMoqData]
    public void Should_RoundTripThroughXmlConfiguration(ServicePathMapping source)
    {
        // Arrange
        var serializer = new XmlSerializer(typeof(ServicePathMapping));
        using var text = new StringWriter();

        // Act
        serializer.Serialize(text, source);
        using var reader = new StringReader(text.ToString());
        var restored = (ServicePathMapping)serializer.Deserialize(reader)!;

        // Assert
        restored.Should().BeEquivalentTo(source);
    }
}
