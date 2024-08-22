using Microsoft.Extensions.DependencyInjection;
using Speckle.InterfaceGenerator;

namespace Speckle.Sdk.DependencyInjection;

public record SpeckleServiceDescriptor(
  SpeckleServiceLifetime Lifetime,
  Type ServiceType,
  Type? ImplementationType,
  object? ImplementationInstance,
  Func<IServiceProvider, object>? ImplementationFactory);

[GenerateAutoInterface]
public class SpeckleScope : ISpeckleScope
{
  private readonly IServiceScope _serviceScope;

  internal SpeckleScope(IServiceScope serviceScope)
  {
    _serviceScope = serviceScope;
  }

  public ISpeckleServiceProvider ServiceProvider => new SpeckleServiceProvider(_serviceScope.ServiceProvider);
  
  [AutoInterfaceIgnore]
  public void Dispose() => _serviceScope.Dispose();
}


public partial interface ISpeckleScope : IDisposable;
