using NUnit.Framework;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

[TestOf(typeof(SpeckleObjectSerializer))]
public sealed class JsonIgnoreRespected
{
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(IgnoreTest).Assembly);
  }

  [Test]
  public async Task IgnoredProperties_NotIncludedInJson()
  {
    IgnoreTest testData = new();

    SpeckleObjectSerializer sut = new();

    var res = await sut.SerializeAsync(testData);

    Assert.That(res, Does.Contain(nameof(testData.ShouldBeIncluded)));
    Assert.That(res, Does.Contain(testData.ShouldBeIncluded));

    Assert.That(res, Does.Not.Contain(nameof(testData.ShouldBeIgnored)));
    Assert.That(res, Does.Not.Contain(testData.ShouldBeIgnored));
  }
}

[SpeckleType("Speckle.Sdk.Tests.Unit.Serialisation.IgnoreTest")]
public sealed class IgnoreTest : Base
{
  [JsonIgnore]
  public string ShouldBeIgnored => "this should have been ignored";

  public string ShouldBeIncluded => "this should have been included";
}
