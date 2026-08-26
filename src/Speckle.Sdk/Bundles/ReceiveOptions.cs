namespace Speckle.Sdk.Bundles;

/// <summary>Options for <see cref="Api.Operations.Receive3(Speckle.Sdk.Credentials.Account, string, string, string, ReceiveOptions?, CancellationToken)"/>.</summary>
/// <param name="IncludeGeometry">
/// Download the geometry shards (<c>*.geometries*.parquet</c>). Default <see langword="true"/>. They are only parsed
/// when <see cref="Model.Geometries"/> is first accessed, so leaving this on costs download time and disk, not memory.
/// Set to <see langword="false"/> for a properties-only receive; <see cref="Model.Geometries"/> then throws.
/// </param>
/// <param name="IncludeViewerArtifacts">
/// Also download the viewer's own binary files (<c>*.viewer.dat</c>, <c>*.viewer.idx</c>). They are the largest files in
/// a bundle and nothing in the SDK reads them; default <see langword="false"/>. Set to <see langword="true"/> only when
/// you intend to hand <see cref="Model.Directory"/> to a viewer.
/// </param>
public sealed record ReceiveOptions(bool IncludeGeometry = true, bool IncludeViewerArtifacts = false)
{
  public static readonly ReceiveOptions Default = new();

  private static readonly string[] s_viewerSuffixes = [".viewer.dat", ".viewer.idx"];

  /// <summary>Whether an artefact file with this basename should be downloaded under these options.</summary>
  public bool ShouldDownload(string fileName)
  {
    if (!IncludeGeometry && IsGeometryShard(fileName))
    {
      return false;
    }
    if (!IncludeViewerArtifacts)
    {
      foreach (var suffix in s_viewerSuffixes)
      {
        if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
          return false;
        }
      }
    }
    return true;
  }

  internal static bool IsGeometryShard(string fileName) =>
    fileName.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase)
#if NET8_0_OR_GREATER
    && fileName.Contains(".geometries", StringComparison.OrdinalIgnoreCase);
#else
    && fileName.IndexOf(".geometries", StringComparison.OrdinalIgnoreCase) >= 0;
#endif
}
