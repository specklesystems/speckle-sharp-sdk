using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Unit;

public static class TestServiceSetup
{
  public static Assembly[] TypeLoaderAssemblies { get; } = [typeof(Base).Assembly, Assembly.GetExecutingAssembly()];

  private static IServiceProvider? s_provider;

  public static IServiceProvider GetServiceProvider() => s_provider ??= CreateServiceProvider();

  private static IServiceProvider CreateServiceProvider()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", TypeLoaderAssemblies);
    return serviceCollection.BuildServiceProvider(
      new ServiceProviderOptions() { ValidateOnBuild = true, ValidateScopes = true }
    );
  }
}
