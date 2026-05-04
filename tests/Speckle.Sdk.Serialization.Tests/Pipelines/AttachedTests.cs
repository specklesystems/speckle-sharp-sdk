using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Send;

namespace Speckle.Sdk.Serialization.Tests.Pipelines;

public sealed class AttachedTests
{
  private readonly Serializer _sut;

  public AttachedTests()
  {
    TypeLoader.ReInitialize(typeof(TestClass).Assembly, typeof(Polyline).Assembly);

    _sut = new();
  }

  [Fact]
  public void ExpectAttachedIdsToMatchBase()
  {
    string seed = Guid.NewGuid().ToString();
    Base b0 = new() { ["data"] = seed };
    UploadItem l0 = _sut.Serialize(b0).First();

    Base b1 = new() { ["data"] = b0 };
    UploadItem l1 = _sut.Serialize(b1).First();
    string expectedLine1 = $"\"id\":\"{l0.Id}\"";
    Assert.Contains(expectedLine1, l1.Json.ToJsonString());
    Assert.Contains(seed, l1.Json.ToJsonString());

    Base b2 = new() { ["data"] = b1 };
    UploadItem l2 = _sut.Serialize(b2).First();
    string expectedLine2 = $"\"id\":\"{l1.Id}\"";
    Assert.Contains(expectedLine2, l2.Json.ToJsonString());
    Assert.Contains(expectedLine2, l1.Json.ToJsonString());
  }
}
