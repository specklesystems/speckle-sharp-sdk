using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.V2.Send;
using Speckle.Sdk.Testing;

namespace Speckle.Objects.SerializationTests;

public sealed class ObjectsSerializationTest
{
  [Theory]
  [MemberData(nameof(ObjectsTestData.TheoryData), MemberType = typeof(ObjectsTestData))]
  public async Task SerializeAndVerify(Base testCase)
  {
    var serialized = Serialize(testCase);

    await VerifySerialized(serialized).UseParameters(testCase);
  }

  private static IReadOnlyList<(Id, Json, Dictionary<Id, int>)> Serialize(Base data)
  {
    using var serializer = new ObjectSerializerFactory(new BasePropertyGatherer()).Create(
      new Dictionary<Id, NodeInfo>(),
      default
    );
    return serializer.Serialize(data).ToList();
  }

  private static SettingsTask VerifySerialized(IReadOnlyList<(Id, Json, Dictionary<Id, int>)> serializedResult)
  {
    var jsons = serializedResult.OrderBy(x => x.Item1.Value).Select(x => x.Item2).ToArray();
    return SpeckleVerify.VerifyJsons(jsons);
  }
}
