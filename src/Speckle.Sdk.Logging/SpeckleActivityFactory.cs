using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Logging;

public static class SpeckleActivityFactory
{
  private static ActivitySource? s_activitySource;

  public static void Initialize(string slug, string version) => s_activitySource = new ActivitySource(slug, version);

  public static ISpeckleActivity? Start([CallerMemberName] string name = "SpeckleActivityFactory")
  {
    var activity = s_activitySource?.StartActivity(name);
    if (activity is null)
    {
      return null;
    }
    return new SpeckleActivity(activity);
  }
}
