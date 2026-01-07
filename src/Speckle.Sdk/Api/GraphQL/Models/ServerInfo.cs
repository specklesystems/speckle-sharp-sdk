using System.Diagnostics.CodeAnalysis;

namespace Speckle.Sdk.Api.GraphQL.Models;

public sealed class ServerInfo
{
#nullable disable  //Not nullable in the schema, but we frequently abuse ServerInfo, creating instances with incomplete data
  public string name { get; init; }
#nullable enable
  public string? company { get; init; }
  public string? version { get; init; }
  public string? description { get; init; }

  [Obsolete("Don't use")]
  public bool frontend2 { get; set; } = true;

  /// <summary>
  /// The URL that should be used to talk with the server
  /// </summary>
  /// <remarks>
  /// This field is not returned from the GQL API,
  /// it should be populated after construction.
  /// see <see cref="Speckle.Sdk.Credentials.AccountManager"/>
  /// </remarks>
  [StringSyntax(StringSyntaxAttribute.Uri)]
  public required string url { get; set; }

  public ServerMigration? migration { get; init; }
}

public sealed class ServerMigration
{
  /// <summary>
  /// Previous URI where this server used to be deployed
  /// </summary>
  public Uri? movedFrom { get; set; }

  /// <summary>
  /// New URI where this server is now deployed
  /// </summary>
  public Uri? movedTo { get; set; }
}
