using Speckle.Sdk.Api.GraphQL.Models;

namespace Speckle.Sdk.Credentials;

internal sealed class ActiveUserServerInfoResponse
{
  public UserInfo activeUser { get; init; }
  public ServerInfo serverInfo { get; init; }
}

internal sealed class TokenExchangeResponse
{
  public string token { get; init; }
  public string refreshToken { get; init; }
}

public sealed class UserInfo
{
  public string id { get; init; }
  public string name { get; init; }
  public string email { get; init; }
  public string? company { get; init; }
  public string? avatar { get; init; }
}
