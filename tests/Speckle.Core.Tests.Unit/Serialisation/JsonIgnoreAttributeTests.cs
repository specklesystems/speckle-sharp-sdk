using NUnit.Framework;
using Speckle.Core.Models;
using Speckle.Core.Serialisation;
using Speckle.Newtonsoft.Json;

namespace Speckle.Core.Tests.Unit.Serialisation;

[TestOf(typeof(BaseObjectSerializerV2))]
public sealed class JsonIgnoreRespected
{
  [Test]
  public void IgnoredProperties_NotIncludedInJson()
  {
    IgnoreTest testData = new();

    BaseObjectSerializerV2 sut = new();

    var res = sut.Serialize(testData);

    Assert.That(res, Does.Contain(nameof(testData.ShouldBeIncluded)));
    Assert.That(res, Does.Contain(testData.ShouldBeIncluded));

    Assert.That(res, Does.Not.Contain(nameof(testData.ShouldBeIgnored)));
    Assert.That(res, Does.Not.Contain(testData.ShouldBeIgnored));
  }
}

public sealed class IgnoreTest : Base
{
  [JsonIgnore]
  public string ShouldBeIgnored => "this should have been ignored";

  public string ShouldBeIncluded => "this should have been included";
}
