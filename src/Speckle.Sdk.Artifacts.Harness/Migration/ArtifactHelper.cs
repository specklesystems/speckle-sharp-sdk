using System.Collections;
using System.Globalization;
using Speckle.DoubleNumerics;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Instances;

namespace Speckle.Sdk.Artifacts.Harness.Migration;

/// <summary>
/// Stateless helpers shared by the v2 and v3 producers
/// </summary>
internal sealed class ArtifactHelper
{
  // Root-level proxy collections. A v2 commit never emitted any of these, so the presence of one is proof of v3.
  private static readonly string[] ProxyKeys =
  [
    "instanceDefinitionProxies",
    "renderMaterialProxies",
    "colorProxies",
    "levelProxies",
    "groupProxies",
  ];

  /// <summary>
  /// True when <paramref name="root"/> is a v3 graph: either it declares <c>version</c> 3, or it carries any
  /// root-level proxy collection. Everything else is treated as v2.
  /// </summary>
  public bool IsV3(Base root) => IsVersion3(root["version"]) || HasAnyRootProxy(root);

  // The version lands in a dynamic member, so it can arrive as any numeric type or as a string.
  private static bool IsVersion3(object? version) =>
    version switch
    {
      long l => l == 3,
      double d => d is 3,
      string s => s == "3",
      _ => false,
    };

  private static bool HasAnyRootProxy(Base root) =>
    ProxyKeys.Any(key => root[key] is not null || root["@" + key] is not null);

  // ── keys (applicationId-keyed; null → stable per-object key) ─────────────────────────

  public string Aid(Base b) => b.applicationId ?? "spk:" + b.id;

  public string CollectionKey(Collection col) => col.applicationId ?? "coll:" + col.id;

  public string DefinitionKey(InstanceDefinitionProxy idp) => idp.applicationId ?? idp.name;

  public string MaterialKey(RenderMaterialProxy rmp) =>
    rmp.applicationId ?? rmp.value.applicationId ?? "mat:" + rmp.value.diffuse.ToString(CultureInfo.InvariantCulture);

  public string LevelKey(Base lvl, string? name) => lvl.applicationId ?? "lvl:" + (name ?? lvl.id);

  public string CollectionSubtype(Collection col)
  {
#pragma warning disable CS0618 // Type or member is obsolete
    var ct = col.collectionType;
#pragma warning restore CS0618 // Type or member is obsolete
    return string.IsNullOrEmpty(ct) ? col.speckle_type.Split('.')[^1] : ct;
  }

  // ── property extraction ─────────────────────────────────────────────────────────────

  public (
    IReadOnlyDictionary<string, object?> props,
    IEnumerable<KeyValuePair<string, object?>> rootScalars,
    string? typeKey
  ) ExtractProperties(Base obj)
  {
    IReadOnlyDictionary<string, object?> props = obj is DataObject dobj
      ? dobj.properties
      : obj.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic);

    // `level` is the level NAME and lives at the top level (not under properties), so it must be listed here.
    var rootScalars = new List<KeyValuePair<string, object?>>
    {
      new("speckle_type", obj.speckle_type),
      new("name", obj["name"]),
      new("units", obj["units"]),
      new("category", obj["category"]),
      new("family", obj["family"]),
      new("type", obj["type"]),
      new("level", ReadLevelScalar(obj)),
    };

    var typeKey = obj["typeId"] as string ?? (props.TryGetValue("typeId", out var tk) ? tk as string : null);

    return (props, rootScalars, typeKey);
  }

  // v2 attached the Level object itself; v3 sends the name. EAV wants the name either way — a Base would be
  // dropped as a non-scalar.
  private object? ReadLevelScalar(Base obj) =>
    ReadV2Level(obj) is { } lvl ? lvl["name"] : obj["level"] ?? obj["@level"];

  // ── member readers ──────────────────────────────────────────────────────────────────

  public bool IsGeometry(Base b) => b.speckle_type.StartsWith("Objects.Geometry.", StringComparison.Ordinal);

  /// <summary>Normalizes in place; false when there is no direction to normalize to.</summary>
  public bool TryNormalize(Vector v)
  {
    var length = v.Length;
    if (length is 0 || double.IsNaN(length) || double.IsInfinity(length))
    {
      return false;
    }
    v.Normalize();
    return true;
  }

  public RenderMaterial? ReadEmbeddedMaterial(Base host) =>
    (host["renderMaterial"] ?? host["@renderMaterial"]) as RenderMaterial;

  /// <summary>The level attached to a v2 buildElement. <c>level</c> was also used for unrelated data, so a
  /// non-<see cref="Base"/> value reads as null and the caller skips it.</summary>
  public Base? ReadV2Level(Base host) => (host["level"] ?? host["@level"]) as Base;

  /// <summary>Elevation off a level object. The deserializer only ever yields double or long, so anything
  /// else (including null) means the level has no usable elevation.</summary>
  public double? ReadElevation(Base host) =>
    host["elevation"] switch
    {
      double d => d,
      long l => l,
      _ => null,
    };

  /// <summary>A detached list may sit under the typed key or the `@`-prefixed dynamic key; takes the first
  /// non-empty one.</summary>
  public IEnumerable<Base> GetBaseList(Base b, string key)
  {
    var raw = NonEmpty(b[key]) ?? NonEmpty(b["@" + key]);
    if (raw is IEnumerable seq and not string)
    {
      foreach (var item in seq)
      {
        if (item is Base bs)
        {
          yield return bs;
        }
      }
    }
  }

  private static object? NonEmpty(object? v) => v is ICollection c && c.Count == 0 ? null : v;

  // ── raw-encoded solids (SOLID rel) ───────────────────────────────────────────────────

  // 3dm (Rhino) and sat (Autocad) are the native solid formats we migrate; others (e.g. dwg) are skipped.
  public bool IsMigratableSolidFormat(string? format) =>
    format is RawEncodingFormats.RHINO_3DM or RawEncodingFormats.ACAD_SAT;

  // Reads the lossless raw encoding off a raw-encoded geometry (encodedValue) or a host wrapper (rawEncoding).
  // Typed casts only — the v3 graph deserializes into these registered SDK types.
  public RawEncoding? TryReadRawEncoding(Base obj) =>
    obj switch
    {
      IRawEncodedObject r => r.encodedValue,
      RhinoObject ro => ro.rawEncoding,
      AutocadObject ao => ao.rawEncoding,
      _ => null,
    };

  public double[] Flatten(Matrix4x4 m) =>
    new[]
    {
      m.M11,
      m.M12,
      m.M13,
      m.M14,
      m.M21,
      m.M22,
      m.M23,
      m.M24,
      m.M31,
      m.M32,
      m.M33,
      m.M34,
      m.M41,
      m.M42,
      m.M43,
      m.M44,
    };
}
