using System.Diagnostics;

namespace Speckle.Logging;

public class SpeckleActivity(Activity activity) : ISpeckleActivity
{
  public void Dispose() => activity.Dispose();

  public void SetTag(string key, object? value) =>activity.SetTag(key, value);
}
