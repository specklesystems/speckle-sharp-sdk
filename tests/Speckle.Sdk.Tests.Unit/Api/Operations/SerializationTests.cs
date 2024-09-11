using System.Drawing;
using NUnit.Framework;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;
using Point = Speckle.Sdk.Tests.Unit.Host.Point;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

[TestFixture]
[TestOf(typeof(Sdk.Api.Operations))]
public class ObjectSerialization
{
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DataChunk).Assembly, typeof(ColorMock).Assembly);
  }

  [Test]
  public async Task IgnoreCircularReferences()
  {
    var pt = new Point(1, 2, 3);
    pt["circle"] = pt;

    var test = Sdk.Api.Operations.Serialize(pt);

    var result = await Sdk.Api.Operations.DeserializeAsync(test);
    var circle = result["circle"];
    Assert.That(circle, Is.Null);
  }

  [Test]
  public async Task InterfacePropHandling()
  {
    Line tail = new() { Start = new Point(0, 0, 0), End = new Point(42, 42, 42) };
    PolygonalFeline cat = new() { Tail = tail };

    for (int i = 0; i < 10; i++)
    {
      cat.Claws[$"Claw number {i}"] = new Line
      {
        Start = new Point(i, i, i),
        End = new Point(i + 3.14, i + 3.14, i + 3.14)
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

    var result = Sdk.Api.Operations.Serialize(cat);

    var deserialisedFeline = await Sdk.Api.Operations.DeserializeAsync(result);

    Assert.That(deserialisedFeline.GetId(), Is.EqualTo(cat.GetId())); // If we're getting the same hash... we're probably fine!
  }

  [Test]
  public async Task InheritanceTests()
  {
    var superPoint = new SuperPoint
    {
      X = 10,
      Y = 10,
      Z = 10,
      W = 42
    };

    var str = Sdk.Api.Operations.Serialize(superPoint);
    var sstr = await Sdk.Api.Operations.DeserializeAsync(str);

    Assert.That(sstr.speckle_type, Is.EqualTo(superPoint.speckle_type));
  }

  [Test]
  public async Task ListDynamicProp()
  {
    var point = new Point();
    var test = new List<Base>();

    for (var i = 0; i < 100; i++)
    {
      test.Add(new SuperPoint { W = i });
    }

    point["test"] = test;

    var str = Sdk.Api.Operations.Serialize(point);
    var dsrls = await Sdk.Api.Operations.DeserializeAsync(str);

    var list = dsrls["test"] as List<object>; // NOTE: on dynamically added lists, we cannot infer the inner type and we always fall back to a generic list<object>.
    Assert.That(list, Has.Count.EqualTo(100));
  }

  [Test]
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

    var baseChunkString = Sdk.Api.Operations.Serialize(baseBasedChunk);
    var stringChunkString = Sdk.Api.Operations.Serialize(stringBasedChunk);
    var doubleChunkString = Sdk.Api.Operations.Serialize(doubleBasedChunk);

    var baseChunkDeserialised = (DataChunk)await Sdk.Api.Operations.DeserializeAsync(baseChunkString);
    var stringChunkDeserialised = (DataChunk)await Sdk.Api.Operations.DeserializeAsync(stringChunkString);
    var doubleChunkDeserialised = (DataChunk)await Sdk.Api.Operations.DeserializeAsync(doubleChunkString);

    Assert.That(baseChunkDeserialised.data, Has.Count.EqualTo(baseBasedChunk.data.Count));
    Assert.That(stringChunkDeserialised.data, Has.Count.EqualTo(stringBasedChunk.data.Count));
    Assert.That(doubleChunkDeserialised.data, Has.Count.EqualTo(doubleBasedChunk.data.Count));
  }

  [Test]
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

    var serialised = Sdk.Api.Operations.Serialize(mesh);
    var deserialised = await Sdk.Api.Operations.DeserializeAsync(serialised);

    Assert.That(mesh.GetId(), Is.EqualTo(deserialised.GetId()));
  }

  [Test]
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

    var serialised = Sdk.Api.Operations.Serialize(test);
    var isCorrect =
      serialised.Contains("\"@(5)emptyChunks\":[]")
      && serialised.Contains("\"emptyList\":[]")
      && serialised.Contains("\"@emptyDetachableList\":[]")
      && serialised.Contains("\"nestedList\":[[[]]]")
      && serialised.Contains("\"@nestedDetachableList\":[[[]]]");

    Assert.That(isCorrect, Is.EqualTo(true));
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Api.Operations.ObjectSerialization+DateMock")]
  private class DateMock : Base
  {
    public DateTime TestField { get; set; }
  }

  [Test]
  public async Task DateSerialisation()
  {
    var date = new DateTime(2020, 1, 14);
    var mockBase = new DateMock { TestField = date };

    var result = Sdk.Api.Operations.Serialize(mockBase);
    var test = (DateMock)await Sdk.Api.Operations.DeserializeAsync(result);

    Assert.That(test.TestField, Is.EqualTo(date));
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Api.Operations.ObjectSerialization+GUIDMock")]
  private class GUIDMock : Base
  {
    public Guid TestField { get; set; }
  }

  [Test]
  public async Task GuidSerialisation()
  {
    var guid = Guid.NewGuid();
    var mockBase = new GUIDMock { TestField = guid };

    var result = Sdk.Api.Operations.Serialize(mockBase);
    var test = (GUIDMock)await Sdk.Api.Operations.DeserializeAsync(result);

    Assert.That(test.TestField, Is.EqualTo(guid));
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Api.Operations.ObjectSerialization+ColorMock")]
  private class ColorMock : Base
  {
    public Color TestField { get; set; }
  }

  [Test]
  public async Task ColorSerialisation()
  {
    var color = Color.FromArgb(255, 4, 126, 251);
    var mockBase = new ColorMock { TestField = color };

    var result = Sdk.Api.Operations.Serialize(mockBase);
    var test = (ColorMock)await Sdk.Api.Operations.DeserializeAsync(result);

    Assert.That(test.TestField, Is.EqualTo(color));
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Api.Operations.ObjectSerialization+StringDateTimeRegressionMock")]
  private class StringDateTimeRegressionMock : Base
  {
    public string TestField { get; set; }
  }

  [Test]
  public async Task StringDateTimeRegression()
  {
    var mockBase = new StringDateTimeRegressionMock { TestField = "2021-11-12T11:32:01" };

    var result = Sdk.Api.Operations.Serialize(mockBase);
    var test = (StringDateTimeRegressionMock)await Sdk.Api.Operations.DeserializeAsync(result);

    Assert.That(test.TestField, Is.EqualTo(mockBase.TestField));
  }
}
