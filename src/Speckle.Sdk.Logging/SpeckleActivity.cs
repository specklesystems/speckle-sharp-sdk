using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Speckle.Sdk.Logging;

public class SpeckleActivity(Activity activity) : ISpeckleActivity
{
  public void Dispose() => activity.Dispose();

  public void SetTag(string key, object? value) => activity.SetTag(key, value);
  public void RecordException(Exception e) => activity.RecordException(e);
}
