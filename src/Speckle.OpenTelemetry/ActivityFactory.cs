using System.Diagnostics;
using Speckle.Core.Common;

namespace Speckle.Core.Logging;

public static class SpeckleActivityFactory
{
  private static ActivitySource? _activitySource;

  public static void Initialize(string application)
  {
    _activitySource = new ActivitySource(application, "1.0.0");
  }

  public static SpeckleActivity? Create(string name)
  {
    var activity = _activitySource.NotNull().StartActivity(name);
    if (activity is null)
    {
      return null;
    }
    return new(activity);
  }
}

public class SpeckleActivity(Activity activity) : IDisposable
{
  public void Dispose() => activity.Dispose();
}
