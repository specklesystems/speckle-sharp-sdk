﻿using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Transports;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class ChunkingTests
{
  public static IEnumerable<object[]> TestCases()
  {
    // Initialize type loader
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(IgnoreTest).Assembly);

    // Return test data as a collection of objects for xUnit
    yield return new object[] { CreateDynamicTestCase(10, 100), 10 };
    yield return new object[] { CreateDynamicTestCase(0.5, 100), 1 };
    yield return new object[] { CreateDynamicTestCase(20.5, 100), 21 };

    yield return new object[] { CreateDynamicTestCase(10, 1000), 10 };
    yield return new object[] { CreateDynamicTestCase(0.5, 1000), 1 };
    yield return new object[] { CreateDynamicTestCase(20.5, 1000), 21 };
  }

  [Theory]
  [MemberData(nameof(TestCases))]
  public void ChunkSerializationTest(Base testCase, int expectedChunkCount)
  {
    // Arrange
    var transport = new MemoryTransport();
    var sut = new SpeckleObjectSerializer(new[] { transport });

    // Act
    _ = sut.Serialize(testCase);
    var serializedObjects = transport
      .Objects.Values.Select(json => JsonConvert.DeserializeObject<Dictionary<string, object?>>(json))
      .ToList();

    var numberOfChunks = serializedObjects.Count(x =>
      x!.TryGetValue("speckle_type", out var speckleType) && ((string)speckleType!) == "Speckle.Core.Models.DataChunk"
    );

    // Assert using Shouldly
    numberOfChunks.ShouldBe(expectedChunkCount);
  }

  private static Base CreateDynamicTestCase(double numberOfChunks, int chunkSize)
  {
    // Helper method to create the dynamic test case
    var value = Enumerable.Range(0, (int)Math.Floor(chunkSize * numberOfChunks)).ToList();
    return new Base { [$"@({chunkSize})chunkedProperty"] = value };
  }
}
