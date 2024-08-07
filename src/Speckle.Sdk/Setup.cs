using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk;

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
      Application = Process.GetCurrentProcess().ProcessName;
    }
    catch (InvalidOperationException)
    {
      Application = "other (.NET)";
    }
  }

  /// <summary>
  /// Set from the connectors, defines which current host application we're running on.
  /// </summary>
  internal static string Application { get; private set; }
  internal static string Version { get; private set; }
  internal static string ApplicationVersion => $"{Application} {Version}";

  /// <summary>
  /// Set from the connectors, defines which current host application we're running on - includes the version.
  /// </summary>
  internal static string Slug { get; private set; } = HostApplications.Other.Slug;

  public static IDisposable? Initialize(SpeckleConfiguration configuration)
  {
    if (s_initialized)
    {
      SpeckleLog.Logger.Information("Setup was already initialized with {currentHostApp}", configuration.Application);
      throw new InvalidOperationException();
    }

    s_initialized = true;
    Application = configuration.Application.Name;
    Version = HostApplications.GetVersion(configuration.Version);
    Slug = configuration.Application.Slug;

    //start mutex so that Manager can detect if this process is running
    Mutex = new Mutex(false, "SpeckleConnector-" + configuration.Application);

    var traceProvider = TraceBuilder.Initialize(Application, Slug, configuration.Tracing);
    LogBuilder.Initialize(GetUserIdFromDefaultAccount(), Application, Slug, configuration.Logging);

    foreach (var account in AccountManager.GetAccounts())
    {
      Analytics.AddConnectorToProfile(account.GetHashedEmail(), Application);
      Analytics.IdentifyProfile(account.GetHashedEmail(), Application);
    }

    SpeckleActivityFactory.Initialize(Slug, Version);

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
