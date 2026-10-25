namespace TransmissionStreaming.Infrastructure.Tests.Arr.Transport;

using FluentAssertions;
using global::TransmissionStreaming.Infrastructure.Arr.Transport;
using System.Text.Json;
using Xunit;

public sealed class ArrJsonTests
{
    [Theory, AutoMoqData]
    internal void Should_ReadCaseInsensitiveImportData(JsonRecord<string> source)
    {
        // Arrange
        var element = Serialize(source);
        var property = source.Data.Single();

        // Act
        var text = ArrJson.Data(element, property.Key.ToUpperInvariant());

        // Assert
        text.Should().Be(property.Value);
    }

    [Theory, AutoMoqData]
    internal void Should_AcceptNumericImportData(JsonRecord<long> source)
    {
        // Arrange
        var element = Serialize(source);
        var property = source.Data.Single();

        // Act
        var text = ArrJson.Data(element, property.Key);

        // Assert
        text.Should().Be(property.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory, AutoMoqData]
    internal void Should_DefaultMissingOrWrongJsonShapes(JsonRecord<string> source)
    {
        // Arrange
        var element = Serialize(source);
        var scalar = ArrJson.Object(element, "name");

        // Act
        var nonArray = ArrJson.Array(element).ToArray();
        var wrongNumber = ArrJson.Number(element, "name");
        var wrongText = ArrJson.Text(element, "id");
        var missingData = ArrJson.Data(scalar, source.Name);

        // Assert
        nonArray.Should().BeEmpty();
        wrongNumber.Should().Be(0);
        wrongText.Should().BeEmpty();
        missingData.Should().BeEmpty();
    }

    [Theory, AutoMoqData]
    internal void Should_PreserveWideFileSizes(JsonRecord<string> source)
    {
        // Arrange
        source.Size += int.MaxValue;
        var element = Serialize(source);

        // Act
        var size = ArrJson.Long(element, "size");
        var tooWideForId = ArrJson.Number(element, "size");

        // Assert
        size.Should().Be(source.Size);
        tooWideForId.Should().Be(0);
    }

    internal sealed record JsonRecord<T>(string Name, int Id, Dictionary<string, T> Data)
    {
        public long Size { get; set; }
    }

    private static JsonElement Serialize<T>(T source)
    {
        return JsonSerializer.SerializeToElement(source, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
