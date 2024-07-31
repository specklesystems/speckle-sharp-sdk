using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Speckle.Logging;

public static class SpeckleActivityFactory
{
  private static ActivitySource? s_activitySource;

  public static void Initialize(string application, string version) =>
    s_activitySource = new ActivitySource(application, version);

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
