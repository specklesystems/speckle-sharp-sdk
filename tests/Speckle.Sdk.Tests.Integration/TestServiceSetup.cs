using Microsoft.Extensions.DependencyInjection;

namespace Speckle.Sdk.Tests.Integration;

public static class TestServiceSetup
{
  public static IServiceProvider GetServiceProvider()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk("Tests", "test", "v3");
    return serviceCollection.BuildServiceProvider();
  }
}
