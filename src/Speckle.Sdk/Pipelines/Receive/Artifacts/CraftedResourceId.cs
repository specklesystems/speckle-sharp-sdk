// SPIKE(wayfinder ticket 07) — THROWAWAY. Candidate format pinned for real by ticket 08.
namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>
/// The crafted resource identifier a bundle-only version carries in <c>version.referencedObject</c> instead of a
/// content hash: <c>projectId;modelId;versionId</c>. The <c>;</c> delimiter makes it non-hex-shaped (unambiguous
/// vs legacy object ids) and it avoids <c>/ ? # %</c> (ticket 02's URL-interpolation constraint).
/// </summary>
public readonly record struct CraftedResourceId(string ProjectId, string ModelId, string VersionId)
{
  public static bool TryParse(string? id, out CraftedResourceId parsed)
  {
    parsed = default;
    if (id is null || id.IndexOf(';') < 0)
    {
      return false;
    }
    var parts = id.Split(';');
    if (parts.Length != 3)
    {
      return false;
    }
    foreach (var p in parts)
    {
      if (p.Length == 0 || p.IndexOfAny(s_forbidden) >= 0)
      {
        return false;
      }
    }
    parsed = new CraftedResourceId(parts[0], parts[1], parts[2]);
    return true;
  }

  private static readonly char[] s_forbidden = ['/', '?', '#', '%', ' '];

  public override string ToString() => $"{ProjectId};{ModelId};{VersionId}";
}
