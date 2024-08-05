using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;

namespace Speckle.Sdk.Logging;

/// <summary>
///  Anonymous telemetry to help us understand how to make a better Speckle.
///  This really helps us to deliver a better open source project and product!
/// </summary>
[SuppressMessage(
  "Naming",
  "CA1708:Identifiers should differ by more than case",
  Justification = "Class contains obsolete members that are kept for backwards compatiblity"
)]
public static class Setup
{
  public static Mutex Mutex { get; set; }

  private static bool s_initialized;

  static Setup()
  {
    //Set fallback values
    try
    {
      HostApplication = Process.GetCurrentProcess().ProcessName;
    }
    catch (InvalidOperationException)
    {
      HostApplication = "other (.NET)";
    }
  }

  /// <summary>
  /// Set from the connectors, defines which current host application we're running on.
  /// </summary>
  internal static string HostApplication { get; private set; }

  /// <summary>
  /// Set from the connectors, defines which current host application we're running on - includes the version.
  /// </summary>
  internal static string VersionedHostApplication { get; private set; } = HostApplications.Other.Slug;

  public static IDisposable Initialize(
    string hostApplicationName,
    string hostApplicationVersion,
    SpeckleLogConfiguration? logConfiguration = null
  )
  {
    if (s_initialized)
    {
      SpeckleLog.Logger.Information("Setup was already initialized with {currentHostApp}", hostApplicationName);
      throw new InvalidOperationException();
    }

    s_initialized = true;

    logConfiguration ??= new SpeckleLogConfiguration();
    HostApplication = hostApplicationName;
    VersionedHostApplication = hostApplicationVersion;

    //start mutex so that Manager can detect if this process is running
    Mutex = new Mutex(false, "SpeckleConnector-" + hostApplicationName);

    var traceProvider = TraceBuilder.Initialize(hostApplicationName, logConfiguration);
    LogBuilder.Initialize(GetUserIdFromDefaultAccount(), hostApplicationName, hostApplicationVersion, logConfiguration);

    foreach (var account in AccountManager.GetAccounts())
    {
      Analytics.AddConnectorToProfile(account.GetHashedEmail(), hostApplicationName);
      Analytics.IdentifyProfile(account.GetHashedEmail(), hostApplicationName);
    }

    SpeckleActivityFactory.Initialize(hostApplicationName, hostApplicationVersion);

    return traceProvider;
  }

  private static string GetUserIdFromDefaultAccount()
  {
    var machineName = Environment.MachineName;
    var userName = Environment.UserName;
    var id = Crypt.Md5($"{machineName}:{userName}", "X2");
    try
    {
      var defaultAccount = AccountManager.GetDefaultAccount();
      if (defaultAccount != null)
      {
        id = defaultAccount.GetHashedEmail();
      }
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
      // To log it after Logger initialized as deferred action.
    }
    return id;
  }
}
