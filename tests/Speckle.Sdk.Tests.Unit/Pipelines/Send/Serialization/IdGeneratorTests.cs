using System.Text;
using Speckle.Sdk.Pipelines.Send;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Send.Serialization;

public class IdGeneratorTests
{
  [Theory]
  [InlineData("hello world", "b94d27b9934d3e08a52e52d7da7dabfa")]
  [InlineData("test input", "9dfe6f15d1ab73af898739394fd22fd7")]
  [InlineData("", "e3b0c44298fc1c149afbf4c8996fb924")]
  [InlineData("hjlkasdfhkjladfkjhlasdflkhjasdflkhjasfdlhkjasfdlhjkafsdhlkfjads", "68bcfbbbd4cf06adf294e3e8d68b856c")]
  public void ComputeId_BothOverloads_ReturnExpectedHashes(string input, string expectedHash)
  {
    // Arrange
    byte[] bytes = Encoding.UTF8.GetBytes(input);

    // Act
    string spanResult = IdGenerator.ComputeId(bytes);
    string arrayResult = IdGenerator.ComputeId(bytes, 0, bytes.Length);

    // Assert
    Assert.Equal(expectedHash, spanResult);
    Assert.Equal(expectedHash, arrayResult);
    Assert.Equal(32, expectedHash.Length);
    Assert.Equal(32, IdGenerator.ID_HEX_LENGTH_CHARS);
  }
}
