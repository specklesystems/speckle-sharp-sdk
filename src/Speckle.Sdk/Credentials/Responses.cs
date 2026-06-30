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

public sealed class TokenExchangeResponse
{
  [JsonRequired]
  public required string token { get; init; }

  [JsonRequired]
  public required string refreshToken { get; init; }
}

public sealed class UserInfo
{
  public string id { get; init; }
  public string name { get; init; }
  public string email { get; init; }
  public string? company { get; init; }
  public string? avatar { get; init; }
}
