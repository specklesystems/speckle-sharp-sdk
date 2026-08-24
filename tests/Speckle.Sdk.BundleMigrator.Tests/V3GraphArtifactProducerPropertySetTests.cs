using System.Security.Cryptography;
using System.Text;
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
/// Guards ENG-9062: a v3 Civil3D root's <c>propertySetDefinitions</c> re-emits as
/// <c>eav.property_set_definitions</c> rows — one per field in authored (dict) order, native defaults split,
/// the shared set_key recipe — and a root without it produces no file.
/// </summary>
public class V3GraphArtifactProducerPropertySetTests
{
  public V3GraphArtifactProducerPropertySetTests()
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

  private static Collection RootWithMesh() =>
    new()
    {
      name = "root",
      applicationId = "root",
      elements =
      {
        new Mesh
        {
          vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
          faces = new List<int> { 3, 0, 1, 2 },
          units = "m",
          applicationId = "m1",
          id = "m1",
        },
      },
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
    Path.Combine(Path.GetTempPath(), "SpeckleV3PropSets", Guid.NewGuid().ToString("N"));

  private static Dictionary<string, object?> D(params (string Key, object? Value)[] entries)
  {
    var d = new Dictionary<string, object?>();
    foreach (var (key, value) in entries)
    {
      d[key] = value;
    }
    return d;
  }

  private static Dictionary<string, object?> SetDefinition(string name, Dictionary<string, object?> fieldDefs) =>
    D(("name", name), ("propertyDefinitions", fieldDefs));

  private static string ExpectedSetKey(string material) =>
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));

  [Fact]
  public async Task Definitions_MapToSpecRows_InDictOrder()
  {
    var dir = TempDir();
    try
    {
      var root = RootWithMesh();
      root["propertySetDefinitions"] = D(
        (
          "Pipe Data",
          SetDefinition(
            "Pipe Data",
            D(
              (
                "Slope",
                D(
                  ("name", "Slope"),
                  ("description", "Design slope"),
                  ("id", 1L),
                  ("dataType", "Real"),
                  ("defaultValue", 2L), // integral JSON deserializes as long → default_double
                  ("units", "%")
                )
              ),
              ("Service", D(("dataType", "Text"), ("defaultValue", "Supply"))),
              ("Insulated", D(("dataType", "TrueFalse"), ("defaultValue", false)))
            )
          )
        )
      );

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      var rows = bundle.PropertySetDefinitions;

      rows.Should().HaveCount(3);
      rows.Select(r => r.FieldName).Should().Equal("Slope", "Service", "Insulated");
      rows.Should().OnlyContain(r => r.SetName == "Pipe Data");
      var expectedKey = ExpectedSetKey("Pipe Data\nSlope|Real|%\nService|Text|\nInsulated|TrueFalse|");
      rows.Should().OnlyContain(r => r.SetKey == expectedKey);
      rows.Should().OnlyContain(r => r.FieldBucketId == null && r.SetDescription == null && r.AppliesTo == null);

      var slope = rows[0];
      (slope.DataType, slope.Unit, slope.Description).Should().Be(("Real", "%", "Design slope"));
      (slope.DefaultDouble, slope.DefaultString, slope.DefaultBoolean).Should().Be((2.0, null, null));
      (rows[1].DefaultString, rows[1].DefaultDouble, rows[1].DefaultBoolean).Should().Be(("Supply", null, null));
      (rows[2].DefaultBoolean, rows[2].DefaultString, rows[2].DefaultDouble).Should().Be((false, null, null));

      stats.PropertySets.Should().Be(1);
      stats.PropertySetFields.Should().Be(3);
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
  public async Task DetachedRootKey_IsAlsoRead()
  {
    var dir = TempDir();
    try
    {
      var root = RootWithMesh();
      root["@propertySetDefinitions"] = D(("Minimal", SetDefinition("Minimal", D(("Depth", D(("dataType", "Real")))))));

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);

      var row = bundle.PropertySetDefinitions.Should().ContainSingle().Subject;
      (row.SetName, row.FieldName, row.DataType).Should().Be(("Minimal", "Depth", "Real"));
      (row.DefaultString, row.DefaultDouble, row.DefaultBoolean).Should().Be((null, null, null));
      stats.PropertySets.Should().Be(1);
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
  public async Task NoRootKey_ProducesNoFile()
  {
    var dir = TempDir();
    try
    {
      var stats = Migrate(RootWithMesh(), dir);

      File.Exists(Path.Combine(dir, "v3.eav.property_set_definitions.parquet")).Should().BeFalse();
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);
      bundle.PropertySetDefinitions.Should().BeEmpty();
      stats.PropertySets.Should().Be(0);
      stats.PropertySetFields.Should().Be(0);
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
  public async Task MalformedEntries_SkippedWithNotes_ValidRowsStillEmitted()
  {
    var dir = TempDir();
    try
    {
      var root = RootWithMesh();
      root["propertySetDefinitions"] = D(
        ("NotADict", "nope"),
        ("NoFields", D(("name", "NoFields"))),
        ("Good", SetDefinition("Good", D(("Width", D(("dataType", "Real"), ("units", "mm"))), ("Broken", 5L))))
      );

      var stats = Migrate(root, dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);

      var row = bundle.PropertySetDefinitions.Should().ContainSingle().Subject;
      (row.SetName, row.FieldName, row.Unit).Should().Be(("Good", "Width", "mm"));
      row.SetKey.Should().Be(ExpectedSetKey("Good\nWidth|Real|mm")); // non-dict fields excluded from the hash too

      stats.PropertySets.Should().Be(1);
      stats.PropertySetFields.Should().Be(1);
      stats.Notes.Should().Contain(n => n.Contains("NotADict"));
      stats.Notes.Should().Contain(n => n.Contains("NoFields"));
      stats.Notes.Should().Contain(n => n.Contains("Broken"));
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
