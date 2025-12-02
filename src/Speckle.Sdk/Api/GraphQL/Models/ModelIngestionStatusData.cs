using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ModelIngestionStatusData
{
  public required ModelIngestionStatus status { get; init; }
  public required string? progressMessage { get; init; }
}
