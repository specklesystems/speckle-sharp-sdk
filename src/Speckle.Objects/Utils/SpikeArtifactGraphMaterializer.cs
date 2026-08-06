// SPIKE(wayfinder ticket 07) — THROWAWAY. Objects-side implementation of the Sdk-declared materializer seam:
// wraps the existing connector reader unchanged, so the spike measures ObjectsArtifactReader's fidelity as-is.
using Speckle.Sdk.Models;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Objects.Utils;

public sealed class SpikeArtifactGraphMaterializer : IArtifactGraphMaterializer
{
  public Task<Base> MaterializeAsync(string bundleDir, CancellationToken cancellationToken) =>
    // PreferSolids: false = the Revit-shaped view (SGEO display meshes, no raw 3dm) — the closest analog to
    // what an SDK data consumer traverses today.
    new ObjectsArtifactReader().ReadAsync(bundleDir, new ArtifactReceiveOptions(PreferSolids: false), cancellationToken);
}
