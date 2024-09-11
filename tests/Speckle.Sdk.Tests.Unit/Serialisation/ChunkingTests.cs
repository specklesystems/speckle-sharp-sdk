using NUnit.Framework;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class ChunkingTests
{
  public static IEnumerable<TestCaseData> TestCases()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(IgnoreTest).Assembly);

    yield return new TestCaseData(CreateDynamicTestCase(10, 100)).Returns(10);
    yield return new TestCaseData(CreateDynamicTestCase(0.5, 100)).Returns(1);
    yield return new TestCaseData(CreateDynamicTestCase(20.5, 100)).Returns(21);

    yield return new TestCaseData(CreateDynamicTestCase(10, 1000)).Returns(10);
    yield return new TestCaseData(CreateDynamicTestCase(0.5, 1000)).Returns(1);
    yield return new TestCaseData(CreateDynamicTestCase(20.5, 1000)).Returns(21);
  }

  [TestCaseSource(nameof(TestCases))]
  public int ChunkSerializationTest(Base testCase)
  {
    MemoryTransport transport = new();
    var sut = new SpeckleObjectSerializer([transport]);

    _ = sut.Serialize(testCase);

    var serailizedObjects = transport
      .Objects.Values.Select(json => JsonConvert.DeserializeObject<Dictionary<string, object?>>(json))
      .ToList();

    int numberOfChunks = serailizedObjects.Count(x =>
      x!.TryGetValue("speckle_type", out var speckleType) && ((string)speckleType!) == "DataChunk"
    );

    return numberOfChunks;
  }

  private static Base CreateDynamicTestCase(double numberOfChunks, int chunkSize)
  {
    List<int> value = Enumerable.Range(0, (int)Math.Floor(chunkSize * numberOfChunks)).ToList();
    return new Base { [$"@({chunkSize})chunkedProperty"] = value, };
  }
}
