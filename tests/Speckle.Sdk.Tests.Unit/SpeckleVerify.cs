using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Tests.Unit;

public static class SpeckleVerify
{
  private static bool _initialized;

  [ModuleInitializer]
  public static void Initialize()
  {
    if (_initialized)
    {
      return;
    }

    global::Speckle.Sdk.Testing.SpeckleVerify.Initialize();
    _initialized = true;
  }
}
