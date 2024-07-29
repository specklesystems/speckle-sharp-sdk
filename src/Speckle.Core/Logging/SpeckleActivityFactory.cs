using System.Runtime.CompilerServices;

namespace Speckle.Core.Logging;

public static class SpeckleActivityFactory
{
  private static ISpeckleActivityFactory? s_speckleActivityFactory;

  public static void Initialize(ISpeckleActivityFactory speckleActivityFactory) => s_speckleActivityFactory = speckleActivityFactory;

  public static ISpeckleActivity? Start([CallerMemberName]string name = "SpeckleActivityFactory") => s_speckleActivityFactory?.StartActivity(name);
}
