using System.Globalization;
using System.Text.RegularExpressions;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Linq;

namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// One flat property row destined for the eav.duckdb <c>properties</c> table.
/// Mirrors the server's EavRow (packages/shared/src/filtering/eavExtraction.ts).
/// </summary>
public sealed record EavRow(
  string ObjectId,
  string Path,
  string ValueText,
  double? ValueNum,
  string Type, // "string" | "number" | "boolean"
  string? Units,
  string? InternalDefinitionName
);

/// <summary>
/// EAV (Entity-Attribute-Value) property extraction, ported from the server's
/// <c>packages/shared/src/filtering/eavExtraction.ts</c> so the client can
/// produce eav.duckdb at send time instead of the server deriving it by
/// re-parsing the uploaded NDJSON. Behaviour-parity with the TS implementation
/// is the goal — quirks are intentionally preserved.
/// </summary>
public static class EavExtraction
{
  private const int MAX_DEPTH = 10;

  /// <summary>
  /// UNIVERSAL top-level keys under <c>properties</c> excluded from EAV regardless
  /// of source application — render/document noise that's either available elsewhere
  /// (viewer materials) or pure repetition, safe to drop for any source. This set is
  /// applied by the eav writers (binary objects.duckdb + envelope viewer.duckdb) to
  /// EVERY object, so it must stay source-agnostic. App-specific author categories
  /// belong in a source-scoped set instead (e.g. <see cref="RevitExcludedTopLevelProperties"/>),
  /// applied only to objects of that source — NOT here.
  /// </summary>
  public static readonly ISet<string> DefaultExcludedTopLevelProperties = new HashSet<string>(StringComparer.Ordinal)
  {
    "Autodesk Material",
    "Document",
  };

  /// <summary>
  /// Revit-specific top-level categories to skip — author tabs the team has decided
  /// not to index (high-volume, low query value: "Material" ~1.6M rows, "Revit
  /// Material" ~790k, "Category" ~560k, "Phase Created" ~520k on a typical model).
  /// Applied ONLY to Revit-sourced elements (ODA class <c>LcRevit*</c>) at READ time,
  /// so the costly per-property ODA read is never paid; NOT a writer exclusion, so it
  /// never drops a same-named category on non-Revit federated content (DGN/IFC/CAD).
  /// Curate this list as the indexing contract for Revit evolves.
  /// (Names must match the Navisworks category display name exactly. "SketchPlane" /
  /// "GeometryCurve" were 0-row no-ops on the CP2 model — kept for other models.)
  /// </summary>
  public static readonly ISet<string> RevitExcludedTopLevelProperties = new HashSet<string>(StringComparer.Ordinal)
  {
    "Line Style",
    "SketchPlane",
    "GeometryCurve",
    "Element ID",
    "Category",
    "CreatedPhaseId",
    "Id",
    "Material",
    "Revit Material",
    "Orientation",
    "ParametersMap",
    "Phase Created",
  };

  /// <summary>Root-level fields to index (outside of `properties`).</summary>
  private static readonly string[] s_rootScalarFields =
  [
    "name",
    "type",
    "family",
    "category",
    "level",
    "units",
    "speckle_type",
    "applicationId",
    "definitionId",
    "ifcType",
    "path",
  ];

  /// <summary>Rejects UUID-like strings ("a-b-c" shapes) from numeric inference.</summary>
  private static readonly Regex s_uuidLike = new(".-.-", RegexOptions.Compiled);

  // double.IsFinite isn't available on netstandard2.0
  private static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

  /// <summary>
  /// Extract EAV rows from a parsed Speckle object. Dispatches on speckle_type:
  /// InstanceProxy → root scalars + transform rows; DataObject subtypes (or
  /// missing type) → full extraction; Collection/Layer → root scalars +
  /// properties + elements; everything else (geometry primitives, chunks,
  /// blobs) → no rows.
  /// </summary>
  /// <param name="excludedTopLevelProperties">
  /// Optional set of top-level keys under <c>properties</c> to skip entirely
  /// (the whole subtree is not walked). Used by the 4.0 binary path to drop
  /// high-volume / redundant Revit categories (e.g. "Autodesk Material",
  /// "Document"). Null/empty (the envelope path) extracts everything as before.
  /// </param>
  public static List<EavRow> FlattenObjectProperties(
    string objectId,
    JObject obj,
    ISet<string>? excludedTopLevelProperties = null
  )
  {
    var speckleType = obj["speckle_type"]?.Type == JTokenType.String ? (string)obj["speckle_type"]! : "";

    if (speckleType.Contains("InstanceProxy"))
    {
      return ExtractInstanceProxy(objectId, obj);
    }
    if (speckleType.Length == 0 || speckleType.StartsWith("Objects.Data."))
    {
      return ExtractDataObject(objectId, obj, excludedTopLevelProperties);
    }
    if (IsCollectionType(speckleType))
    {
      return ExtractCollection(objectId, obj, excludedTopLevelProperties);
    }
    return [];
  }

