namespace Speckle.Sdk.DependencyInjection;

public record SpeckleServiceDescriptor(
  SpeckleServiceLifetime Lifetime,
  Type ServiceType,
  Type? ImplementationType,
  object? ImplementationInstance,
  Func<IServiceProvider, object>? ImplementationFactory);
