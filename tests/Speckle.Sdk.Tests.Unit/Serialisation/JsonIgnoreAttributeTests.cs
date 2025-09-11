using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

/// <summary>
/// Tests that the <see cref="JsonIgnoreAttribute"/> leads to properties being ignored both from the final JSON output,
/// But also from the id calculation
/// </summary>
public sealed class JsonIgnoreRespected
{
  public JsonIgnoreRespected()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(IgnoreTest).Assembly);
  }

  public static IEnumerable<object[]> IgnoredTestCases()
  {
    const string EXPECTED_PAYLOAD = "this should have been included";
    const string EXPECTED_HASH = "e1d9f0685266465c9bfe4e71f2eee6e9";
    yield return ["this should have been ignored", EXPECTED_PAYLOAD, EXPECTED_HASH];
    yield return ["again, ignored!", EXPECTED_PAYLOAD, EXPECTED_HASH];
    yield return ["this one is not", EXPECTED_PAYLOAD, EXPECTED_HASH];
  }

  public static IEnumerable<object[]> IgnoredCompoundTestCases()
  {
    const string EXPECTED_PAYLOAD = "this should have been included";
    const string EXPECTED_HASH = "eeaeee4e61b04b313dd840cd63341eee";
    yield return ["this should have been ignored", EXPECTED_PAYLOAD, EXPECTED_HASH];
    yield return ["again, ignored!", EXPECTED_PAYLOAD, EXPECTED_HASH];
    yield return ["this one is not", EXPECTED_PAYLOAD, EXPECTED_HASH];
  }

  [SpeckleType("Speckle.Sdk.Test.Unit.Serialisation.IgnoredCompoundTest")]
  public sealed class IgnoredCompoundTest(string ignoredPayload, string expectedPayload) : Base
  {
    [JsonIgnore]
    public Base ShouldBeIgnored => new IgnoreTest(ignoredPayload, expectedPayload) { ["override"] = ignoredPayload };

    public Base ShouldBeIncluded => new IgnoreTest(ignoredPayload, expectedPayload);

    [JsonIgnore, DetachProperty] public Base ShouldBeIgnoredDetached => ShouldBeIgnored;

    [DetachProperty] public Base ShouldBeIncludedDetached => ShouldBeIncluded;

    [JsonIgnore] public List<Base> ShouldBeIgnoredList => [ShouldBeIgnored];

    [JsonIgnore, DetachProperty] public List<Base> ShouldBeIgnoredDetachedList => ShouldBeIgnoredList;

    public List<Base> ShouldBeIncludedList => [ShouldBeIncluded];

    [DetachProperty] public List<Base> ShouldBeIncludedDetachedList => ShouldBeIncludedList;
  }
}

[SpeckleType("Speckle.Sdk.Tests.Unit.Serialisation.IgnoreTest")]
public sealed class IgnoreTest(string shouldBeIgnoredPayload, string shouldBeIncludedPayload) : Base
{
  [JsonIgnore]
  public string ShouldBeIgnored => shouldBeIgnoredPayload;

  public string ShouldBeIncluded => shouldBeIncludedPayload;
}
