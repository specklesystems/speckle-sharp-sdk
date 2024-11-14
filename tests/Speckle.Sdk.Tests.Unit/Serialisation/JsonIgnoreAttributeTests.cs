using Shouldly;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

/// <summary>
/// Tests that the <see cref="JsonIgnoreAttribute"/> leads to properties being ignored both from the final JSON output,
/// But also from the id calculation
/// </summary>
public sealed class JsonIgnoreRespected
{
  [Before(Class)]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(IgnoreTest).Assembly);
  }

  const string EXPECTED_PAYLOAD = "this should have been included";
  const string EXPECTED_HASH = "e1d9f0685266465c9bfe4e71f2eee6e9";
  public static IEnumerable<(string, string, string?)> IgnoredTestCases()
  {
    yield return ("this should have been ignored", EXPECTED_PAYLOAD,EXPECTED_HASH);
    yield return ("again, ignored!", EXPECTED_PAYLOAD,EXPECTED_HASH);
    yield return ("this one is not", EXPECTED_PAYLOAD,EXPECTED_HASH);
  }

  const string EXPECTED_PAYLOAD2 = "this should have been included";
  const string EXPECTED_HASH2 = "eeaeee4e61b04b313dd840cd63341eee";
  public static IEnumerable<(string, string, string?)> IgnoredCompoundTestCases()
  {
    yield return ("this should have been ignored", EXPECTED_PAYLOAD2,EXPECTED_HASH2);
    yield return ("again, ignored!", EXPECTED_PAYLOAD2,EXPECTED_HASH2);
    yield return ("this one is not", EXPECTED_PAYLOAD2,EXPECTED_HASH2);
  }

  [MethodDataSource(nameof(IgnoredTestCases))]
  public void IgnoredProperties_NotIncludedInJson(string ignoredPayload, string expectedPayload, string? ret)
  {
    IgnoreTest testData = new(ignoredPayload, expectedPayload);

    SpeckleObjectSerializer sut = new();

    var (json, id) = sut.SerializeBase(testData).NotNull();

    json.ShouldNotContain(nameof(testData.ShouldBeIgnored));
    json.ShouldNotContain(ignoredPayload);

    json.ShouldContain(nameof(testData.ShouldBeIncluded));
    json.ShouldContain(expectedPayload);
    
    id.ShouldBe(ret);
  }

  [MethodDataSource(nameof(IgnoredCompoundTestCases))]
  public void IgnoredProperties_Compound_NotIncludedInJson(string ignoredPayload, string expectedPayload, string? ret)
  {
    IgnoredCompoundTest testData = new(ignoredPayload, expectedPayload);

    MemoryTransport savedObjects = new();
    SpeckleObjectSerializer sut = new(writeTransports: [savedObjects]);

    var (json, id) = sut.SerializeBase(testData).NotNull();

    savedObjects.SaveObject(id.NotNull(), json);

    foreach ((_, string childJson) in savedObjects.Objects)
    {
      childJson.ShouldNotContain(nameof(testData.ShouldBeIgnored));
      childJson.ShouldNotContain(ignoredPayload);

      childJson.ShouldContain(nameof(testData.ShouldBeIncluded));
      childJson.ShouldContain(expectedPayload);
    }

    id.ShouldBe(ret);
  }
}

[SpeckleType("Speckle.Sdk.Test.Unit.Serialisation.IgnoredCompoundTest")]
public sealed class IgnoredCompoundTest(string ignoredPayload, string expectedPayload) : Base
{
  [JsonIgnore]
  public Base ShouldBeIgnored => new IgnoreTest(ignoredPayload, expectedPayload) { ["override"] = ignoredPayload };
  public Base ShouldBeIncluded => new IgnoreTest(ignoredPayload, expectedPayload);

  [JsonIgnore, DetachProperty]
  public Base ShouldBeIgnoredDetached => ShouldBeIgnored;

  [DetachProperty]
  public Base ShouldBeIncludedDetached => ShouldBeIncluded;

  [JsonIgnore]
  public List<Base> ShouldBeIgnoredList => [ShouldBeIgnored];

  [JsonIgnore, DetachProperty]
  public List<Base> ShouldBeIgnoredDetachedList => ShouldBeIgnoredList;

  public List<Base> ShouldBeIncludedList => [ShouldBeIncluded];

  [DetachProperty]
  public List<Base> ShouldBeIncludedDetachedList => ShouldBeIncludedList;
}

[SpeckleType("Speckle.Sdk.Tests.Unit.Serialisation.IgnoreTest")]
public sealed class IgnoreTest(string shouldBeIgnoredPayload, string shouldBeIncludedPayload) : Base
{
  [JsonIgnore]
  public string ShouldBeIgnored => shouldBeIgnoredPayload;

  public string ShouldBeIncluded => shouldBeIncludedPayload;
}
