using System.Runtime.CompilerServices;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Testing;

namespace Speckle.Objects.Tests.Unit;

public static class Module
{
  [ModuleInitializer]
  public static void Initialize()
  {
    SpeckleVerify.Initialize();
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Polyline).Assembly);
  }
}
