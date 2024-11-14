using Shouldly;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class ChunkingTests
{
  public static IEnumerable<(Base, int)> TestCases()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(IgnoreTest).Assembly);

    yield return (CreateDynamicTestCase(10, 100),10);
    yield return (CreateDynamicTestCase(0.5, 100),1);
    yield return (CreateDynamicTestCase(20.5, 100),21);

    yield return (CreateDynamicTestCase(10, 1000),10);
    yield return (CreateDynamicTestCase(0.5, 1000),1);
    yield return (CreateDynamicTestCase(20.5, 1000),21);
  }

  [MethodDataSource(nameof(TestCases))]
  public void ChunkSerializationTest(Base testCase, int ret)
  {
    MemoryTransport transport = new();
    var sut = new SpeckleObjectSerializer([transport]);

    _ = sut.Serialize(testCase);

    var serailizedObjects = transport
      .Objects.Values.Select(json => JsonConvert.DeserializeObject<Dictionary<string, object?>>(json))
      .ToList();

    int numberOfChunks = serailizedObjects.Count(x =>
      x!.TryGetValue("speckle_type", out var speckleType) && ((string)speckleType!) == "Speckle.Core.Models.DataChunk"
    );

     numberOfChunks.ShouldBe(ret);
  }

  private static Base CreateDynamicTestCase(double numberOfChunks, int chunkSize)
  {
    List<int> value = Enumerable.Range(0, (int)Math.Floor(chunkSize * numberOfChunks)).ToList();
    return new Base { [$"@({chunkSize})chunkedProperty"] = value };
  }
}
