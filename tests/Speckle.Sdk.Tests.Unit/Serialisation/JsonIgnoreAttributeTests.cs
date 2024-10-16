﻿using NUnit.Framework;
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
[TestOf(typeof(SpeckleObjectSerializer))]
public sealed class JsonIgnoreRespected
{
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(IgnoreTest).Assembly);
  }

  public static IEnumerable<TestCaseData> IgnoredTestCases()
  {
    const string EXPECTED_PAYLOAD = "this should have been included";
    const string EXPECTED_HASH = "e1d9f0685266465c9bfe4e71f2eee6e9";
    yield return new TestCaseData("this should have been ignored", EXPECTED_PAYLOAD).Returns(EXPECTED_HASH);
    yield return new TestCaseData("again, ignored!", EXPECTED_PAYLOAD).Returns(EXPECTED_HASH);
    yield return new TestCaseData("this one is not", EXPECTED_PAYLOAD).Returns(EXPECTED_HASH);
  }

  public static IEnumerable<TestCaseData> IgnoredCompoundTestCases()
  {
    const string EXPECTED_PAYLOAD = "this should have been included";
    const string EXPECTED_HASH = "eeaeee4e61b04b313dd840cd63341eee";
    yield return new TestCaseData("this should have been ignored", EXPECTED_PAYLOAD).Returns(EXPECTED_HASH);
    yield return new TestCaseData("again, ignored!", EXPECTED_PAYLOAD).Returns(EXPECTED_HASH);
    yield return new TestCaseData("this one is not", EXPECTED_PAYLOAD).Returns(EXPECTED_HASH);
  }

  [TestCaseSource(nameof(IgnoredTestCases))]
  public string? IgnoredProperties_NotIncludedInJson(string ignoredPayload, string expectedPayload)
  {
    IgnoreTest testData = new(ignoredPayload, expectedPayload);

    SpeckleObjectSerializer sut = new();

    var (json, id) = sut.SerializeBase(testData).NotNull();

    Assert.That(json, Does.Not.Contain(nameof(testData.ShouldBeIgnored)));
    Assert.That(json, Does.Not.Contain(ignoredPayload));

    Assert.That(json, Does.Contain(nameof(testData.ShouldBeIncluded)));
    Assert.That(json, Does.Contain(expectedPayload));

    return id;
  }

  [TestCaseSource(nameof(IgnoredCompoundTestCases))]
  public string? IgnoredProperties_Compound_NotIncludedInJson(string ignoredPayload, string expectedPayload)
  {
    IgnoredCompoundTest testData = new(ignoredPayload, expectedPayload);

    MemoryTransport savedObjects = new();
    SpeckleObjectSerializer sut = new(writeTransports: [savedObjects]);

    var (json, id) = sut.SerializeBase(testData).NotNull();

    savedObjects.SaveObject(id.NotNull(), json);

    foreach ((_, string childJson) in savedObjects.Objects)
    {
      Assert.That(childJson, Does.Not.Contain(nameof(testData.ShouldBeIgnored)));
      Assert.That(childJson, Does.Not.Contain(ignoredPayload));

      Assert.That(childJson, Does.Contain(nameof(testData.ShouldBeIncluded)));
      Assert.That(childJson, Does.Contain(expectedPayload));
    }

    return id;
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
