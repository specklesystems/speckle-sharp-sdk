using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ResourceIdentifier
{
  public required string resourceId { get; init; }
  public required ResourceType resourceType { get; init; }
}
