using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.Extensions.Logging;
using Speckle.Core.Credentials;
using Speckle.Core.Kits;

namespace Speckle.Core.Logging;

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

  public static void Init(
    string application, ILoggerFactory loggerFactory
  )
  {
    if (s_initialized)
    {
      SpeckleLogger.Create("Speckle.Core.Setup")
        .Information(
          "Setup was already initialized with {currentHostApp}",
          application
        );
      return;
    }

    s_initialized = true;

    HostApplication = application;

    //start mutex so that Manager can detect if this process is running
    Mutex = new Mutex(false, "SpeckleConnector-" + application);

    SpeckleLogger.Initialize(loggerFactory);

    foreach (var account in AccountManager.GetAccounts())
    {
      Analytics.AddConnectorToProfile(account.GetHashedEmail(), application);
      Analytics.IdentifyProfile(account.GetHashedEmail(), application);
    }
  }

  [Obsolete("Use " + nameof(Mutex))]
  [SuppressMessage("Style", "IDE1006:Naming Styles")]
  public static Mutex mutex
  {
    get => Mutex;
    set => Mutex = value;
  }
}
