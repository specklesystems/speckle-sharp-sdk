using System.Globalization;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>
/// Resolves an object's grouping path from a bundle's default scene view (the explorer/layer hierarchy) into an
/// ordered list of name segments — host-agnostic graph-walking over the neutral <see cref="ArtefactBundle"/>, with no
/// host types. A connector's native host builder maps the returned segments onto its own grouping (Rhino/AutoCAD nested
/// layers, Revit category, …) rather than duplicating the resolution logic. Each scene-view tier is either a
/// <c>"rel"</c> tier (group by a relation's target node, walking the node's parent ancestry via
/// <see cref="ArtefactNode.DefRef"/>) or an <c>"eav"</c> tier (group by an object property path, possibly dotted).
/// </summary>
public static class SceneViewResolver
{
  private const int ANCESTRY_GUARD = 64;

  /// <summary>The ordered grouping-name segments for one object under the bundle's default scene view, outermost→leaf.
  /// Empty when the bundle has no scene view or the object matches no tier (the caller falls back to a base group/layer).</summary>
  public static IReadOnlyList<string> Segments(ArtefactBundle bundle, int objK)
  {
    var segments = new List<string>();
    foreach (var tier in bundle.DefaultSceneView)
    {
      if (tier.Source == "rel")
      {
        if (
          int.TryParse(tier.Ref, NumberStyles.Integer, CultureInfo.InvariantCulture, out int relNum)
          && bundle.Relations.ObjectNodeByRel.TryGetValue(relNum, out var map)
          && map.TryGetValue(objK, out int nodeK)
        )
        {
          segments.AddRange(NodeAncestry(bundle.Nodes, nodeK)); // walk parents → nested collections/containers
        }
      }
      else if (tier.Source == "eav" && ResolveEav(bundle.Properties, objK, tier.Ref) is { Length: > 0 } val)
      {
        segments.Add(val);
      }
    }
    return segments;
  }

  /// <summary>Like <see cref="Segments"/> but pairs each segment with its source node's colour (argb) for node ("rel")
  /// tiers, so a host can colour layers, not just name them. eav tiers carry no node → null colour.</summary>
  public static IReadOnlyList<(string Name, int? Argb)> SegmentsWithColor(ArtefactBundle bundle, int objK)
  {
    var appearance = SegmentsWithAppearance(bundle, objK);
    var segments = new List<(string, int?)>(appearance.Count);
    foreach (var (name, argb, _) in appearance)
    {
      segments.Add((name, argb));
    }
    return segments;
  }

  /// <summary>Like <see cref="SegmentsWithColor"/> but resolves each node ("rel") segment's full appearance: colour
  /// prefers the node's NODE_HAS_COLOR edge (its COLOR node's argb) over the legacy argb stamped directly on the
  /// container row (pre-vocab bundles), and the segment carries its node K so a host can look up NODE_HAS_MATERIAL
  /// (<see cref="ArtefactRelations.MaterialByNode"/>) against whatever material it baked. eav tiers carry no node →
  /// null colour and null node K.</summary>
  public static IReadOnlyList<(string Name, int? Argb, int? NodeK)> SegmentsWithAppearance(
    ArtefactBundle bundle,
    int objK
  )
  {
    var segments = new List<(string, int?, int?)>();
    foreach (var tier in bundle.DefaultSceneView)
    {
      if (tier.Source == "rel")
      {
        if (
          int.TryParse(tier.Ref, NumberStyles.Integer, CultureInfo.InvariantCulture, out int relNum)
          && bundle.Relations.ObjectNodeByRel.TryGetValue(relNum, out var map)
          && map.TryGetValue(objK, out int nodeK)
        )
        {
          segments.AddRange(NodeAncestryWithAppearance(bundle, nodeK));
        }
      }
      else if (tier.Source == "eav" && ResolveEav(bundle.Properties, objK, tier.Ref) is { Length: > 0 } val)
      {
        segments.Add((val, null, null));
      }
    }
    return segments;
  }

  /// <summary>A node + its grouping ancestry (via <see cref="ArtefactNode.DefRef"/>), outermost→leaf, each paired with
  /// its <see cref="ArtefactNode.Argb"/> colour (null when the node has none).</summary>
  public static IReadOnlyList<(string Name, int? Argb)> NodeAncestryWithColor(
    IReadOnlyDictionary<int, ArtefactNode> nodes,
    int nodeK
  )
  {
    var result = new List<(string, int?)>();
    int? cursor = nodeK;
    int guard = 0;
    while (cursor is int c && nodes.TryGetValue(c, out var n) && guard++ < ANCESTRY_GUARD)
    {
      result.Insert(0, (n.Name is { Length: > 0 } nm ? nm : "unnamed", n.Argb));
      cursor = n.DefRef;
    }
    return result;
  }

  /// <summary>A node + its grouping ancestry, outermost→leaf, each with its resolved colour (NODE_HAS_COLOR edge
  /// first, the node's own argb as the pre-vocab fallback) and its node K (for NODE_HAS_MATERIAL lookups).</summary>
  public static IReadOnlyList<(string Name, int? Argb, int? NodeK)> NodeAncestryWithAppearance(
    ArtefactBundle bundle,
    int nodeK
  )
  {
    var result = new List<(string, int?, int?)>();
    int? cursor = nodeK;
    int guard = 0;
    while (cursor is int c && bundle.Nodes.TryGetValue(c, out var n) && guard++ < ANCESTRY_GUARD)
    {
      int? argb =
        bundle.Relations.ColorByNode.TryGetValue(c, out int colorK)
        && bundle.Nodes.TryGetValue(colorK, out var colorNode)
        && colorNode.Kind == NodeKind.Color
          ? colorNode.Argb
          : n.Argb;
      result.Insert(0, (n.Name is { Length: > 0 } nm ? nm : "unnamed", argb, c));
      cursor = n.DefRef;
    }
    return result;
  }

  /// <summary>A node + its grouping ancestry (via <see cref="ArtefactNode.DefRef"/>), outermost→leaf. Levels have no
  /// parent (single segment); collections/containers nest.</summary>
  public static IReadOnlyList<string> NodeAncestry(IReadOnlyDictionary<int, ArtefactNode> nodes, int nodeK)
  {
    var names = new List<string>();
    int? cursor = nodeK;
    int guard = 0;
    while (cursor is int c && nodes.TryGetValue(c, out var n) && guard++ < ANCESTRY_GUARD)
    {
      names.Insert(0, n.Name is { Length: > 0 } nm ? nm : "unnamed");
      cursor = n.DefRef;
    }
    return names;
  }

  /// <summary>Navigates an object's nested property dict by a (possibly dotted) eav path → its string value, or null.</summary>
  public static string? ResolveEav(
    IReadOnlyDictionary<int, Dictionary<string, object?>> properties,
    int objK,
    string path
  )
  {
    if (!properties.TryGetValue(objK, out var props))
    {
      return null;
    }
    var parts = path.Split('.');
    object? cursor = props;
    foreach (var part in parts)
    {
      if (cursor is Dictionary<string, object?> dict && dict.TryGetValue(part, out var next))
      {
        cursor = next;
      }
      else
      {
        return null;
      }
    }
    return cursor as string ?? cursor?.ToString();
  }
}
