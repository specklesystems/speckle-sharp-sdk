#if NETSTANDARD2_0 || NET8_0_OR_GREATER
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
#endif
