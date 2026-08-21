using System.Globalization;
using AwesomeAssertions;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.BundleMigrator.Migration;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Tests;

/// <summary>
/// Guards ENG-9060: a v3 Revit root's <c>referencePointTransform</c> (basis columns first, translation at
/// 12–14 in internal feet) re-emits as the spec's <c>referencePoint.*</c> eav.model rows — row-major layout,
/// translation in the graph's display units, no <c>kind</c> row (v3 never recorded the selection) — and a
/// root without it produces no rows.
/// </summary>
public class V3GraphArtifactProducerReferencePointTests
{
  public V3GraphArtifactProducerReferencePointTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Mesh).Assembly);
  }

  private static readonly SpeckleApplication TestProducer = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.2.3",
    Slug = "test-connector",
    SpeckleVersion = "999.1.0-alpha.1",
  };

  private static Mesh UnitMesh(string appId, string units) =>
    new()
    {
      vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
      faces = new List<int> { 3, 0, 1, 2 },
      units = units,
      applicationId = appId,
      id = appId,
    };

  private static Collection RootWithMesh(string units) =>
    new()
    {
      name = "root",
      applicationId = "root",
      elements = { UnitMesh("m1", units) },
    };

  private static Stats Migrate(Collection root, string dir)
  {
    using var producer = new V3GraphArtifactProducer(
      new ObjectsArtifactPipeline(dir, "v3", TestProducer),
      new ArtifactHelper()
    );
    return producer.Produce(root);
  }

  private static string TempDir() =>
    Path.Combine(Path.GetTempPath(), "SpeckleV3RefPoint", Guid.NewGuid().ToString("N"));

  private static Dictionary<string, object?> RefPointValue(List<object> matrix) => new() { ["transform"] = matrix };

  private static double[] ParseCsv(string csv) =>
    csv.Split(',').Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();

  private static async Task<IReadOnlyDictionary<string, object?>> ReadReferencePoint(string dir)
  {
    var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
    return bundle
      .ModelProperties["referencePoint"]
      .Should()
      .BeAssignableTo<IReadOnlyDictionary<string, object?>>()
      .Subject;
  }

  [Fact]
  public async Task TranslationOnly_ScalesFeetToGraphUnits_NoKindRow()
  {
    var dir = TempDir();
    try
    {
      var root = RootWithMesh("mm");
      // Mixed long/double elements — the deserializer yields integral JSON as long.
      root["referencePointTransform"] = RefPointValue([
        1L,
        0L,
        0L,
        0L,
        0L,
        1L,
        0L,
        0L,
        0L,
        0L,
        1L,
        0L,
        10.0,
        20.0,
        -5.0,
        1L,
      ]);

      var stats = Migrate(root, dir);
      var refPoint = await ReadReferencePoint(dir);

      var d = ParseCsv((string)refPoint["transform"]!);
      d.Should().HaveCount(16);
      // identity basis transposed in place; translation moved to 3/7/11 and scaled ft → mm
      double[] expected = [1, 0, 0, 3048, 0, 1, 0, 6096, 0, 0, 1, -1524, 0, 0, 0, 1];
      for (var i = 0; i < 16; i++)
      {
        d[i].Should().BeApproximately(expected[i], 1e-9);
      }
      refPoint["units"].Should().Be("mm");
      refPoint.Should().NotContainKey("kind");
      stats.ReferencePoints.Should().Be(1);
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  [Fact]
  public async Task RotatedMatrix_BasisPreservedExactly()
  {
    var dir = TempDir();
    try
    {
      var (c, s) = (Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6)); // 30° true north about Z
      var root = RootWithMesh("m");
      root["referencePointTransform"] = RefPointValue([
        c,
        s,
        0.0,
        0L,
        -s,
        c,
        0.0,
        0L,
        0L,
        0L,
        1L,
        0L,
        100.0,
        0L,
        0L,
        1L,
      ]);

      var stats = Migrate(root, dir);
      var refPoint = await ReadReferencePoint(dir);

      refPoint.Should().NotContainKey("kind");
      var d = ParseCsv((string)refPoint["transform"]!);
      // basis is copied without arithmetic — "R" formatting round-trips it exactly
      (d[0], d[4], d[8])
        .Should()
        .Be((c, s, 0.0)); // BasisX now the first column of the row-major layout
      (d[1], d[5], d[9]).Should().Be((-s, c, 0.0));
      (d[2], d[6], d[10]).Should().Be((0.0, 0.0, 1.0));
      d[3].Should().BeApproximately(100 * 0.3048, 1e-9);
      (d[7], d[11]).Should().Be((0.0, 0.0));
      stats.ReferencePoints.Should().Be(1);
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  [Fact]
  public async Task NoRootKey_EmitsNoRows()
  {
    var dir = TempDir();
    try
    {
      var stats = Migrate(RootWithMesh("m"), dir);

      File.Exists(Path.Combine(dir, "v3.eav.model.parquet")).Should().BeFalse();
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      bundle.ModelProperties.Should().NotContainKey("referencePoint");
      stats.ReferencePoints.Should().Be(0);
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  [Fact]
  public async Task MalformedMatrix_SkippedWithNote()
  {
    var dir = TempDir();
    try
    {
      var root = RootWithMesh("m");
      root["referencePointTransform"] = RefPointValue([1L, 0L, 0L, 0L, 1L, 0L, 0L, 0L, 1L, 5.0, 6.0, 7.0]); // 12 values

      var stats = Migrate(root, dir);

      File.Exists(Path.Combine(dir, "v3.eav.model.parquet")).Should().BeFalse();
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      bundle.ModelProperties.Should().NotContainKey("referencePoint");
      stats.ReferencePoints.Should().Be(0);
      stats.Notes.Should().Contain(n => n.Contains("referencePointTransform"));
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }

  [Fact]
  public async Task RoundTrip_ReaderRebuildsOriginalV3Matrix()
  {
    var dir = TempDir();
    try
    {
      var (c, s) = (Math.Cos(0.2), Math.Sin(0.2));
      double[] original = [c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 25.5, -140.25, 3.75, 1];
      var root = RootWithMesh("m");
      root["referencePointTransform"] = RefPointValue(original.Cast<object>().ToList());

      Migrate(root, dir);

      // the reader's BuildReferencePointRootValue is the exact inverse: back to feet, translation at 12–14
      var reader = new ObjectsArtifactReader();
      var rebuiltRoot = (Collection)
        await reader.ReadAsync(dir, new ArtifactReceiveOptions(PreferSolids: true), default);
      var refPoint = rebuiltRoot["referencePointTransform"].Should().BeOfType<Dictionary<string, object>>().Subject;
      var rebuilt = (double[])refPoint["transform"];
      for (var i = 0; i < 16; i++)
      {
        // Units.GetConversionFactor's table holds 6-significant-digit constants that aren't exact reciprocals
        // (ft→m × m→ft ≈ 1.000000032), so the translation round-trips with ~3e-8 relative error.
        rebuilt[i].Should().BeApproximately(original[i], Math.Max(1e-9, Math.Abs(original[i]) * 1e-7));
      }
    }
    finally
    {
      if (Directory.Exists(dir))
      {
        Directory.Delete(dir, true);
      }
    }
  }
}
