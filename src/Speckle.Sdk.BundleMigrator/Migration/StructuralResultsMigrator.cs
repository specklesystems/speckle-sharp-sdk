using System.Globalization;
using Speckle.Objects.Utils;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.BundleMigrator.Migration;

/// <summary>
/// Migrates the v3 CSi root — <c>analysisResults</c> plus the model unit scalars — into the bundle (ENG-9076).
///
/// v3 stored each result type as its extractor's nested dictionary: grouping keys as dict levels, a
/// <c>Wrap:</c>-prefixed key adding a literal-named level. That is exactly the shape the v4 CSi producer
/// flattens, so this ports its descriptor mapping, extended from big-truck's 4 result types to all 8 the
/// spec supports. Results key objects by CSi element NAME while objects intern by applicationId — the
/// caller supplies the name→appId bridge; an unresolved name rides <c>location</c> (big-truck's fallback),
/// never minting a phantom object K.
/// </summary>
internal sealed class StructuralResultsMigrator(ObjectsArtifactPipeline pipeline, Stats stats)
{
  // One entry per v3 result type: the grouping axes, and which identity column each feeds.
  private sealed record ResultDescriptor(
    string ResultsKey,
    string ResultType,
    string? ElementKey, // axis resolved via name→appId (object-level identity)
    string? ElementNameKey, // axis → element_name (group-level identity: pier/spandrel name)
    string? LocationKey, // axis → location (story)
    string? PositionKey, // axis → position_label (Top/Bottom)
    IReadOnlyList<string> GroupingKeys,
    IReadOnlyDictionary<string, string>? ComponentRenames = null,
    bool DriftPivot = false
  );

  // v3 storyForces leaf keys are the ETABS database-table codes; the spec catalog names them semantically.
  private static readonly Dictionary<string, string> s_storyForceComponents = new(StringComparer.Ordinal)
  {
    ["P"] = "axial",
    ["VX"] = "majorShear",
    ["VY"] = "minorShear",
    ["T"] = "torsion",
    ["MX"] = "majorMoment",
    ["MY"] = "minorMoment",
  };

  private static readonly ResultDescriptor[] s_descriptors =
  [
    new("frameForces", "frameForce", "Elm", null, null, null, ["Elm", "LoadCase", "Wrap:ElmSta", "Wrap:StepNum"]),
    new("jointReact", "jointReaction", "Elm", null, null, null, ["Elm", "LoadCase", "Wrap:StepNum"]),
    new("baseReact", "baseReaction", null, null, null, null, ["LoadCase", "Wrap:StepNum"]),
    new("modalPeriodsAndFrequencies", "modalPeriod", null, null, null, null, ["LoadCase", "Wrap:Mode"]),
    new(
      "pierForces",
      "pierForce",
      null,
      "PierName",
      "StoryName",
      "Location",
      ["PierName", "StoryName", "LoadCase", "Wrap:Location"]
    ),
    new(
      "spandrelForces",
      "spandrelForce",
      null,
      "SpandrelName",
      "StoryName",
      "Location",
      ["SpandrelName", "StoryName", "LoadCase", "Wrap:Location"]
    ),
    new(
      "storyDrifts",
      "storyDrift",
      null,
      null,
      "Story",
      null,
      ["Story", "LoadCase", "Wrap:StepNum"],
      DriftPivot: true
    ),
    new(
      "storyForces",
      "storyForce",
      null,
      null,
      "Story",
      "Location",
      ["Story", "LoadCase", "Location"],
      s_storyForceComponents
    ),
  ];

  /// <summary>Carries the v3 CSi unit scalars into <c>eav.model</c> and notes the root keys that have no
  /// bundle home. v4 result rows are unitless, so these are the only record of what the magnitudes mean.</summary>
  public void MigrateModelScalars(Base root)
  {
    // forceUnits/temperatureUnits only ever came from the CSi builders, so their presence gates the pass.
    if (root["forceUnits"] is string || root["temperatureUnits"] is string)
    {
      AddUnit("units.distance", root["units"] as string); // already the converted Speckle unit string
      AddUnit("units.force", root["forceUnits"] as string); // verbatim eForce name: lb|kip|N|kN|kgf|tonf
      AddUnit("units.temperature", root["temperatureUnits"] as string); // verbatim eTemperature name: F|C
    }

    foreach (var key in (string[])["materialProxies", "sectionProxies"])
    {
      if (root[key] is not null || root["@" + key] is not null)
      {
        stats.Notes.Add($"root '{key}' present — not migrated (ENG-9076: no bundle home)");
      }
    }
  }

  private void AddUnit(string path, string? value)
  {
    // "NotApplicable" is a silently failed GetDatabaseUnits_2 call; "none" is Units.None — neither informs.
    if (string.IsNullOrEmpty(value) || value == "NotApplicable" || value == "none")
    {
      stats.Notes.Add($"model unit '{path}' skipped: '{value ?? "null"}'");
      return;
    }
    pipeline.AddModelProperty(path, value);
    stats.ModelUnitRows++;
  }

  public void MigrateResults(Base analysisResults, IReadOnlyDictionary<string, string> objectAppIdByName)
  {
    foreach (var member in analysisResults.GetMembers(DynamicBaseMemberType.Dynamic))
    {
      var descriptor = Array.Find(s_descriptors, d => d.ResultsKey == member.Key);
      if (descriptor is null)
      {
        stats.SkippedResultTypes++;
        stats.Notes.Add($"analysisResults '{member.Key}' skipped: no structural_results mapping");
        continue;
      }
      FlattenResultType(member.Value, descriptor, objectAppIdByName);
    }
  }

