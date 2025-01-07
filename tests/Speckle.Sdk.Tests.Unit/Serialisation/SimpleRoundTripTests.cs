﻿using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Api;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;
using Xunit;

namespace Speckle.Sdk.Tests.Unit.Serialisation;

public class SimpleRoundTripTests
{
  private IOperations _operations;

  public SimpleRoundTripTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, Assembly.GetExecutingAssembly());
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  public static IEnumerable<object[]> TestData() => TestDataInternal().Select(x => new object[] { x });

  public static IEnumerable<Base> TestDataInternal()
  {
    yield return new DiningTable { ["@strangeVariable_NAme3"] = new TableLegFixture() };

    var polyline = new Polyline();
    for (int i = 0; i < 100; i++)
    {
      polyline.Points.Add(new Point { X = i * 2, Y = i % 2 });
    }
    yield return polyline;
  }

  [Theory]
  [MemberData(nameof(TestData))]
  public async Task SimpleSerialization(Base testData)
  {
    var result = _operations.Serialize(testData);
    var test = await _operations.DeserializeAsync(result);

    testData.GetId().Should().Be(test.GetId());
  }
}
