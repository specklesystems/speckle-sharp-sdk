namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class Version
{
  public required LimitedUser? authorUser { get; init; }
  public required DateTime createdAt { get; init; }
  public required string id { get; init; }
  public required string? message { get; init; }
  public required Uri previewUrl { get; init; }

  /// <remarks>May be <see langword="null"/> if workspaces version history limit has been exceeded</remarks>
  public required string? referencedObject { get; init; }
  public required string? sourceApplication { get; init; }
}
