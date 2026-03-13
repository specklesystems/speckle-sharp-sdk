using Microsoft.Extensions.Logging;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Api;
using Speckle.Sdk.Api.GraphQL.Models;

namespace Speckle.Sdk.Pipelines.Progress;

[GenerateAutoInterface]
public sealed class IngestionProgressManagerFactory(ILogger<IngestionProgressManager> logger)
  : IIngestionProgressManagerFactory
{
  public IIngestionProgressManager CreateInstance(
    IClient speckleClient,
    ModelIngestion ingestion,
    TimeSpan updateInterval,
    CancellationToken cancellationToken
  )
  {
    return new IngestionProgressManager(logger, speckleClient, ingestion, updateInterval, cancellationToken);
  }
}
