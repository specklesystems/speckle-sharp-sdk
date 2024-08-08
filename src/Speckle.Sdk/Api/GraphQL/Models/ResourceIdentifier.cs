#nullable disable
using Speckle.Sdk.Api.GraphQL.Enums;

namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ResourceIdentifier
{
  public string resourceId { get; init; }
  public ResourceType resourceType { get; init; }
}
