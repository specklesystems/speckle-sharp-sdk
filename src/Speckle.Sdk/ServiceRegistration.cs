﻿using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Speckle.Sdk.Dependencies;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk;

public record Application(string Name, string Slug);

public record SpeckleSdkOptions(
  Application Application,
  string ApplicationVersion,
  string? SpeckleVersion,
  IEnumerable<Assembly>? Assemblies
);

public static class ServiceRegistration
{
  private static string GetAssemblyVersion() =>
    Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

  public static IServiceCollection AddSpeckleSdk(
    this IServiceCollection serviceCollection,
    Application application,
    string applicationVersion,
    string? speckleVersion = null,
    IEnumerable<Assembly>? assemblies = null
  ) => serviceCollection.AddSpeckleSdk(new(application, applicationVersion, speckleVersion, assemblies));

  public static IServiceCollection AddSpeckleSdk(
    this IServiceCollection serviceCollection,
    Application application,
    string applicationVersion,
    string? speckleVersion,
    params Assembly[] assemblies
  ) => serviceCollection.AddSpeckleSdk(new(application, applicationVersion, speckleVersion, assemblies));

  public static IServiceCollection AddSpeckleSdk(
    this IServiceCollection serviceCollection,
    Application application,
    string applicationVersion,
    params Assembly[] assemblies
  ) => serviceCollection.AddSpeckleSdk(new(application, applicationVersion, null, assemblies));

  public static IServiceCollection AddSpeckleSdk(
    this IServiceCollection serviceCollection,
    SpeckleSdkOptions speckleSdkOptions
  )
  {
    var currentAssembly = Assembly.GetExecutingAssembly();
    var allAssembles = speckleSdkOptions.Assemblies?.ToList() ?? [];
    if (!allAssembles.Contains(currentAssembly))
    {
      allAssembles.Add(currentAssembly);
    }
    TypeLoader.Reset();
    TypeLoader.Initialize(allAssembles.ToArray());
    serviceCollection.AddLogging();

    serviceCollection.AddSingleton<ISpeckleApplication>(
      new SpeckleApplication
      {
        HostApplication = speckleSdkOptions.Application.Name,
        HostApplicationVersion = speckleSdkOptions.ApplicationVersion,
        Slug = speckleSdkOptions.Application.Slug,
        SpeckleVersion = speckleSdkOptions.SpeckleVersion ?? GetAssemblyVersion(),
      }
    );
    serviceCollection.TryAddSingleton<ISdkActivityFactory, NullActivityFactory>();
    serviceCollection.TryAddSingleton<ISdkMetricsFactory, NullSdkMetricsFactory>();
    serviceCollection.AddMatchingInterfacesAsTransient(Assembly.GetExecutingAssembly());
    serviceCollection.AddMatchingInterfacesAsTransient(typeof(GraphQLRetry).Assembly);
    return serviceCollection;
  }

  public static IServiceCollection AddMatchingInterfacesAsTransient(
    this IServiceCollection serviceCollection,
    Assembly assembly
  )
  {
    foreach (var type in assembly.ExportedTypes.Where(t => t.IsNonAbstractClass()))
    {
      foreach (var matchingInterface in type.FindMatchingInterface())
      {
        serviceCollection.TryAddTransient(matchingInterface, type);
      }
    }

    return serviceCollection;
  }
}
