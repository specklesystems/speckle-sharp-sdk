using System.Collections.Generic;
using FluentAssertions;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Serialisation
{
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
      yield return new object[] { "this should have been ignored", EXPECTED_PAYLOAD, EXPECTED_HASH };
      yield return new object[] { "again, ignored!", EXPECTED_PAYLOAD, EXPECTED_HASH };
      yield return new object[] { "this one is not", EXPECTED_PAYLOAD, EXPECTED_HASH };
    }

    public static IEnumerable<object[]> IgnoredCompoundTestCases()
    {
      const string EXPECTED_PAYLOAD = "this should have been included";
      const string EXPECTED_HASH = "eeaeee4e61b04b313dd840cd63341eee";
      yield return new object[] { "this should have been ignored", EXPECTED_PAYLOAD, EXPECTED_HASH };
      yield return new object[] { "again, ignored!", EXPECTED_PAYLOAD, EXPECTED_HASH };
      yield return new object[] { "this one is not", EXPECTED_PAYLOAD, EXPECTED_HASH };
    }

    [Theory]
    [MemberData(nameof(IgnoredTestCases))]
    public void IgnoredProperties_NotIncludedInJson(string ignoredPayload, string expectedPayload, string expectedHash)
    {
      IgnoreTest testData = new(ignoredPayload, expectedPayload);

      SpeckleObjectSerializer sut = new();

      var result = sut.SerializeBase(testData);
      result.Should().NotBeNull();
      result!.Value.Id.Should().NotBeNull();

      var jsonString = result.Value.Json.ToString();
      jsonString.Should().NotContain(nameof(testData.ShouldBeIgnored));
      jsonString.Should().NotContain(ignoredPayload);

      jsonString.Should().Contain(nameof(testData.ShouldBeIncluded));
      jsonString.Should().Contain(expectedPayload);

      result.Value.Id!.Value.Value.Should().Be(expectedHash);
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

      MemoryTransport savedObjects = new();
      SpeckleObjectSerializer sut = new(writeTransports: new[] { savedObjects });

      var result = sut.SerializeBase(testData);
      var (json, id) = result.NotNull();
      json.Value.Should().NotBeNull();
      id.Should().NotBeNull();

      savedObjects.SaveObject(id!.Value.Value.NotNull(), json.Value);

      foreach ((_, string childJson) in savedObjects.Objects)
      {
        childJson.Should().NotContain(nameof(testData.ShouldBeIgnored));
        childJson.Should().NotContain(ignoredPayload);

        childJson.Should().Contain(nameof(testData.ShouldBeIncluded));
        childJson.Should().Contain(expectedPayload);
      }

      id.Value.Value.Should().Be(expectedHash);
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
    public List<Base> ShouldBeIgnoredList => new List<Base> { ShouldBeIgnored };

    [JsonIgnore, DetachProperty]
    public List<Base> ShouldBeIgnoredDetachedList => ShouldBeIgnoredList;

    public List<Base> ShouldBeIncludedList => new List<Base> { ShouldBeIncluded };

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
}
