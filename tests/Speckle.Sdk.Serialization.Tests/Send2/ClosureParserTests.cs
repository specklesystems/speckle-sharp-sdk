using AwesomeAssertions;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Serialization.Tests;

public class ClosureParserTests
{
  [Fact]
  public void GetClosures_WithValidJson_ReturnsCorrectClosures()
  {
    // Arrange
    var json = @"{""__closure"": {""id1"": 2, ""id2"": 1, ""id3"": 3}}";

    // Act
    var result = ClosureParser.GetClosures(json, CancellationToken.None);

    // Assert
    result.Should().HaveCount(3);
    result.Should().Contain((closure) => closure.Item1 == "id1" && closure.Item2 == 2);
    result.Should().Contain((closure) => closure.Item1 == "id2" && closure.Item2 == 1);
    result.Should().Contain((closure) => closure.Item1 == "id3" && closure.Item2 == 3);
  }

  [Fact]
  public void GetClosures_WithValidJson_ReturnsUnsorted()
  {
    // Arrange
    var json = @"{""__closure"": {""id1"": 2, ""id2"": 1, ""id3"": 3}}";

    // Act
    var result = ClosureParser.GetClosures(json, CancellationToken.None);

    // Assert
    result.Should().HaveCount(3);
    result[0].Item2.Should().Be(2);
    result[1].Item2.Should().Be(1);
    result[2].Item2.Should().Be(3);
  }

  [Fact]
  public void GetClosures_WithValidJson_ReturnsSortedByDepthDescending()
  {
    // Arrange
    var json = @"{""__closure"": {""id1"": 2, ""id2"": 1, ""id3"": 3}}";

    // Act
    var result = ClosureParser.GetClosuresSorted(json, CancellationToken.None);

    // Assert
    result.Should().HaveCount(3);
    result[0].Item2.Should().Be(3);
    result[1].Item2.Should().Be(2);
    result[2].Item2.Should().Be(1);
  }

  [Fact]
  public void GetChildrenIds_WithValidJson_ReturnsCorrectIds()
  {
    // Arrange
    var json = @"{""__closure"": {""id1"": 2, ""id2"": 1, ""id3"": 3}}";

    // Act
    var result = ClosureParser.GetChildrenIds(json, CancellationToken.None).ToList();

    // Assert
    result.Should().HaveCount(3);
    result.Should().Contain("id1");
    result.Should().Contain("id2");
    result.Should().Contain("id3");
  }

  [Fact]
  public void GetClosures_WithRandomOrderedClosures_ReturnsSortedByDepthDescending()
  {
    // Arrange
    var random = new Random(42); // Fixed seed for reproducibility
    var idDepthPairs = new List<(string id, int depth)>
    {
      ("id1", 5),
      ("id2", 3),
      ("id3", 7),
      ("id4", 1),
      ("id5", 10),
      ("id6", 2),
    };

    // Randomize the order
    var randomized = idDepthPairs.OrderBy(_ => random.Next()).ToList();

    // Build JSON with randomized order
    using var stringWriter = new StringWriter();
    using var jsonWriter = new JsonTextWriter(stringWriter);

    jsonWriter.WriteStartObject();
    jsonWriter.WritePropertyName("__closure");
    jsonWriter.WriteStartObject();

    foreach (var pair in randomized)
    {
      jsonWriter.WritePropertyName(pair.id);
      jsonWriter.WriteValue(pair.depth);
    }

    jsonWriter.WriteEndObject();
    jsonWriter.WriteEndObject();

    var json = stringWriter.ToString();

    // Act
    var result = ClosureParser.GetClosuresSorted(json, CancellationToken.None);

    // Assert
    result.Should().HaveCount(6);

    // Verify sorting is correct (descending by depth)
    for (int i = 0; i < result.Count - 1; i++)
    {
      result[i].Item2.Should().BeGreaterThanOrEqualTo(result[i + 1].Item2);
    }

    // Verify specific order
    result[0].Item1.Should().Be("id5"); // depth 10
    result[1].Item1.Should().Be("id3"); // depth 7
    result[2].Item1.Should().Be("id1"); // depth 5
    result[3].Item1.Should().Be("id2"); // depth 3
    result[4].Item1.Should().Be("id6"); // depth 2
    result[5].Item1.Should().Be("id4"); // depth 1
  }

  [Fact]
  public void GetClosures_WithEmptyJson_ReturnsEmptyList()
  {
    // Arrange
    var json = "{}";

    // Act
    var result = ClosureParser.GetClosures(json, CancellationToken.None);

    // Assert
    result.Should().BeEmpty();
  }

  [Fact]
  public void GetClosures_WithInvalidJson_ReturnsEmptyList()
  {
    // Arrange
    var json = "invalid json";

    // Act
    var result = ClosureParser.GetClosures(json, CancellationToken.None);

    // Assert
    result.Should().BeEmpty();
  }

  [Fact]
  public void GetClosures_WithNullJson_ReturnsEmptyList()
  {
    // Arrange
    string json = null!;

    // Act
    var result = ClosureParser.GetClosures(json, CancellationToken.None);

    // Assert
    result.Should().BeEmpty();
  }

  [Fact]
  public void GetClosures_WithJsonReader_ReturnsCorrectClosures()
  {
    // Arrange
    var json = @"{""id1"": 2, ""id2"": 1, ""id3"": 3}";
    using var stringReader = new StringReader(json);
    using var jsonReader = new JsonTextReader(stringReader);

    // Act
    jsonReader.Read(); // Move to start object
    var result = ClosureParser.GetClosures(jsonReader, CancellationToken.None);

    // Assert
    result.Should().HaveCount(3);
    result.Should().Contain((closure) => closure.Item1 == "id1" && closure.Item2 == 2);
    result.Should().Contain((closure) => closure.Item1 == "id2" && closure.Item2 == 1);
    result.Should().Contain((closure) => closure.Item1 == "id3" && closure.Item2 == 3);
  }
}
