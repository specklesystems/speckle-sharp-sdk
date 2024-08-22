using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.DependencyInjection;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk;

public record SpeckleSdk(IDisposable? Logging, IServiceProvider ServiceProvider);
/// <summary>
///  Anonymous telemetry to help us understand how to make a better Speckle.
///  This really helps us to deliver a better open source project and product!
/// </summary>
public static class Setup
{

  public static IServiceProvider CreateServiceProvider(IServiceCollection serviceCollection) =>
    new SpeckleServiceProvider(serviceCollection.Select(x => new SpeckleServiceDescriptor(GetServiceLifetime(x.Lifetime), x.ServiceType, x.ImplementationType, x.ImplementationInstance, x.ImplementationFactory)));
  
  private static SpeckleServiceLifetime GetServiceLifetime(ServiceLifetime serviceLifetime) =>
    serviceLifetime switch
    {
      ServiceLifetime.Singleton => SpeckleServiceLifetime.Singleton,
      ServiceLifetime.Scoped => SpeckleServiceLifetime.Scoped,
      ServiceLifetime.Transient => SpeckleServiceLifetime.Transient,
      _ => throw new ArgumentOutOfRangeException(nameof(serviceLifetime), serviceLifetime, null)
    };
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

  public static SpeckleSdk Initialize(IServiceCollection serviceCollection, SpeckleConfiguration configuration)
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

    foreach (var account in AccountManager.GetAccounts())
    {
      Analytics.AddConnectorToProfile(account.GetHashedEmail(), Application);
      Analytics.IdentifyProfile(account.GetHashedEmail(), Application);
    }
    var logDisposable = LogBuilder.Initialize(
      GetUserIdFromDefaultAccount(),
      ApplicationVersion,
      Slug,
      configuration.Logging,
      configuration.Tracing
    );
    serviceCollection.AddSdk();
    return new (logDisposable, CreateServiceProvider(serviceCollection));
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
