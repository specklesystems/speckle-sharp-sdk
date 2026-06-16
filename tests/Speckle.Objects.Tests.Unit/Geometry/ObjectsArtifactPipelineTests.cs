using AwesomeAssertions;
using DuckDB.NET.Data;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class ObjectsArtifactPipelineTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), $"objects-pipeline-{Guid.NewGuid():N}");

  public void Dispose()
  {
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, recursive: true);
    }
  }

  [Fact]
  public void EncodesGeometryToObjectsDuckDb_AndRoundTripsViaSgeo()
  {
    var line = new Line
    {
      start = new Point(0, 0, 0, Units.Meters),
      end = new Point(3, 3, 6, Units.Meters),
      units = Units.Meters,
    };
    var mesh = new Mesh
    {
      vertices = [0, 0, 0, 1, 0, 0, 0, 1, 0],
      faces = [3, 0, 1, 2],
      units = Units.Meters,
    };

    var dataObjectJson = JObject.Parse(
      """{"speckle_type":"Objects.Data.DataObject","applicationId":"wall-1","name":"Wall","properties":{"thickness":{"name":"thickness","value":0.3,"units":"m"}}}"""
    );

    string dbPath;
    string eavPath;
    using (var pipeline = new ObjectsArtifactPipeline(_dir, "proj_ing"))
    {
      dbPath = pipeline.ObjectsDbPath;
      eavPath = pipeline.EavDbPath;
      pipeline.AddGeometry("wall-1", line);
      pipeline.AddGeometry("wall-1", mesh); // multi-geometry object
      pipeline.AddProxy("layer", """{"id":"L1/Walls","parentId":null,"name":"Walls","objects":["wall-1"]}""");
      pipeline.AddProperties("wall-1", dataObjectJson);
      pipeline.Complete();
    }

    using var db = new DuckDBConnection($"Data Source={dbPath}");
    db.Open();

    Scalar<long>(db, "SELECT COUNT(*) FROM geometries WHERE applicationId = 'wall-1'").Should().Be(2);
    Scalar<long>(db, "SELECT COUNT(*) FROM geometries WHERE type = 'line'").Should().Be(1);
    Scalar<long>(db, "SELECT COUNT(*) FROM geometries WHERE type = 'mesh'").Should().Be(1);
    Scalar<long>(db, "SELECT COUNT(*) FROM proxies WHERE type = 'layer'").Should().Be(1);

    // The stored blob is a real SGEO buffer: read it back and decode it.
    var lineBlob = ReadBlobBytes(db, "SELECT content FROM geometries WHERE type = 'line'");
    var decoded = (Line)SgeoDecoder.Decode(lineBlob);
    decoded.end.x.Should().Be(3);
    decoded.end.z.Should().Be(6);
    decoded.units.Should().Be(Units.Meters);

    // eav.duckdb: properties flattened and keyed by applicationId.
    using var eav = new DuckDBConnection($"Data Source={eavPath}");
    eav.Open();
    Scalar<long>(eav, "SELECT COUNT(*) FROM eav WHERE applicationId = 'wall-1' AND path = 'properties.thickness'")
      .Should()
      .Be(1);
    Scalar<double>(eav, "SELECT value_double FROM eav WHERE applicationId = 'wall-1' AND path = 'properties.thickness'")
      .Should()
      .Be(0.3);
  }

  [Fact]
  public void AddProperties_DropsExcludedTopLevelCategories()
  {
    // "Autodesk Material" and "Document" are high-volume / redundant Revit tabs
    // (carried through Navisworks) — excluded from the binary eav by default.
    var json = JObject.Parse(
      """
      {
        "speckle_type": "Objects.Data.DataObject",
        "applicationId": "elem-1",
        "properties": {
          "Autodesk Material": { "Name": { "name": "Name", "value": "Concrete" } },
          "Document": { "Title": { "name": "Title", "value": "model.rvt" } },
          "Element": { "Volume": { "name": "Volume", "value": 1.5, "units": "m3" } }
        }
      }
      """
    );

    string eavPath;
    using (var pipeline = new ObjectsArtifactPipeline(_dir, "excl"))
    {
      eavPath = pipeline.EavDbPath;
      pipeline.AddProperties("elem-1", json);
      pipeline.Complete();
    }

    using var eav = new DuckDBConnection($"Data Source={eavPath}");
    eav.Open();

    Scalar<long>(eav, "SELECT COUNT(*) FROM eav WHERE path LIKE 'properties.Autodesk Material%'").Should().Be(0);
    Scalar<long>(eav, "SELECT COUNT(*) FROM eav WHERE path LIKE 'properties.Document%'").Should().Be(0);
    // A normal category is still extracted.
    Scalar<long>(eav, "SELECT COUNT(*) FROM eav WHERE path = 'properties.Element.Volume'").Should().Be(1);
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Test-only constant queries."
  )]
  private static T Scalar<T>(DuckDBConnection conn, string sql)
  {
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    var result = cmd.ExecuteScalar();
    return (T)Convert.ChangeType(result, typeof(T))!;
  }

  [System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Test-only constant queries."
  )]
  private static byte[] ReadBlobBytes(DuckDBConnection conn, string sql)
  {
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    using var reader = cmd.ExecuteReader();
    reader.Read().Should().BeTrue();
    var value = reader.GetValue(0);
    if (value is byte[] bytes)
    {
      return bytes;
    }
    if (value is Stream stream)
    {
      using var ms = new MemoryStream();
      stream.CopyTo(ms);
      return ms.ToArray();
    }
    throw new InvalidOperationException($"Unexpected BLOB representation: {value.GetType()}");
  }
}
