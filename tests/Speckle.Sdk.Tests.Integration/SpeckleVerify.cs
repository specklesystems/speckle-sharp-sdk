using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Tests.Integration;

public static class SpeckleVerify
{
  private static bool s_initialized;

  [ModuleInitializer]
  public static void Initialize()
  {
    if (s_initialized)
    {
      return;
    }

    Testing.SpeckleVerify.Initialize();
    s_initialized = true;
  }
}
