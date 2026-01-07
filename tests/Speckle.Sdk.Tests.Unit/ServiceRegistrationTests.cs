using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Speckle.Sdk.Tests.Unit;

public class ServiceRegistrationTests
{
  [Fact]
  public void RegisterDependencies_Validation()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3");
    var serviceProvider = serviceCollection.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    serviceProvider.Should().NotBeNull();
  }

  [Fact]
  public void RegisterDependencies_Scopes()
  {
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddSpeckleSdk(new("Tests", "test"), "v3");
    var serviceProvider = serviceCollection.BuildServiceProvider(
      new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
    );
    serviceProvider.Should().NotBeNull();
  }
}
