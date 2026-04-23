using AwesomeAssertions;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Send;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Send.Serialization;

/// <summary>
/// Tests that the <see cref="JsonIgnoreAttribute"/> leads to properties being ignored both from the final JSON output,
/// But also from the id calculation
/// </summary>
[Collection(nameof(RequiresTypeLoaderCollection))]
public sealed class JsonIgnoreRespected
{
  private readonly Serializer _sut = new();

  public JsonIgnoreRespected()
  {
    TypeLoader.ReInitialize(typeof(Base).Assembly, typeof(IgnoreTest).Assembly);
  }

  public static IEnumerable<object[]> IgnoredTestCases()
  {
    const string EXPECTED_PAYLOAD = "this should have been included";
    const string EXPECTED_HASH = "c24fe8fad993b1f500e65315e9284afd";
    yield return ["this should have been ignored", EXPECTED_PAYLOAD, EXPECTED_HASH];
    yield return ["again, ignored!", EXPECTED_PAYLOAD, EXPECTED_HASH];
    yield return ["this one is not", EXPECTED_PAYLOAD, EXPECTED_HASH];
  }

  public static IEnumerable<object[]> IgnoredCompoundTestCases()
  {
    const string EXPECTED_PAYLOAD = "this should have been included";
    const string EXPECTED_HASH = "aa78478569795e0ed8df656e792a4ee8";
    yield return ["this should have been ignored", EXPECTED_PAYLOAD, EXPECTED_HASH];
    yield return ["again, ignored!", EXPECTED_PAYLOAD, EXPECTED_HASH];
    yield return ["this one is not", EXPECTED_PAYLOAD, EXPECTED_HASH];
  }

  [Theory]
  [MemberData(nameof(IgnoredTestCases))]
  public void IgnoredProperties_NotIncludedInJson(string ignoredPayload, string expectedPayload, string expectedHash)
  {
    IgnoreTest testData = new(ignoredPayload, expectedPayload);

    UploadItem result = _sut.Serialize(testData).ToArray().First();
    result.SpeckleType.Should().Be(testData.speckle_type);

    string jsonString = result.Json.ToJsonString();
    jsonString.Should().NotContain(nameof(testData.ShouldBeIgnored));
    jsonString.Should().NotContain(ignoredPayload);

    jsonString.Should().Contain(nameof(testData.ShouldBeIncluded));
    jsonString.Should().Contain(expectedPayload);

    result.Id.Should().Be(expectedHash);
  }

  [Theory]
  [MemberData(nameof(IgnoredCompoundTestCases))]
  public void IgnoredProperties_Compound_NotIncludedInJson(
    string ignoredPayload,
    string expectedPayload,
    string expectedHash
  )
  {
    IgnoredCompoundTest testData = new(ignoredPayload, expectedPayload);

    UploadItem[] results = _sut.Serialize(testData).ToArray();
    UploadItem result = results[0];
    result.SpeckleType.Should().Be(testData.speckle_type);
    result.Reference.closure.Should().NotBeNull();

    foreach (UploadItem child in results)
    {
      string jsonString = child.Json.ToJsonString();
      jsonString.Should().NotContain(nameof(testData.ShouldBeIgnored));
      jsonString.Should().NotContain(ignoredPayload);

      jsonString.Should().Contain(nameof(testData.ShouldBeIncluded));
      jsonString.Should().Contain(expectedPayload);
    }

    result.Id.Should().Be(expectedHash);
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
