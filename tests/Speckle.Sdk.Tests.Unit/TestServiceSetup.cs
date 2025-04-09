using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Speckle.Sdk.Tests.Unit;

public static class TestServiceSetup
{
  public static IServiceProvider GetServiceProvider()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3", Assembly.GetExecutingAssembly());
    return serviceCollection.BuildServiceProvider();
  }
}
