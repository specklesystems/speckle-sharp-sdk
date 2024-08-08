using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Speckle.Sdk.Logging;

public static class SpeckleActivityFactory
{
  private static readonly ActivitySource? s_activitySource = new(Consts.Application, Consts.Version);

  public static ISpeckleActivity? Start(string? name = null, [CallerMemberName] string source = "")
  {
    var activity = s_activitySource?.StartActivity(name ?? source, ActivityKind.Client);
    if (activity is null)
    {
      return null;
    }
    return new SpeckleActivity(activity);
  }
}