  /// <summary>
  /// Cheap pre-check on the wire-level speckle_type so callers can skip JSON
  /// parsing entirely for objects that produce no EAV rows (meshes, chunks…).
  /// Keep in sync with the dispatch in <see cref="FlattenObjectProperties"/>.
  /// </summary>
  public static bool ProducesRows(string speckleType) =>
    speckleType.Length == 0
    || speckleType.Contains("InstanceProxy")
    || speckleType.StartsWith("Objects.Data.")
    || IsCollectionType(speckleType);

  private static bool IsCollectionType(string speckleType)
  {
    if (speckleType.EndsWith(".Layer"))
    {
      return true;
    }
    return speckleType.Contains("Collection");
  }

  private static List<EavRow> ExtractDataObject(
    string objectId,
    JObject obj,
    ISet<string>? excludedTopLevelProperties
  )
  {
    var rows = new List<EavRow>();

    // 1. Root-level scalar fields
    ExtractRootScalars(objectId, obj, rows);

    // 2. Recursive walk of `properties`
    var props = obj["properties"] as JObject;
    if (props != null)
    {
      WalkProperties(objectId, props, "properties", 0, rows, excludedTopLevelProperties);
    }

    // 3. Material Quantities (Revit) — inside `properties`, special structure
    if (props?["Material Quantities"] is JObject matQuants)
    {
      ExtractMaterialQuantities(objectId, matQuants, rows);
    }

    // 4. LocationPoint — Revit's per-element placement (X/Y/Z world coords)
    if (obj["location"] is JObject loc)
    {
      ExtractLocation(objectId, loc, rows);
    }

    // 5. DisplayValue references — one row per entry's referencedId (or `id`)
    if (obj["displayValue"] is JArray dv)
    {
      ExtractDisplayValueRefs(objectId, dv, rows);
    }

    // 6. Elements — child references, stored as one JSON array in value_text
    if (obj["elements"] is JArray elements)
    {
      ExtractElementsRefs(objectId, elements, rows);
    }

    return rows;
  }

  private static List<EavRow> ExtractInstanceProxy(string objectId, JObject obj)
  {
    var rows = new List<EavRow>();

    ExtractRootScalars(objectId, obj, rows);

    // proxy.transform.{tx,ty,tz} from the row-major 4×4 matrix translation
    // (indices 3, 7, 11), plus the full matrix verbatim as JSON.
    var units = obj["units"]?.Type == JTokenType.String ? (string)obj["units"]! : null;
    if (obj["transform"] is JArray transform && transform.Count == 16)
    {
      if (TryGetFiniteNumber(transform[3], out var tx))
      {
        rows.Add(MakeRow(objectId, "proxy.transform.tx", new JValue(tx), units, null));
      }
      if (TryGetFiniteNumber(transform[7], out var ty))
      {
        rows.Add(MakeRow(objectId, "proxy.transform.ty", new JValue(ty), units, null));
      }
      if (TryGetFiniteNumber(transform[11], out var tz))
      {
        rows.Add(MakeRow(objectId, "proxy.transform.tz", new JValue(tz), units, null));
      }
      rows.Add(
        MakeRow(objectId, "proxy.transform.matrix", new JValue(transform.ToString(Formatting.None)), units, null)
      );
    }

    return rows;
  }

  private static List<EavRow> ExtractCollection(
    string objectId,
    JObject obj,
    ISet<string>? excludedTopLevelProperties
  )
  {
    var rows = new List<EavRow>();

    ExtractRootScalars(objectId, obj, rows);

    // Only RootCollection carries `properties` (model-wide metadata)
    if (obj["properties"] is JObject props)
    {
      WalkProperties(objectId, props, "properties", 0, rows, excludedTopLevelProperties);
    }

    if (obj["elements"] is JArray elements)
    {
      ExtractElementsRefs(objectId, elements, rows);
    }

    return rows;
  }

