using AwesomeAssertions;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.BundleMigrator.Migration;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Tests;

/// <summary>
/// Guards ENG-9009: v3 Revit objects get a name-derived type key ({linkSuffix}|{category}|{family}|{type}),
/// so their Type Parameters dedupe into types/type_eav/object_type instead of flattening per-instance —
/// skipped on the "none" no-type sentinel, and scoped per linked-model placement by the _t{8hex} appId suffix.
/// </summary>
public class V3GraphArtifactProducerTypeParameterTests
{
  public V3GraphArtifactProducerTypeParameterTests()
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

  private static Stats Migrate(Collection root, string dir)
  {
    using var producer = new V3GraphArtifactProducer(
      new ObjectsArtifactPipeline(dir, "v3", TestProducer),
      new ArtifactHelper()
    );
    return producer.Produce(root);
  }

  private static string TempDir() =>
    Path.Combine(Path.GetTempPath(), "SpeckleV3TypeParams", Guid.NewGuid().ToString("N"));

  private static Dictionary<string, object?> D(params (string Key, object? Value)[] entries)
  {
    var d = new Dictionary<string, object?>();
    foreach (var (key, value) in entries)
    {
      d[key] = value;
    }
    return d;
  }

  private static Dictionary<string, object?> ParamLeaf(object? value, string idn) =>
    D(("value", value), ("name", idn), ("internalDefinitionName", idn));

  // A v3-shaped Revit DataObject: root scalars category/family/type plus the 4-level Parameters tree.
  private static DataObject RevitObject(
    string appId,
    string family,
    string type,
    string category = "Walls",
    string typeMark = "W1",
    bool withTypeParams = true
  )
  {
    var parameters = D(
      ("Instance Parameters", D(("Constraints", D(("Base Offset", ParamLeaf(0.5, "WALL_BASE_OFFSET_PARAM"))))))
    );
    if (withTypeParams)
    {
      parameters["Type Parameters"] = D(
        ("Identity Data", D(("Type Mark", ParamLeaf(typeMark, "ALL_MODEL_TYPE_MARK"))))
      );
    }
    return new DataObject
    {
      name = appId,
      applicationId = appId,
      id = appId,
      displayValue = new List<Base>
      {
        new Mesh
        {
          vertices = new List<double> { 0, 0, 0, 1, 0, 0, 1, 1, 0 },
          faces = new List<int> { 3, 0, 1, 2 },
          units = "m",
          applicationId = appId + "-mesh",
          id = appId + "-mesh",
        },
      },
      properties = D(("Parameters", parameters)),
      ["category"] = category,
      ["family"] = family,
      ["type"] = type,
    };
  }

  private static Collection Root(params Base[] elements)
  {
    var root = new Collection { name = "root", applicationId = "root" };
    root.elements.AddRange(elements);
    return root;
  }

  private static int ObjIdx(ArtefactBundle bundle, string appId) =>
    bundle.ObjectAppIds.Single(kv => kv.Value == appId).Key;

  // Walks the nested dict shape BuildProperties/SetNested produces from dotted eav paths.
  private static object? Nested(object? node, params string[] path)
  {
    foreach (var key in path)
    {
      if (node is not Dictionary<string, object?> dict || !dict.TryGetValue(key, out node))
      {
        return null;
      }
    }
    return node;
  }

  [Fact]
  public async Task SameType_SharesOneTypeRow_SplitFromInstanceEav()
  {
    var dir = TempDir();
    try
    {
      var stats = Migrate(
        Root(
          RevitObject("a1", "Basic Wall", "Generic - 200mm"),
          RevitObject("a2", "Basic Wall", "Generic - 200mm"),
          RevitObject("b1", "Basic Wall", "Generic - 300mm", typeMark: "W2")
        ),
        dir
      );
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);

      var typeProps = bundle.TypePropertiesByObject;
      var (a1, a2, b1) = (ObjIdx(bundle, "a1"), ObjIdx(bundle, "a2"), ObjIdx(bundle, "b1"));
      typeProps.Keys.Should().BeEquivalentTo(new[] { a1, a2, b1 });

      // Same type ⇒ literally the same parsed dictionary; different type ⇒ a different one.
      typeProps[a1].Should().BeSameAs(typeProps[a2]);
      typeProps[b1].Should().NotBeSameAs(typeProps[a1]);
      Nested(typeProps[a1], "properties", "Parameters", "Type Parameters", "Identity Data", "Type Mark")
        .Should()
        .Be("W1");
      Nested(typeProps[b1], "properties", "Parameters", "Type Parameters", "Identity Data", "Type Mark")
        .Should()
        .Be("W2");

      // Instance eav keeps instance params but no longer carries the type subtree.
      Nested(bundle.Properties[a1], "properties", "Parameters", "Instance Parameters", "Constraints", "Base Offset")
        .Should()
        .Be(0.5);
      Nested(bundle.Properties[a1], "properties", "Parameters", "Type Parameters").Should().BeNull();

      stats.RevitTypeKeys.Should().Be(2);
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
  public async Task LinkedModelSuffix_ScopesSameNamedTypesApart()
  {
    var dir = TempDir();
    try
    {
      var stats = Migrate(
        Root(
          RevitObject("uid1", "Basic Wall", "Generic - 200mm"),
          RevitObject("uid2_tdeadbeef", "Basic Wall", "Generic - 200mm", typeMark: "W9")
        ),
        dir
      );
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);

      var host = ObjIdx(bundle, "uid1");
      var linked = ObjIdx(bundle, "uid2_tdeadbeef");
      bundle.TypePropertiesByObject[host].Should().NotBeSameAs(bundle.TypePropertiesByObject[linked]);
      stats.RevitTypeKeys.Should().Be(2);
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
  public async Task NoneSentinel_KeepsTypeParamsInline()
  {
    var dir = TempDir();
    try
    {
      var stats = Migrate(Root(RevitObject("c1", family: "none", type: "Generic - 200mm")), dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);

      bundle.TypePropertiesByObject.Should().BeEmpty();
      var c1 = ObjIdx(bundle, "c1");
      Nested(bundle.Properties[c1], "properties", "Parameters", "Type Parameters", "Identity Data", "Type Mark")
        .Should()
        .Be("W1");
      stats.RevitTypeKeys.Should().Be(0);
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
  public async Task TypeKeyWithoutTypeSubtree_FlattensEverythingInline()
  {
    var dir = TempDir();
    try
    {
      var stats = Migrate(Root(RevitObject("d1", "Basic Wall", "Generic - 200mm", withTypeParams: false)), dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);

      bundle.TypePropertiesByObject.Should().BeEmpty();
      var d1 = ObjIdx(bundle, "d1");
      Nested(bundle.Properties[d1], "properties", "Parameters", "Instance Parameters", "Constraints", "Base Offset")
        .Should()
        .Be(0.5);
      stats.RevitTypeKeys.Should().Be(1); // candidate count: the key was derived even though no type row was emitted
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
  public async Task NullProperties_MigratesRootScalarsOnly()
  {
    var dir = TempDir();
    try
    {
      var obj = RevitObject("n1", "Basic Wall", "Generic - 200mm");
      obj.properties = null!; // v3 payloads can carry `"properties": null`

      var stats = Migrate(Root(obj), dir);
      var bundle = await ArtefactBundleReader.ReadAsync(dir, default);

      stats.Objects.Should().Be(1);
      var props = bundle.Properties[ObjIdx(bundle, "n1")];
      props["family"].Should().Be("Basic Wall");
      props.Should().NotContainKey("properties");
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
