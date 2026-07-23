using Speckle.Objects.Utils;

namespace Speckle.Sdk.Artifacts.Harness;

/// <summary>
/// DI-registered factory for <see cref="GraphArtifactProducer"/>. The producer's
/// <see cref="ObjectsArtifactPipeline"/> is bound to an output directory + base name, both of which
/// are per-run values (destination model id + timestamp) resolved at runtime — so they can't be
/// container-time constructor dependencies. This factory supplies them on demand.
/// </summary>
internal sealed class GraphArtifactProducerFactory
{
  /// <summary>
  /// Creates a producer whose pipeline writes the bundle into <paramref name="outputDir"/> under
  /// <paramref name="baseName"/>. The caller owns the returned instance and must dispose it.
  /// </summary>
  public GraphArtifactProducer Create(string outputDir, string baseName)
  {
    Directory.CreateDirectory(outputDir);
    return new GraphArtifactProducer(new ObjectsArtifactPipeline(outputDir, baseName));
  }
}
