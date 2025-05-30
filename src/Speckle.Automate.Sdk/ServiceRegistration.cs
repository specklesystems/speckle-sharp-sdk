using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Geometry;
using Speckle.Sdk;
using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2;

namespace Speckle.Automate.Sdk;

public static class ServiceRegistration
{
  /// <summary>
  /// Sets-up the serviceCollection with all the services in Speckle.Automate.Sdk and Speckle.Sdk
  /// </summary>
  /// <param name="serviceCollection"></param>
  /// <returns></returns>
  public static IServiceCollection AddAutomateSdk(this IServiceCollection serviceCollection)
  {
    var executingAssembly = Assembly.GetExecutingAssembly().GetName();
    var speckleAssembly = typeof(Base).Assembly.GetName();
    AddAutomateSdk(
      serviceCollection,
      new SpeckleSdkOptions(
        new(executingAssembly.FullName, "automatefunction"),
        executingAssembly.Version?.ToString() ?? "Unknown",
        speckleAssembly.Version?.ToString(),
        [typeof(Base).Assembly, typeof(Point).Assembly]
      )
    );

    return serviceCollection;
  }

  public static IServiceCollection AddAutomateSdk(
    this IServiceCollection serviceCollection,
    SpeckleSdkOptions speckleSdkOptions
  )
  {
    serviceCollection.AddSpeckleSdk(speckleSdkOptions);

    //Overwrite the SDK's default IDeserializeProcessFactory to ensure SQLite is not used to cache objects
    serviceCollection.AddTransient<IDeserializeProcessFactory, DeserializeProcessFactoryNoCache>();

    //Add automate assembly services
    serviceCollection.AddTransient<IAutomationContextFactory, AutomationContextFactory>();
    serviceCollection.AddTransient<IAutomationRunner, AutomationRunner>();

    return serviceCollection;
  }
}
