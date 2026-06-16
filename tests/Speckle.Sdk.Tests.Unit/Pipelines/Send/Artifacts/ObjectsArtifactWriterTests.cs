using System.Security.Cryptography;
using AwesomeAssertions;
using DuckDB.NET.Data;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Send.Artifacts;

public class ObjectsArtifactWriterTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), $"objects-artifacts-{Guid.NewGuid():N}");

  public void Dispose()
  {
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, recursive: true);
    }
  }

  /// <summary>
  /// A minimal valid SGEO blob: 16-byte header ("SGEO", v1, given primitive
  /// type) + arbitrary body bytes. The writer treats the blob opaquely — it
  /// only reads the magic + type byte and hashes the whole buffer.
  /// </summary>
  private static byte[] Sgeo(byte primitiveType, params byte[] body)
  {
    var buf = new byte[16 + body.Length];
    buf[0] = (byte)'S';
    buf[1] = (byte)'G';
    buf[2] = (byte)'E';
    buf[3] = (byte)'O';
    buf[4] = 1; // version
    buf[5] = primitiveType;
    body.CopyTo(buf, 16);
    return buf;
  }

  private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

  [Fact]
  public void WritesGeometriesAndProxies_WithTypeAndContentHash()
  {
    var mesh = Sgeo(0, 0x01, 0x02, 0x03);
    var line = Sgeo(1, 0xAA, 0xBB);

    string dbPath;
    using (var writer = new ObjectsArtifactWriter(_dir, "proj_ing"))
    {
      dbPath = writer.ObjectsDbPath;

      writer.AddGeometry("wall-1", mesh);
      writer.AddGeometry("wall-1", line); // same object, second geometry (multi-geometry)
      writer.AddGeometry("col-2", mesh); // different object, identical buffer

      writer.AddProxy("layer", """{"id":"L1/Walls","parentId":null,"name":"Walls","objects":["wall-1"]}""");
      writer.AddProxy("material", """{"id":"mat:steel","objects":["wall-1","col-2"],"value":{"name":"Steel"}}""");

      writer.Complete();
    }

    using var db = new DuckDBConnection($"Data Source={dbPath}");
    db.Open();

    // 3 geometry rows: (wall-1, mesh), (wall-1, line), (col-2, mesh)
    Scalar<long>(db, "SELECT COUNT(*) FROM geometries").Should().Be(3);

    // type derived from the SGEO header byte
    Scalar<string>(db, "SELECT type FROM geometries WHERE applicationId = 'wall-1' AND type = 'line'")
      .Should()
      .Be("line");
    Scalar<long>(db, "SELECT COUNT(*) FROM geometries WHERE type = 'mesh'").Should().Be(2);

    // id = SHA256 of the blob; identical buffers share the same id across objects
    Scalar<string>(db, "SELECT id FROM geometries WHERE applicationId = 'wall-1' AND type = 'mesh'")
      .Should()
      .Be(Sha256Hex(mesh));
    Scalar<long>(db, $"SELECT COUNT(DISTINCT applicationId) FROM geometries WHERE id = '{Sha256Hex(mesh)}'")
      .Should()
      .Be(2);

    // content round-trips byte-for-byte
    ReadBlobBytes(db, "SELECT content FROM geometries WHERE applicationId = 'wall-1' AND type = 'line'")
      .Should()
      .Equal(line);

    // proxies
    Scalar<long>(db, "SELECT COUNT(*) FROM proxies").Should().Be(2);
    Scalar<long>(db, "SELECT COUNT(*) FROM proxies WHERE type = 'layer'").Should().Be(1);
  }

  [Fact]
  public void DuplicateGeometry_ForSameObject_WrittenOnce()
  {
    var mesh = Sgeo(0, 0x01, 0x02);

    string dbPath;
    using (var writer = new ObjectsArtifactWriter(_dir, "dedup"))
    {
      dbPath = writer.ObjectsDbPath;
      writer.AddGeometry("wall-1", mesh);
      writer.AddGeometry("wall-1", mesh); // exact same (applicationId, id) → ignored
      writer.Complete();
    }

    using var db = new DuckDBConnection($"Data Source={dbPath}");
    db.Open();
    Scalar<long>(db, "SELECT COUNT(*) FROM geometries").Should().Be(1);
  }

  [Fact]
  public void AddGeometry_RejectsNonSgeoBuffer()
  {
    using var writer = new ObjectsArtifactWriter(_dir, "bad");
    Action act = () => writer.AddGeometry("x", new byte[] { 0x00, 0x01, 0x02 });
    act.Should().Throw<ArgumentException>().WithMessage("*SGEO*");
  }

  [Fact]
  public void Complete_ReleasesFileLock()
  {
    using var writer = new ObjectsArtifactWriter(_dir, "locks");
    writer.AddGeometry("a", Sgeo(1, 0x01));
    writer.Complete();

    using var stream = new FileStream(writer.ObjectsDbPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    stream.Length.Should().BeGreaterThan(0);
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
