using System.Diagnostics;
using System.Runtime.CompilerServices;
using Speckle.InterfaceGenerator;

namespace Speckle.Sdk.Logging;

[GenerateAutoInterface]
public sealed class ActivityFactory : IActivityFactory, IDisposable
{
  private readonly ActivitySource? s_activitySource = new(Consts.Application, Consts.Version);

  public ISpeckleActivity? Start(string? name = null, [CallerMemberName] string source = "")
  {
    var activity = s_activitySource?.StartActivity(name ?? source, ActivityKind.Client);
    if (activity is null)
    {
      return null;
    }
    return new SpeckleActivity(activity);
  }

  public void Dispose() => s_activitySource?.Dispose();
}
