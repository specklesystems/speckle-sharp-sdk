using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Host;

namespace Speckle.Sdk.Tests.Integration;

public static class TestServiceSetup
{
  public static IServiceProvider GetServiceProvider(params Assembly[] assemblies)
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(HostApplications.Navisworks, HostAppVersion.v2023, "Test", assemblies);
    return serviceCollection.BuildServiceProvider();
  }
}