  private void FlattenResultType(
    object? node,
    ResultDescriptor d,
    IReadOnlyDictionary<string, string> objectAppIdByName
  )
  {
    var skippedValues = 0;
    Walk(
      node,
      d.GroupingKeys,
      0,
      new Dictionary<string, string>(StringComparer.Ordinal),
      (axes, leaf) =>
      {
        string? Axis(string? key) => key is not null && axes.TryGetValue(key, out var v) ? v : null;

        string? objectAppId = null;
        var location = Axis(d.LocationKey);
        if (d.ElementKey is not null && axes.TryGetValue(d.ElementKey, out var elementName))
        {
          if (objectAppIdByName.TryGetValue(elementName, out var appId))
          {
            objectAppId = appId;
          }
          else
          {
            // Analysis-only element (meshed sub-element, unsent object) — keep its raw name, as big-truck does.
            location = elementName;
            stats.StructuralElmFallbacks++;
          }
        }

        var loadCase = axes.TryGetValue("LoadCase", out var lc) ? lc : "";
        double? station =
          Axis("ElmSta") is { } sta
          && double.TryParse(sta, NumberStyles.Float, CultureInfo.InvariantCulture, out var staD)
            ? staD
            : null;
        int? step = null;
        if (
          Axis("StepNum") is { } stepStr
          && int.TryParse(stepStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stepI)
        )
        {
          step = stepI;
        }
        else if (
          Axis("Mode") is { } modeStr
          && int.TryParse(modeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var modeI)
        )
        {
          step = modeI;
        }

        if (d.DriftPivot)
        {
          // v3 stored Direction/Drift/Label/X/Y/Z as leaf values; the spec keeps Drift per Direction and
          // drops the rest (locked decision in speckle-bundle-spec/docs/rationale/structural-results.md).
          string? direction = null;
          double? drift = null;
          foreach (var kv in leaf)
          {
            if (kv.Key == "Direction")
            {
              direction = kv.Value as string;
            }
            else if (kv.Key == "Drift")
            {
              drift = AsDouble(kv.Value);
            }
          }
          if (direction is null || drift is null)
          {
            skippedValues++;
            return;
          }
          pipeline.AddStructuralResult(
            null,
            location,
            d.ResultType,
            loadCase,
            "drift",
            null,
            step,
            drift,
            positionLabel: direction
          );
          stats.StructuralResultRows++;
          return;
        }

        foreach (var kv in leaf)
        {
          if (AsDouble(kv.Value) is not { } value)
          {
            skippedValues++;
            continue;
          }
          var component = d.ComponentRenames?.GetValueOrDefault(kv.Key) ?? kv.Key;
          pipeline.AddStructuralResult(
            objectAppId,
            location,
            d.ResultType,
            loadCase,
            component,
            station,
            step,
            value,
            elementName: Axis(d.ElementNameKey),
            positionLabel: Axis(d.PositionKey)
          );
          stats.StructuralResultRows++;
        }
      }
    );
    if (skippedValues > 0)
    {
      stats.Notes.Add($"analysisResults '{d.ResultsKey}': {skippedValues} non-numeric leaf value(s) skipped");
    }
  }

  private static void Walk(
    object? node,
    IReadOnlyList<string> groupingKeys,
    int level,
    Dictionary<string, string> axes,
    Action<Dictionary<string, string>, IEnumerable<KeyValuePair<string, object?>>> onLeaf
  )
  {
    var entries = TryGetEntries(node);
    if (entries is null)
    {
      return; // scalar sibling (baseReact's GX/GY/GZ centroid doubles) or malformed node
    }
    if (level >= groupingKeys.Count)
    {
      onLeaf(axes, entries);
      return;
    }
    var key = groupingKeys[level];
    if (key.StartsWith("Wrap:", StringComparison.Ordinal))
    {
      // v3 wrapped this axis in a literal-named level: { "ElmSta": { "<value>": … } }.
      var actual = key["Wrap:".Length..];
      foreach (var entry in entries)
      {
        if (entry.Key != actual || TryGetEntries(entry.Value) is not { } wrapped)
        {
          continue;
        }
        foreach (var axisEntry in wrapped)
        {
          axes[actual] = axisEntry.Key;
          Walk(axisEntry.Value, groupingKeys, level + 1, axes, onLeaf);
        }
      }
      return;
    }
    foreach (var entry in entries)
    {
      axes[key] = entry.Key;
      Walk(entry.Value, groupingKeys, level + 1, axes, onLeaf);
    }
  }

  // The deserializer yields plain JSON objects as Dictionary<string, object?>, but a subtree the v3
  // serializer detached arrives as a Base — accept both.
  private static IEnumerable<KeyValuePair<string, object?>>? TryGetEntries(object? node) =>
    node switch
    {
      Base b => b.GetMembers(DynamicBaseMemberType.Dynamic),
      IReadOnlyDictionary<string, object?> d => d,
      _ => null,
    };

  private static double? AsDouble(object? value) =>
    value switch
    {
      double d => d,
      long l => l,
      _ => null,
    };
}