  private static void ExtractRootScalars(string objectId, JObject obj, List<EavRow> rows)
  {
    foreach (var field in s_rootScalarFields)
    {
      var val = obj[field];
      if (val == null || val.Type == JTokenType.Null)
      {
        continue;
      }
      if (val is JObject or JArray)
      {
        continue;
      }
      rows.Add(MakeRow(objectId, field, (JValue)val, null, null));
    }
  }

  private static void ExtractLocation(string objectId, JObject loc, List<EavRow> rows)
  {
    var units = loc["units"]?.Type == JTokenType.String ? (string)loc["units"]! : null;
    foreach (var axis in (string[])["x", "y", "z"])
    {
      if (TryGetFiniteNumber(loc[axis], out var v))
      {
        rows.Add(MakeRow(objectId, $"location.{axis}", new JValue(v), units, null));
      }
    }
  }

  private static void ExtractDisplayValueRefs(string objectId, JArray dv, List<EavRow> rows)
  {
    for (var i = 0; i < dv.Count; i++)
    {
      if (dv[i] is not JObject e)
      {
        continue;
      }
      // Reference shape: { speckle_type: "reference", referencedId: "…" }
      // Inlined shape: full object with its own `id` (Speckle ids ARE content hashes)
      string? refId = null;
      if (e["referencedId"]?.Type == JTokenType.String)
      {
        refId = (string)e["referencedId"]!;
      }
      else if (e["id"]?.Type == JTokenType.String)
      {
        refId = (string)e["id"]!;
      }
      if (refId != null)
      {
        rows.Add(MakeRow(objectId, $"displayValue.{i}.referencedId", new JValue(refId), null, null));
      }
    }
  }

  private static void ExtractElementsRefs(string objectId, JArray elements, List<EavRow> rows)
  {
    var refIds = new List<string>();
    foreach (var entry in elements)
    {
      if (entry is not JObject e)
      {
        continue;
      }
      if (e["referencedId"]?.Type == JTokenType.String)
      {
        refIds.Add((string)e["referencedId"]!);
      }
      else if (e["id"]?.Type == JTokenType.String)
      {
        refIds.Add((string)e["id"]!);
      }
    }
    if (refIds.Count == 0)
    {
      return;
    }
    var json = new JArray(refIds).ToString(Formatting.None);
    rows.Add(MakeRow(objectId, "elements", new JValue(json), null, null));
  }

  private static void WalkProperties(
    string objectId,
    JObject obj,
    string prefix,
    int depth,
    List<EavRow> rows,
    ISet<string>? excludedTopLevelProperties = null
  )
  {
    if (depth >= MAX_DEPTH)
    {
      return;
    }

    foreach (var prop in obj.Properties())
    {
      var key = prop.Name;
      var val = prop.Value;
      var path = prefix + "." + key;

      // Drop excluded top-level categories (e.g. "Autodesk Material",
      // "Document") wholesale — skip the entire subtree, not just the leaves.
      if (depth == 0 && excludedTopLevelProperties != null && excludedTopLevelProperties.Contains(key))
      {
        continue;
      }

      if (val.Type == JTokenType.Null)
      {
        continue;
      }
      if (val is JArray)
      {
        continue;
      }

      if (val is not JObject asRecord)
      {
        // Leaf primitive
        rows.Add(MakeRow(objectId, path, (JValue)val, null, null));
        continue;
      }

      // It's an object — check for parameter pattern {name, value}
      if (IsParameter(asRecord))
      {
        var paramVal = asRecord["value"];
        if (paramVal == null || paramVal.Type == JTokenType.Null)
        {
          continue;
        }
        if (paramVal is JObject or JArray)
        {
          continue; // value is object/array, skip
        }

        var units = asRecord["units"]?.Type == JTokenType.String ? (string)asRecord["units"]! : null;
        var idn =
          asRecord["internalDefinitionName"]?.Type == JTokenType.String
            ? (string)asRecord["internalDefinitionName"]!
            : null;

        rows.Add(MakeRow(objectId, path, (JValue)paramVal, units, idn));
        continue;
      }

      // Skip Structure layer definitions under Type Parameters
      if (key == "Structure" && prefix.EndsWith(".Type Parameters"))
      {
        continue;
      }

      // Skip Material Quantities — handled separately
      if (key == "Material Quantities")
      {
        continue;
      }

      // Regular nested object — recurse (exclusions apply at depth 0 only)
      WalkProperties(objectId, asRecord, path, depth + 1, rows);
    }
  }

