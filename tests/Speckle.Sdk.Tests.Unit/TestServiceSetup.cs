using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk.Host;

namespace Speckle.Sdk.Tests.Unit;

public static class TestServiceSetup
{
  public static IServiceProvider GetServiceProvider()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(HostApplications.Navisworks, HostAppVersion.v2023, "Test");
    return serviceCollection.BuildServiceProvider();
  }
}
