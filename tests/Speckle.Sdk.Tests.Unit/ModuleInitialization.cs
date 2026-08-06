using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Tests.Unit;

public static class ModuleInitialization
{
  private static bool _initialized;

  [ModuleInitializer]
  public static void Initialize()
  {
    if (_initialized)
    {
      return;
    }

    Testing.SpeckleVerify.Initialize();
    _ = TestServiceSetup.GetServiceProvider(); //Force type loader initialization
    _initialized = true;
  }
}