  /// <summary>
  /// Material quantity rows from Revit's `Material Quantities` dict.
  /// Path: `properties.Material Quantities.{category}.{materialName}.{area|volume}`
  /// </summary>
  private static void ExtractMaterialQuantities(string objectId, JObject matQuants, List<EavRow> rows)
  {
    foreach (var matProp in matQuants.Properties())
    {
      if (matProp.Value is not JObject mat)
      {
        continue;
      }

      var category = mat["materialCategory"]?.Type == JTokenType.String ? (string)mat["materialCategory"]! : "Unknown";

      AppendQuantity(objectId, mat, "area", category, matProp.Name, rows);
      AppendQuantity(objectId, mat, "volume", category, matProp.Name, rows);
    }
  }

  private static void AppendQuantity(
    string objectId,
    JObject mat,
    string kind,
    string category,
    string matName,
    List<EavRow> rows
  )
  {
    if (mat[kind] is not JObject q)
    {
      return;
    }
    var value = q["value"];
    if (value == null || value.Type == JTokenType.Null || value is JObject or JArray)
    {
      return;
    }
    var units = q["units"]?.Type == JTokenType.String ? (string)q["units"]! : null;
    rows.Add(MakeRow(objectId, $"properties.Material Quantities.{category}.{matName}.{kind}", (JValue)value, units, null));
  }

  /// <summary>Check if an object is a parameter: has both `name` and `value` keys.</summary>
  private static bool IsParameter(JObject obj) => obj.ContainsKey("name") && obj.ContainsKey("value");

  private static EavRow MakeRow(string objectId, string path, JValue value, string? units, string? internalDefinitionName)
  {
    var valueText = ToText(value);
    var inferredType = InferType(value);
    double? valueNum = inferredType == "number" ? ToNum(value) : null;

    return new EavRow(objectId, path, valueText, valueNum, inferredType, units, internalDefinitionName);
  }

  /// <summary>JS String(value) semantics: lowercase booleans, invariant numbers.</summary>
  private static string ToText(JValue value) =>
    value.Value switch
    {
      bool b => b ? "true" : "false",
      string s => s,
      double d => d.ToString("R", CultureInfo.InvariantCulture),
      float f => f.ToString("R", CultureInfo.InvariantCulture),
      decimal m => m.ToString(CultureInfo.InvariantCulture),
      IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
      null => "null",
      var other => other.ToString() ?? "",
    };

