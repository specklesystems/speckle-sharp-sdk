using AwesomeAssertions;
using DuckDB.NET.Data;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Send;
using Speckle.Sdk.Pipelines.Send.Artifacts;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Send.Artifacts;

public class DuckDbArtifactWriterTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), $"duckdb-artifacts-{Guid.NewGuid():N}");

  public void Dispose()
  {
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, recursive: true);
    }
  }

  private static UploadItem Item(string id, string speckleType, string json) =>
    new(id, new Json(json), speckleType, new ObjectReference { referencedId = id });

  [Fact]
  public void WritesViewerAndEavFiles_WithStrippingAndBlobExtraction()
  {
    var smshBytes = new byte[] { 0x53, 0x4D, 0x53, 0x48, 0x01, 0x00, 0x00, 0x00, 0xAA, 0xBB, 0xCC };
    var smshBase64 = Convert.ToBase64String(smshBytes);

    string viewerPath;
    string eavPath;
    using (var writer = new DuckDbArtifactWriter(_dir, "proj_ing"))
    {
      viewerPath = writer.ViewerDbPath;
      eavPath = writer.EavDbPath;

      // Blob: content goes to blobs table, envelope stripped
      writer.Add(
        Item(
          "blobhash1",
          "Speckle.Core.Models.Blob",
          $$"""{"filePath":"/tmp/x.smsh","content":"{{smshBase64}}","id":"blobhash1","speckle_type":"Speckle.Core.Models.Blob"}"""
        )
      );

      // MeshBinary: not parsed, written as-is
      writer.Add(
        Item(
          "mesh1",
          "Objects.Geometry.MeshBinary",
          """{"units":"m","encoding":"smsh_v1","geometryBlob":{"speckle_type":"reference","referencedId":"blobhash1"},"id":"mesh1"}"""
        )
      );

      // DataObject: EAV rows extracted, properties stripped from envelope
      writer.Add(
        Item(
          "dobj1",
          "Objects.Data.DataObject:Objects.Data.NavisworksObject",
          """{"speckle_type":"Objects.Data.DataObject:Objects.Data.NavisworksObject","name":"Wall","properties":{"thickness":{"name":"thickness","value":0.3,"units":"m"}},"id":"dobj1"}"""
        )
      );

      // duplicate id → ignored
      writer.Add(Item("dobj1", "Objects.Data.DataObject:Objects.Data.NavisworksObject", """{"id":"dobj1"}"""));

      // 'reference' rows → skipped entirely
      writer.Add(Item("someref", "reference", """{"speckle_type":"reference","referencedId":"x"}"""));

      // Root collection last (server convention: last item is root)
      writer.Add(
        Item(
          "root1",
          "Speckle.Core.Models.Collections.Collection",
          """{"speckle_type":"Speckle.Core.Models.Collections.Collection","name":"model.nwd","elements":[{"referencedId":"dobj1"}],"id":"root1"}"""
        )
      );

      writer.Complete();
    }

    // ── viewer.duckdb assertions ─────────────────────────────────────────
    using var viewerDb = new DuckDBConnection($"Data Source={viewerPath}");
    viewerDb.Open();

    Scalar<long>(viewerDb, "SELECT COUNT(*) FROM objects").Should().Be(4); // blob, mesh, dataobject, root collection
    Scalar<long>(viewerDb, "SELECT COUNT(*) FROM blobs").Should().Be(1);

    // Blob envelope stripped, bytes intact
    var blobEnvelope = JObject.Parse(Scalar<string>(viewerDb, "SELECT data FROM objects WHERE id = 'blobhash1'"));
    blobEnvelope.ContainsKey("content").Should().BeFalse();
    blobEnvelope.ContainsKey("filePath").Should().BeTrue();
    ReadBlobBytes(viewerDb, "SELECT content FROM blobs WHERE id = 'blobhash1'").Should().Equal(smshBytes);

    // DataObject envelope stripped of properties, rest intact
    var dataObjEnvelope = JObject.Parse(Scalar<string>(viewerDb, "SELECT data FROM objects WHERE id = 'dobj1'"));
    dataObjEnvelope.ContainsKey("properties").Should().BeFalse();
    ((string)dataObjEnvelope["name"]!).Should().Be("Wall");

    // MeshBinary written verbatim
    var meshEnvelope = JObject.Parse(Scalar<string>(viewerDb, "SELECT data FROM objects WHERE id = 'mesh1'"));
    ((string)meshEnvelope["encoding"]!).Should().Be("smsh_v1");

    // Root = last added item
    Scalar<string>(viewerDb, "SELECT id FROM root").Should().Be("root1");

    // ── eav.duckdb assertions ────────────────────────────────────────────
    using var eavDb = new DuckDBConnection($"Data Source={eavPath}");
    eavDb.Open();

    // DataObject rows present (stripped from envelope but preserved here)
    Scalar<long>(eavDb, "SELECT COUNT(*) FROM properties WHERE object_id = 'dobj1' AND path = 'properties.thickness'")
      .Should()
      .Be(1);
    Scalar<double>(eavDb, "SELECT value_num FROM properties WHERE object_id = 'dobj1' AND path = 'properties.thickness'")
      .Should()
      .Be(0.3);

    // Collection rows present
    Scalar<long>(eavDb, "SELECT COUNT(*) FROM properties WHERE object_id = 'root1' AND path = 'name'").Should().Be(1);

    // No rows for the blob or the mesh
    Scalar<long>(eavDb, "SELECT COUNT(*) FROM properties WHERE object_id IN ('blobhash1','mesh1')").Should().Be(0);
  }

  [Fact]
  public void BlobBytes_AreReadFromFilePath_ThePrimaryPath()
  {
    // Geometry bytes don't travel on the NDJSON wire anymore — the writer
    // reads them straight from the Blob's local temp file.
    var smshBytes = new byte[] { 0x53, 0x4D, 0x53, 0x48, 0x01, 0x00, 0x09, 0x08 };
    Directory.CreateDirectory(_dir);
    var smshFile = Path.Combine(_dir, "geom.smsh");
    File.WriteAllBytes(smshFile, smshBytes);

    string viewerPath;
    using (var writer = new DuckDbArtifactWriter(_dir, "frompath"))
    {
      viewerPath = writer.ViewerDbPath;
      var envelope =
        $$"""{"filePath":{{Speckle.Newtonsoft.Json.JsonConvert.ToString(smshFile)}},"id":"b2","speckle_type":"Speckle.Core.Models.Blob"}""";
      writer.Add(Item("b2", "Speckle.Core.Models.Blob", envelope));
      writer.Complete();
    }

    using var viewerDb = new DuckDBConnection($"Data Source={viewerPath}");
    viewerDb.Open();
    ReadBlobBytes(viewerDb, "SELECT content FROM blobs WHERE id = 'b2'").Should().Equal(smshBytes);
  }

  [Fact]
  public void Complete_ReleasesFileLocks_SoFilesCanBeUploadedBeforeDispose()
  {
    // Regression: SendPipeline uploads the artifact files after Complete() but
    // BEFORE the writer is disposed. DuckDB holds an exclusive lock while a
    // connection is open, so Complete() must close the connections itself —
    // otherwise the upload's FileStream open throws IOException.
    using var writer = new DuckDbArtifactWriter(_dir, "locks");
    writer.Add(Item("a", "Objects.Geometry.MeshBinary", """{"units":"m","id":"a"}"""));
    writer.Complete();

    // Both files must be openable for shared read while `writer` is alive.
    foreach (var path in new[] { writer.ViewerDbPath, writer.EavDbPath })
    {
      using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
      stream.Length.Should().BeGreaterThan(0);
    }
  }

  [Fact]
  public void BlobWithoutContent_IsPassedThroughWithoutBlobRow()
  {
    string viewerPath;
    using (var writer = new DuckDbArtifactWriter(_dir, "nocontent"))
    {
      viewerPath = writer.ViewerDbPath;
      writer.Add(
        Item("b1", "Speckle.Core.Models.Blob", """{"filePath":"/tmp/y.bin","id":"b1","speckle_type":"Speckle.Core.Models.Blob"}""")
      );
      writer.Complete();
    }

    using var viewerDb = new DuckDBConnection($"Data Source={viewerPath}");
    viewerDb.Open();
    Scalar<long>(viewerDb, "SELECT COUNT(*) FROM blobs").Should().Be(0);
    Scalar<long>(viewerDb, "SELECT COUNT(*) FROM objects").Should().Be(1);
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
