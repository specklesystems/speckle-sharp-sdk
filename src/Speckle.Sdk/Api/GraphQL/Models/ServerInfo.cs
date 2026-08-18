namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class AuthStrategy
{
  public required string? color { get; init; }
  public required string icon { get; init; }
  public required string id { get; init; }
  public required string name { get; init; }
  public required string url { get; init; }
}

public sealed class ServerInfo
{
#nullable disable
  public string name { get; init; }
#nullable enable
  public string? company { get; init; }
  public string? version { get; init; }
  public string? description { get; init; }

  [Obsolete("Don't use")]
  public bool frontend2 { get; set; } = true;

#nullable disable

  /// <summary>
  /// The URL that should be used to talk with the server
  /// </summary>
  /// <remarks>
  /// This field is not returned from the GQL API,
  /// it should be populated after construction.
  /// see <see cref="Speckle.Sdk.Credentials.AccountManager"/>
  /// </remarks>
  public string url { get; set; }

#nullable restore

  public ServerMigration? migration { get; init; }
}

public sealed class ServerMigration
{
  /// <summary>
  /// Previous URI where this server used to be deployed
  /// </summary>
  public required Uri? movedFrom { get; set; }

  /// <summary>
  /// New URI where this server is now deployed
  /// </summary>
  public required Uri? movedTo { get; set; }
}
