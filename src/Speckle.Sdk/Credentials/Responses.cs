using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api.GraphQL.Models;

namespace Speckle.Sdk.Credentials;

internal sealed class ActiveUserServerInfoResponse
{
  [property: JsonProperty(Required = Required.AllowNull)]
  public required UserInfo? activeUser { get; init; }

  [property: JsonProperty(Required = Required.Always)]
  public required ServerInfo serverInfo { get; init; }
}

internal sealed class TokenExchangeResponse
{
  public required string token { get; init; }
  public required string refreshToken { get; init; }
}

public sealed class UserInfo
{
#nullable disable // Non-nullable in the schema, but we frequently abuse UserInfo with incomplete data
  public string id { get; init; }
  public string name { get; init; }
  public string email { get; init; }
#nullable enable
  public string? company { get; init; }
  public string? avatar { get; init; }
}
