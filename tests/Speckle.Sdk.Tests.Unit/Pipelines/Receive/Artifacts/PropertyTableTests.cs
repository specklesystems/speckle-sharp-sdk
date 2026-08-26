using Speckle.Objects.Utils;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines.Receive.Artifacts;

/// <summary>The columnar property store must answer exactly what the nested reader answers, for less memory.</summary>
public sealed class PropertyTableTests : IDisposable
{
  private static readonly SpeckleApplication s_producer = new()
  {
    HostApplication = "Test",
    HostApplicationVersion = "1.0",
    Slug = "test",
    SpeckleVersion = "0.0.0",
  };

  private readonly string _dir = Path.Combine(Path.GetTempPath(), "PropertyTableTests", Guid.NewGuid().ToString("N"));

  private void WriteBundle()
  {
    using var pipeline = new ObjectsArtifactPipeline(_dir, "p", s_producer);
    for (int i = 0; i < 5; i++)
    {
      string id = $"obj-{i}";
      pipeline.InternObject(id);
      pipeline.AddProperties(
        id,
        new Dictionary<string, object?>
        {
          ["A"] = new Dictionary<string, object?> { ["x"] = (double)i, ["flag"] = i % 2 == 0 },
          ["label"] = i == 3 ? null : $"L{i}",
        },
        [new("name", $"Object {i}"), new("units", "mm")]
      );
    }
    pipeline.SetProducer(s_producer);
    pipeline.Complete();
  }

  [Fact]
  public async Task Columnar_MatchesNested()
  {
    WriteBundle();
    var nested = await ArtefactBundleReader.ReadAsync(_dir, ArtefactReadOptions.Eager, CancellationToken.None);
    var columnar = await ArtefactBundleReader.ReadAsync(_dir, ArtefactReadOptions.Columnar, CancellationToken.None);
    var table = columnar.PropertyTable!;

    Assert.Equal(nested.Properties.Count, table.KeyCount);
    Assert.Equal(nested.Units, columnar.Units);
    foreach (var kv in nested.Properties)
    {
      // ToNested() rebuilds the exact shape the nested reader produced.
      Assert.Equal(Flatten(kv.Value), Flatten(table[kv.Key].ToNested()));
    }
  }

  [Fact]
  public async Task Lookups_ScansAndViews()
  {
    WriteBundle();
    var bundle = await ArtefactBundleReader.ReadAsync(_dir, ArtefactReadOptions.Columnar, CancellationToken.None);
    var table = bundle.PropertyTable!;

    Assert.Equal(2.0, table.GetDouble(2, "properties.A.x"));
    Assert.True(table.GetBool(2, "properties.A.flag"));
    Assert.Null(table.GetString(3, "properties.label")); // null-valued row dropped at load
    Assert.False(table.TryGetValue(3, "properties.label", out _));
    Assert.Equal("Object 4", table.GetString(4, "name"));

    Assert.Equal([0, 1, 2, 4], table.KeysWith("properties.label"));
    Assert.Equal(5, table.ValuesOf("properties.A.x").Count());
    Assert.Empty(table.KeysWith("properties.missing"));
    Assert.Equal(-1, table.PathId("properties.missing"));

    var under = table.Under(1, "properties");
    Assert.Equal(["A.flag", "A.x", "label"], under.Keys.OrderBy(k => k, StringComparer.Ordinal));
    Assert.Equal(3, under.Count);
    Assert.False(under.ContainsKey("name"));
    Assert.True(table[1].ContainsKey("name"));
    Assert.Throws<KeyNotFoundException>(() => under["nope"]);
  }

  private static SortedDictionary<string, string?> Flatten(Dictionary<string, object?> nested)
  {
    var flat = new SortedDictionary<string, string?>(StringComparer.Ordinal);
    Walk(nested, null);
    return flat;

    void Walk(Dictionary<string, object?> d, string? prefix)
    {
      foreach (var kv in d)
      {
        string path = prefix is null ? kv.Key : $"{prefix}.{kv.Key}";
        if (kv.Value is Dictionary<string, object?> child)
        {
          Walk(child, path);
        }
        else
        {
          flat[path] = kv.Value?.ToString();
        }
      }
    }
  }

  public void Dispose()
  {
    if (Directory.Exists(_dir))
    {
      Directory.Delete(_dir, true);
    }
  }
}
