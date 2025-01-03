using System.Drawing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;
using Xunit;
using Point = Speckle.Sdk.Tests.Unit.Host.Point;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public class ObjectSerialization
{
  private readonly IOperations _operations;

  public ObjectSerialization()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DataChunk).Assembly, typeof(ColorMock).Assembly);
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [Fact]
  public async Task IgnoreCircularReferences()
  {
    var pt = new Point(1, 2, 3);
    pt["circle"] = pt;

    var test = _operations.Serialize(pt);

    var result = await _operations.DeserializeAsync(test);
    var circle = result["circle"];
    circle.ShouldBeNull();
  }

  [Fact]
  public async Task InterfacePropHandling()
  {
    Line tail = new() { Start = new Point(0, 0, 0), End = new Point(42, 42, 42) };
    PolygonalFeline cat = new() { Tail = tail };

    for (int i = 0; i < 10; i++)
    {
      cat.Claws[$"Claw number {i}"] = new Line
      {
        Start = new Point(i, i, i),
        End = new Point(i + 3.14, i + 3.14, i + 3.14),
      };

      if (i % 2 == 0)
      {
        cat.Whiskers.Add(
          new Line { Start = new Point(i / 2, i / 2, i / 2), End = new Point(i + 3.14, i + 3.14, i + 3.14) }
        );
      }
      else
      {
        var brokenWhisker = new Polyline();
        brokenWhisker.Points.Add(new Point(-i, 0, 0));
        brokenWhisker.Points.Add(new Point(0, 0, 0));
        brokenWhisker.Points.Add(new Point(i, 0, 0));
        cat.Whiskers.Add(brokenWhisker);
      }

      cat.Fur[i] = new Line { Start = new Point(i, i, i), End = new Point(i + 3.14, i + 3.14, i + 3.14) };
    }

    var result = _operations.Serialize(cat);

    var deserialisedFeline = await _operations.DeserializeAsync(result);

    deserialisedFeline.GetId().ShouldBe(cat.GetId());
  }

  [Fact]
  public async Task InheritanceTests()
  {
    var superPoint = new SuperPoint
    {
      X = 10,
      Y = 10,
      Z = 10,
      W = 42,
    };

    var str = _operations.Serialize(superPoint);
    var sstr = await _operations.DeserializeAsync(str);

    sstr.speckle_type.ShouldBe(superPoint.speckle_type);
  }

  [Fact]
  public async Task ListDynamicProp()
  {
    var point = new Point();
    var test = new List<Base>();

    for (var i = 0; i < 100; i++)
    {
      test.Add(new SuperPoint { W = i });
    }

    point["test"] = test;

    var str = _operations.Serialize(point);
    var dsrls = await _operations.DeserializeAsync(str);

    var list = dsrls["test"] as List<object>;
    list.ShouldNotBeNull(); // Ensure the list isn't null in first place
    list.Count.ShouldBe(100);
  }

  [Fact]
  public async Task ChunkSerialisation()
  {
    var baseBasedChunk = new DataChunk() { data = new() };
    for (var i = 0; i < 200; i++)
    {
      baseBasedChunk.data.Add(new SuperPoint { W = i });
    }

    var stringBasedChunk = new DataChunk() { data = new() };
    for (var i = 0; i < 200; i++)
    {
      stringBasedChunk.data.Add(i + "_hai");
    }

    var doubleBasedChunk = new DataChunk() { data = new() };
    for (var i = 0; i < 200; i++)
    {
      doubleBasedChunk.data.Add(i + 0.33);
    }

    var baseChunkString = _operations.Serialize(baseBasedChunk);
    var stringChunkString = _operations.Serialize(stringBasedChunk);
    var doubleChunkString = _operations.Serialize(doubleBasedChunk);

    var baseChunkDeserialised = (DataChunk)await _operations.DeserializeAsync(baseChunkString);
    var stringChunkDeserialised = (DataChunk)await _operations.DeserializeAsync(stringChunkString);
    var doubleChunkDeserialised = (DataChunk)await _operations.DeserializeAsync(doubleChunkString);

    baseChunkDeserialised.data.Count.ShouldBe(baseBasedChunk.data.Count);
    stringChunkDeserialised.data.Count.ShouldBe(stringBasedChunk.data.Count);
    doubleChunkDeserialised.data.Count.ShouldBe(doubleBasedChunk.data.Count);
  }

  [Fact]
  public async Task ObjectWithChunksSerialisation()
  {
    const int MAX_NUM = 2020;
    var mesh = new FakeMesh { ArrayOfDoubles = new double[MAX_NUM], ArrayOfLegs = new TableLeg[MAX_NUM] };

    var customChunk = new List<double>();
    var defaultChunk = new List<double>();

    for (int i = 0; i < MAX_NUM; i++)
    {
      mesh.Vertices.Add(i / 2);
      customChunk.Add(i / 2);
      defaultChunk.Add(i / 2);
      mesh.Tables.Add(new Tabletop { length = 2000 });
      mesh.ArrayOfDoubles[i] = i * 3.3;
      mesh.ArrayOfLegs[i] = new TableLeg { height = 2 + i };
    }

    mesh["@(800)CustomChunk"] = customChunk;
    mesh["@()DefaultChunk"] = defaultChunk;

    var serialised = _operations.Serialize(mesh);
    var deserialised = await _operations.DeserializeAsync(serialised);

    mesh.GetId().ShouldBe(deserialised.GetId());
  }

  [Fact]
  public void EmptyListSerialisationTests()
  {
    // NOTE: expected behaviour is that empty lists should serialize as empty lists. Don't ask why, it's complicated.
    // Regarding chunkable empty lists, to prevent empty chunks, the expected behaviour is to have an empty lists, with no chunks inside.
    var test = new Base();

    test["@(5)emptyChunks"] = new List<object>();
    test["emptyList"] = new List<object>();
    test["@emptyDetachableList"] = new List<object>();

    // Note: nested empty lists should be preserved.
    test["nestedList"] = new List<object> { new List<object> { new List<object>() } };
    test["@nestedDetachableList"] = new List<object> { new List<object> { new List<object>() } };

    var serialised = _operations.Serialize(test);
    var isCorrect =
      serialised.Contains("\"@(5)emptyChunks\":[]")
      && serialised.Contains("\"emptyList\":[]")
      && serialised.Contains("\"@emptyDetachableList\":[]")
      && serialised.Contains("\"nestedList\":[[[]]]")
      && serialised.Contains("\"@nestedDetachableList\":[[[]]]");

    isCorrect.ShouldBeTrue();
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Api.Operations.ObjectSerialization+DateMock")]
  private class DateMock : Base
  {
    public DateTime TestField { get; set; }
  }

  [Fact]
  public async Task DateSerialisation()
  {
    var date = new DateTime(2020, 1, 14);
    var mockBase = new DateMock { TestField = date };

    var result = _operations.Serialize(mockBase);
    var test = (DateMock)await _operations.DeserializeAsync(result);

    test.TestField.ShouldBe(date);
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Api.Operations.ObjectSerialization+GUIDMock")]
  private class GUIDMock : Base
  {
    public Guid TestField { get; set; }
  }

  [Fact]
  public async Task GuidSerialisation()
  {
    var guid = Guid.NewGuid();
    var mockBase = new GUIDMock { TestField = guid };

    var result = _operations.Serialize(mockBase);
    var test = (GUIDMock)await _operations.DeserializeAsync(result);

    test.TestField.ShouldBe(guid);
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Api.Operations.ObjectSerialization+ColorMock")]
  private class ColorMock : Base
  {
    public Color TestField { get; set; }
  }

  [Fact]
  public async Task ColorSerialisation()
  {
    var color = Color.FromArgb(255, 4, 126, 251);
    var mockBase = new ColorMock { TestField = color };

    var result = _operations.Serialize(mockBase);
    var test = (ColorMock)await _operations.DeserializeAsync(result);

    test.TestField.ShouldBe(color);
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Api.Operations.ObjectSerialization+StringDateTimeRegressionMock")]
  private class StringDateTimeRegressionMock : Base
  {
    public string TestField { get; set; }
  }

  [Fact]
  public async Task StringDateTimeRegression()
  {
    var mockBase = new StringDateTimeRegressionMock { TestField = "2021-11-12T11:32:01" };

    var result = _operations.Serialize(mockBase);
    var test = (StringDateTimeRegressionMock)await _operations.DeserializeAsync(result);

    test.TestField.ShouldBe(mockBase.TestField);
  }
}
