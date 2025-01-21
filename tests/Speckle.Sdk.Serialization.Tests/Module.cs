using System.Runtime.CompilerServices;
using Speckle.Sdk.Testing;

namespace Speckle.Sdk.Serialization.Tests;

public static class Module
{
  [ModuleInitializer]
  public static void Initialize()
  {
    SpeckleVerify.Initialize();
  }
}
