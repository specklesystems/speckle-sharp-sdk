#nullable disable
using System.Runtime.InteropServices;

namespace Speckle.Core.Api.GraphQL.Models;

[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public sealed class ServerInfo
{
  public string name { get; set; }
  public string company { get; set; }
  public string version { get; set; }
  public string adminContact { get; set; }
  public string description { get; set; }

  /// <remarks>
  /// This field is not returned from the GQL API,
  /// it should be populated after construction from the response headers.
  /// see <see cref="Speckle.Core.Credentials.AccountManager"/>
  /// </remarks>
  public bool frontend2 { get; set; }

  /// <remarks>
  /// This field is not returned from the GQL API,
  /// it should be populated after construction.
  /// see <see cref="Speckle.Core.Credentials.AccountManager"/>
  /// </remarks>
  public string url { get; set; }

  public ServerMigration migration { get; init; }
}

public sealed class ServerMigration
{
  /// <summary>
  /// New URI where this server is now deployed
  /// </summary>
  public Uri movedTo { get; set; }

  /// <summary>
  /// Previous URI where this server used to be deployed
  /// </summary>
  public Uri movedFrom { get; set; }
}
