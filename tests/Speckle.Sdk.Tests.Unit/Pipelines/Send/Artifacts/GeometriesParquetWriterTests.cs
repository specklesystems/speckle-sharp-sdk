using AwesomeAssertions;
using Parquet;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Send.Artifacts;

public class GeometriesParquetWriterTests : IDisposable
{
  private readonly string _dir = Path.Combine(Path.GetTempPath(), $"geom-shards-{Guid.NewGuid():N}");

  public void Dispose()
  {
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, recursive: true);
    }
  }

  // A minimally-valid SGEO blob: 'SGEO' magic + version + primitive type, padded to `size`.
  private static byte[] Sgeo(int size, byte primitiveType = 0)
  {
    var b = new byte[Math.Max(size, 16)];
    b[0] = (byte)'S';
    b[1] = (byte)'G';
    b[2] = (byte)'E';
    b[3] = (byte)'O';
    b[4] = 1;
    b[5] = primitiveType;
    return b;
  }

  // Reads every geometryIndex across all row groups of one shard file.
  private static async Task<List<int>> ReadIndices(string path)
  {
    var result = new List<int>();
    await using var fs = File.OpenRead(path);
    using var reader = await ParquetReader.CreateAsync(fs);
    var indexField = reader.Schema.DataFields[0]; // geometryIndex
    for (var g = 0; g < reader.RowGroupCount; g++)
    {
      using var rg = reader.OpenRowGroupReader(g);
      var col = await rg.ReadColumnAsync(indexField);
      result.AddRange(((int[])col.Data));
    }
    return result;
  }

  [Fact]
  public void SingleShard_KeepsCanonicalName()
  {
    using var scheduler = new ParquetWriteScheduler();
    string path;
    using (var writer = new GeometriesParquetWriter(_dir, "v1", scheduler, shardCapBytes: 1_000_000))
    {
      path = writer.GeometriesPath;
      writer.AddGeometry(0, Sgeo(500));
      writer.AddGeometry(1, Sgeo(500));
      writer.Complete();
      writer.GeometryPaths.Should().ContainSingle();
    }
    scheduler.CompleteAndWait();

    Path.GetFileName(path).Should().Be("v1.geometries.parquet");
    File.Exists(path).Should().BeTrue();
    File.Exists(Path.Combine(_dir, "v1.geometries.1.parquet")).Should().BeFalse();
  }

  [Fact]
  public async Task RollsToNewShard_WhenCapExceeded_NoDataLoss()
  {
    using var scheduler = new ParquetWriteScheduler();
    List<string> shards;
    // cap 1000B, six 400B blobs → shards roll after [0,1] | [2,3] | [4,5] → 3 shards.
    using (var writer = new GeometriesParquetWriter(_dir, "v1", scheduler, shardCapBytes: 1000))
    {
      for (var i = 0; i < 6; i++)
      {
        writer.AddGeometry(i, Sgeo(400));
      }
      writer.Complete();
      shards = writer.GeometryPaths.ToList();
    }
    scheduler.CompleteAndWait();

    shards.Should().HaveCount(3);
    shards.Select(Path.GetFileName)
      .Should()
      .ContainInOrder("v1.geometries.parquet", "v1.geometries.1.parquet", "v1.geometries.2.parquet");
    shards.Should().OnlyContain(p => File.Exists(p));

    // Every geometryIndex survives exactly once across the shard set — no loss, no dup.
    var all = new List<int>();
    foreach (var shard in shards)
    {
      all.AddRange(await ReadIndices(shard));
    }
    all.Should().BeEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 });
  }

  [Fact]
  public void DeletesStaleShards_FromPreviousRun()
  {
    // Simulate a previous run that produced 3 shards.
    Directory.CreateDirectory(_dir);
    foreach (var name in new[] { "v1.geometries.parquet", "v1.geometries.1.parquet", "v1.geometries.2.parquet" })
    {
      File.WriteAllBytes(Path.Combine(_dir, name), new byte[] { 1, 2, 3 });
    }

    using var scheduler = new ParquetWriteScheduler();
    using (var writer = new GeometriesParquetWriter(_dir, "v1", scheduler, shardCapBytes: 1_000_000))
    {
      writer.AddGeometry(0, Sgeo(500));
      writer.Complete();
    }
    scheduler.CompleteAndWait();

    // The single shard this run wrote remains; the stale .1/.2 are gone (so a dir-glob upload
    // can't pick up orphaned shards from a larger previous run).
    File.Exists(Path.Combine(_dir, "v1.geometries.1.parquet")).Should().BeFalse();
    File.Exists(Path.Combine(_dir, "v1.geometries.2.parquet")).Should().BeFalse();
    File.Exists(Path.Combine(_dir, "v1.geometries.parquet")).Should().BeTrue();
  }
}
