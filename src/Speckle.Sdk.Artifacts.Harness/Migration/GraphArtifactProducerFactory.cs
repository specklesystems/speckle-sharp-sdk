using Microsoft.Extensions.Logging;
using Speckle.Objects.Utils;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Artifacts.Harness.Migration;

/// <summary>
/// DI-registered factory for <see cref="IGraphArtifactProducer"/>. The producer's
/// <see cref="ObjectsArtifactPipeline"/> is bound to an output directory + base name, both of which
/// are per-run values (destination model id + timestamp) resolved at runtime — so they can't be
/// container-time constructor dependencies. The implementation is chosen per graph, by vintage: see
/// <see cref="ArtifactHelper.IsV3"/>.
/// </summary>
internal sealed class GraphArtifactProducerFactory(ArtifactHelper helper, ILogger<GraphArtifactProducerFactory> logger)
{
  /// <summary>
  /// Creates the producer matching <paramref name="root"/>'s vintage, writing the bundle into
  /// <paramref name="outputDir"/> under <paramref name="baseName"/>. The caller owns the returned instance
  /// and must dispose it.
  /// </summary>
  public IGraphArtifactProducer Create(string outputDir, string baseName, Base root)
  {
    Directory.CreateDirectory(outputDir);
    var pipeline = new ObjectsArtifactPipeline(outputDir, baseName);

    var isV3 = helper.IsV3(root);
    logger.LogInformation("Detected {GraphVersion} graph [{SpeckleType}]", isV3 ? "v3" : "v2", root.speckle_type);

    return isV3 ? new V3GraphArtifactProducer(pipeline, helper) : new V2GraphArtifactProducer(pipeline, helper);
  }
}
