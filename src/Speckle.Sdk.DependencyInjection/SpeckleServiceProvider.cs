using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Speckle.InterfaceGenerator;

namespace Speckle.Sdk.DependencyInjection;

[GenerateAutoInterface]
public class SpeckleServiceProvider : IServiceProvider, ISpeckleServiceProvider
{
  private readonly IServiceProvider _serviceProvider;
  
  internal SpeckleServiceProvider(IServiceProvider serviceProvider)
  {
    _serviceProvider = serviceProvider;
  }

  public SpeckleServiceProvider(IEnumerable<SpeckleServiceDescriptor> serviceCollection)
  {
    var services = new ServiceCollection();
    foreach(var descriptor in serviceCollection.Select(GetServiceDescriptor))
    {
      services.Add(descriptor);
    }

    services.AddSingleton<ISpeckleServiceProvider>(x => (ISpeckleServiceProvider)x);
    _serviceProvider = services.BuildServiceProvider();
  }

  private static ServiceDescriptor GetServiceDescriptor(SpeckleServiceDescriptor speckleServiceDescriptor)
  {
    var lifetime = GetServiceLifetime(speckleServiceDescriptor.Lifetime);
    if (speckleServiceDescriptor.ImplementationType is not null)
    {
      return new ServiceDescriptor(speckleServiceDescriptor.ServiceType, speckleServiceDescriptor.ImplementationType, lifetime);
    }
    if (speckleServiceDescriptor.ImplementationInstance is not null)
    {
      return new ServiceDescriptor(speckleServiceDescriptor.ServiceType, null, speckleServiceDescriptor.ImplementationInstance);
    }
    if (speckleServiceDescriptor.ImplementationFactory is not null)
    {
      return new ServiceDescriptor(speckleServiceDescriptor.ServiceType, speckleServiceDescriptor.ImplementationFactory, lifetime);
    }
    throw new ArgumentOutOfRangeException(nameof(speckleServiceDescriptor), speckleServiceDescriptor, null);
  }
  
  private static ServiceLifetime GetServiceLifetime(SpeckleServiceLifetime speckleServiceLifetime) =>
    speckleServiceLifetime switch
    {
      SpeckleServiceLifetime.Singleton => ServiceLifetime.Singleton,
      SpeckleServiceLifetime.Scoped => ServiceLifetime.Scoped,
      SpeckleServiceLifetime.Transient => ServiceLifetime.Transient,
      _ => throw new ArgumentOutOfRangeException(nameof(speckleServiceLifetime), speckleServiceLifetime, null)
    };

  public object GetService(Type serviceType) => _serviceProvider.GetService(serviceType);
  
  public ISpeckleScope BeginScope() => new SpeckleScope(_serviceProvider.CreateScope());

  [AutoInterfaceIgnore]
  public T GetRequiredService<T>() where T : class  => _serviceProvider.GetRequiredService<T>();
}

public partial interface ISpeckleServiceProvider
{
  T GetRequiredService<T>() where T : class;
}
