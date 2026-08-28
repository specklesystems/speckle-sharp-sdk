using Speckle.Sdk.Models;

namespace Speckle.Sdk.BundleMigrator.Migration;

/// <summary>
/// Migrates a stored Speckle <see cref="Base"/> graph into the artefact bundle. Implemented once per graph
/// vintage — see <see cref="V2GraphArtifactProducer"/> and <see cref="V3GraphArtifactProducer"/>; the
/// <see cref="GraphArtifactProducerFactory"/> picks between them.
/// </summary>
internal interface IGraphArtifactProducer : IDisposable
{
  /// <summary>Walks <paramref name="root"/>, drives the pipeline, and completes it (all parquet files are
  /// flushed and closed on return). Returns the run stats.</summary>
  Stats Produce(Base root);
}
