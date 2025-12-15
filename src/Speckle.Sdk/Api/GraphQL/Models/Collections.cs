using Speckle.Newtonsoft.Json;

namespace Speckle.Sdk.Api.GraphQL.Models;

public class ResourceCollection<T>
{
  [property: JsonProperty(Required = Required.Always)]
  public required int totalCount { get; init; }

  [property: JsonProperty(Required = Required.Always)]
  public required List<T> items { get; init; }

  [property: JsonProperty(Required = Required.AllowNull)]
  public required string? cursor { get; init; }
}

public sealed class CommentReplyAuthorCollection
{
  [property: JsonProperty(Required = Required.Always)]
  public required int totalCount { get; init; }

  [property: JsonProperty(Required = Required.Always)]
  public required List<LimitedUser> items { get; init; }
}

public sealed class UserSearchResultCollection
{
  [property: JsonProperty(Required = Required.Always)]
  public required List<LimitedUser> items { get; init; }

  [property: JsonProperty(Required = Required.AllowNull)]
  public required string? cursor { get; init; }
}

public sealed class ProjectCommentCollection : ResourceCollection<Comment>
{
  [property: JsonProperty(Required = Required.Always)]
  public required int totalArchivedCount { get; init; }
}