  private static string InferType(JValue value)
  {
    if (value.Type == JTokenType.Boolean)
    {
      return "boolean";
    }
    if (value.Type == JTokenType.String)
    {
      var lower = ((string)value.Value!).ToLowerInvariant();
      if (lower is "true" or "false")
      {
        return "boolean";
      }
    }

    if (value.Type is JTokenType.Integer)
    {
      return "number";
    }
    if (value.Type is JTokenType.Float)
    {
      var d = Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);
      return IsFinite(d) ? "number" : "string";
    }
    if (value.Type == JTokenType.String)
    {
      var trimmed = ((string)value.Value!).Trim();
      if (trimmed.Length == 0)
      {
        return "string";
      }
      // Reject UUID-like strings with multiple dashes
      if (s_uuidLike.IsMatch(trimmed))
      {
        return "string";
      }
      if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var num) && IsFinite(num))
      {
        return "number";
      }
    }

    return "string";
  }

  private static double? ToNum(JValue value)
  {
    switch (value.Type)
    {
      case JTokenType.Integer:
        return Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);
      case JTokenType.Float:
      {
        var d = Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);
        return IsFinite(d) ? d : null;
      }
      case JTokenType.String:
      {
        var trimmed = ((string)value.Value!).Trim();
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var num) && IsFinite(num))
        {
          return num;
        }
        return null;
      }
      default:
        return null;
    }
  }

  private static bool TryGetFiniteNumber(JToken? token, out double value)
  {
    value = 0;
    if (token == null)
    {
      return false;
    }
    if (token.Type is not (JTokenType.Integer or JTokenType.Float))
    {
      return false;
    }
    value = Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture);
    return IsFinite(value);
  }

  // ───────────────────────────────────────────────────────────────────────────
  // Native (Dictionary) flatten path — Speckle 4.0 binary Navis (phase 2).
  // Streams EAV rows straight from the extracted Dictionary<string,object?> tree
  // with NO NavisworksObject → JObject.FromObject round-trip. Mirrors the JObject
  // WalkProperties semantics above (paths, parameter {name,value}+units/idn,
  // Material Quantities, MAX_DEPTH, depth-0 exclusions) so eav content matches,
  // minus whatever the caller cleaned out of the dictionary upstream.
  // ───────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// Flattens an object's native property tree into <paramref name="rows"/> in
  /// place. <paramref name="rootScalars"/> are bare top-level fields (e.g.
  /// speckle_type, name, units); <paramref name="properties"/> is walked under the
  /// <c>properties.</c> prefix. Same row shape/paths as the JObject path.
  /// </summary>
  public static void FlattenProperties(
    string objectId,
    IReadOnlyDictionary<string, object?> properties,
    IEnumerable<KeyValuePair<string, object?>>? rootScalars,
    ISet<string>? excludedTopLevelProperties,
    ICollection<EavRow> rows
  )
  {
    if (rootScalars != null)
    {
      foreach (var kvp in rootScalars)
      {
        if (IsScalar(kvp.Value))
        {
          rows.Add(MakeRowNative(objectId, kvp.Key, kvp.Value!, null, null));
        }
      }
    }

    WalkPropertiesNative(objectId, properties, "properties", 0, rows, excludedTopLevelProperties);

    if (
      properties.TryGetValue("Material Quantities", out var mq)
      && mq is IReadOnlyDictionary<string, object?> matQuants
    )
    {
      ExtractMaterialQuantitiesNative(objectId, matQuants, rows);
    }
  }

  private static readonly IReadOnlyDictionary<string, object?> s_emptyRecord = new Dictionary<string, object?>();

  /// <summary>
  /// Recognises any string-keyed dictionary as a record for the EAV walk — crucially
  /// including strongly-typed nested dictionaries like
  /// <c>Dictionary&lt;string, Dictionary&lt;string, object?&gt;&gt;</c> produced by the Revit
  /// parameter extractor (Instance/Type/System parameter groups). The fast path returns the
  /// dict as-is; otherwise it normalises a non-generic <see cref="System.Collections.IDictionary"/>
  /// (string keys) to <c>IReadOnlyDictionary&lt;string, object?&gt;</c>.
  ///
  /// Without this, those typed nested dicts fail a plain
  /// <c>val is IReadOnlyDictionary&lt;string, object?&gt;</c> check (IReadOnlyDictionary is invariant
  /// in TValue), so the ENTIRE parameter subtree was silently dropped from eav. The fast path keeps
  /// uniform <c>&lt;string, object?&gt;</c> dictionaries (e.g. Navis) allocation-free and unchanged.
  /// </summary>
  private static bool TryAsStringKeyedRecord(object val, out IReadOnlyDictionary<string, object?> record)
  {
    switch (val)
    {
      case IReadOnlyDictionary<string, object?> r:
        record = r;
        return true;
      case System.Collections.IDictionary d:
        var map = new Dictionary<string, object?>(d.Count, StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry e in d)
        {
          if (e.Key is string k)
          {
            map[k] = e.Value;
          }
        }
        record = map;
        return true;
      default:
        record = s_emptyRecord;
        return false;
    }
  }

  private static void WalkPropertiesNative(
    string objectId,
    IReadOnlyDictionary<string, object?> obj,
    string prefix,
    int depth,
    ICollection<EavRow> rows,
    ISet<string>? excludedTopLevelProperties
  )
  {
    if (depth >= MAX_DEPTH)
    {
      return;
    }

    foreach (var kvp in obj)
    {
      var key = kvp.Key;
      var val = kvp.Value;

      if (depth == 0 && excludedTopLevelProperties != null && excludedTopLevelProperties.Contains(key))
      {
        continue;
      }
      if (val == null)
      {
        continue;
      }

      var path = prefix + "." + key;

      if (TryAsStringKeyedRecord(val, out var asRecord))
      {
        // Parameter pattern { name, value } → single row at this path.
        if (asRecord.ContainsKey("name") && asRecord.TryGetValue("value", out var paramVal))
        {
          if (!IsScalar(paramVal))
          {
            continue;
          }
          string? units = asRecord.TryGetValue("units", out var u) && u is string us ? us : null;
          string? idn =
            asRecord.TryGetValue("internalDefinitionName", out var i) && i is string isn ? isn : null;
          rows.Add(MakeRowNative(objectId, path, paramVal!, units, idn));
          continue;
        }

        // Parity with the JObject walk's special-cases.
        if (key == "Structure" && prefix.EndsWith(".Type Parameters", StringComparison.Ordinal))
        {
          continue;
        }
        if (key == "Material Quantities")
        {
          continue; // handled separately
        }

        WalkPropertiesNative(objectId, asRecord, path, depth + 1, rows, null);
        continue;
      }

      if (IsScalar(val))
      {
        rows.Add(MakeRowNative(objectId, path, val, null, null));
      }
      // non-scalar, non-dictionary (arrays, etc.) → skipped, as in the JObject walk.
    }
  }

  private static void ExtractMaterialQuantitiesNative(
    string objectId,
    IReadOnlyDictionary<string, object?> matQuants,
    ICollection<EavRow> rows
  )
  {
    foreach (var matProp in matQuants)
    {
      if (matProp.Value is not IReadOnlyDictionary<string, object?> mat)
      {
        continue;
      }
      var category = mat.TryGetValue("materialCategory", out var c) && c is string cs ? cs : "Unknown";
      AppendQuantityNative(objectId, mat, "area", category, matProp.Key, rows);
      AppendQuantityNative(objectId, mat, "volume", category, matProp.Key, rows);
    }
  }

  private static void AppendQuantityNative(
    string objectId,
    IReadOnlyDictionary<string, object?> mat,
    string kind,
    string category,
    string matName,
    ICollection<EavRow> rows
  )
  {
    if (!mat.TryGetValue(kind, out var qObj) || qObj is not IReadOnlyDictionary<string, object?> q)
    {
      return;
    }
    if (!q.TryGetValue("value", out var value) || !IsScalar(value))
    {
      return;
    }
    string? units = q.TryGetValue("units", out var u) && u is string us ? us : null;
    rows.Add(
      MakeRowNative(objectId, $"properties.Material Quantities.{category}.{matName}.{kind}", value!, units, null)
    );
  }

  private static bool IsScalar(object? v) =>
    v is bool or string or sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

  private static EavRow MakeRowNative(string objectId, string path, object value, string? units, string? idn)
  {
    var type = InferTypeNative(value);
    double? num = type == "number" ? ToNumNative(value) : null;
    return new EavRow(objectId, path, ToTextNative(value), num, type, units, idn);
  }

  private static string ToTextNative(object value) =>
    value switch
    {
      bool b => b ? "true" : "false",
      string s => s,
      double d => d.ToString("R", CultureInfo.InvariantCulture),
      float f => f.ToString("R", CultureInfo.InvariantCulture),
      decimal m => m.ToString(CultureInfo.InvariantCulture),
      IFormattable fmt => fmt.ToString(null, CultureInfo.InvariantCulture),
      _ => value.ToString() ?? "",
    };

  private static string InferTypeNative(object value)
  {
    switch (value)
    {
      case bool:
        return "boolean";
      case float f:
        return IsFinite(f) ? "number" : "string";
      case double d:
        return IsFinite(d) ? "number" : "string";
      case sbyte or byte or short or ushort or int or uint or long or ulong or decimal:
        return "number";
      case string s:
      {
        var lower = s.ToLowerInvariant();
        if (lower is "true" or "false")
        {
          return "boolean";
        }
        var trimmed = s.Trim();
        if (trimmed.Length == 0 || s_uuidLike.IsMatch(trimmed))
        {
          return "string";
        }
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var num) && IsFinite(num)
          ? "number"
          : "string";
      }
      default:
        return "string";
    }
  }

  private static double? ToNumNative(object value)
  {
    switch (value)
    {
      case string s:
        return double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var num) && IsFinite(num)
          ? num
          : null;
      case IConvertible c: // numeric scalars only reach here (bool/string handled above / by type)
      {
        var d = c.ToDouble(CultureInfo.InvariantCulture);
        return IsFinite(d) ? d : null;
      }
      default:
        return null;
    }
  }
}
