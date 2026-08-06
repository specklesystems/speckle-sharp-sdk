// SPIKE(wayfinder ticket 07) — THROWAWAY. The DI-inversion seam for the Sdk/Objects split (research 03 §2):
// declared here in Speckle.Sdk (where Operations lives), implemented in Speckle.Objects (where the geometry
// codecs and Base-shaping live), injected via the existing container.
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>Turns a downloaded artefact bundle directory into a legacy-compatible <see cref="Base"/> tree.</summary>
public interface IArtifactGraphMaterializer
{
  Task<Base> MaterializeAsync(string bundleDir, CancellationToken cancellationToken);
}
