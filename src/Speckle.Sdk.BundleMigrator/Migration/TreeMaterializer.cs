using Speckle.Objects.Utils;
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.BundleMigrator.Migration;

/// <summary>
/// Materializes a 4.0 artefact bundle back into a v3-style <see cref="Base"/>/Collection tree — the reverse of the
/// graph producers in this folder. The projection itself is <see cref="ObjectsArtifactReader"/> (collection tree from
/// CONTAINER nodes, EAV properties, SGEO-decoded <c>displayValue</c> via DISPLAY edges, render-material /
/// instance-definition proxies on root dynamic props); this wrapper only stamps the root with the vintage marker so
/// a materialized tree is distinguishable from a genuine v2/v3 one (same convention <c>ArtifactHelper.IsV3</c> reads).
/// </summary>
internal static class TreeMaterializer
{
  public static async Task<Base> MaterializeAsync(
    string bundleDir,
    bool preferSolids = false,
    CancellationToken cancellationToken = default
  )
  {
    var bundle = await ArtefactBundleReader.ReadAsync(bundleDir, cancellationToken).ConfigureAwait(false);
    return Materialize(bundle, preferSolids, cancellationToken);
  }

  /// <summary>Maps an already-parsed bundle into the tree (no IO). <paramref name="preferSolids"/>=true rebuilds
  /// objects carrying raw 3dm SOLID blobs as <c>RhinoObject</c> with <c>rawEncoding</c>; false (default) rebuilds
  /// every object from its DISPLAY meshes only — the shape script/SDK consumers traverse.</summary>
  public static Base Materialize(
    ArtefactBundle bundle,
    bool preferSolids = false,
    CancellationToken cancellationToken = default
  )
  {
    var root = new ObjectsArtifactReader().Build(bundle, new ArtifactReceiveOptions(preferSolids), cancellationToken);
    root["version"] = 4;
    return root;
  }
}
