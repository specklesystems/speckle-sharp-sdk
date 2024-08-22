using Speckle.Sdk.DependencyInjection;

namespace Speckle.Sdk.Common;

public class ServiceProvider(SpeckleServiceProvider speckleServiceProvider) : IServiceProvider
{
  public object GetService(Type serviceType) => speckleServiceProvider.GetService(serviceType);
}
