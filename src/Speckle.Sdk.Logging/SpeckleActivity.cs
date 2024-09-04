using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Speckle.Sdk.Logging;

public sealed class SpeckleActivity(Activity activity) : ISpeckleActivity
{
  public void Dispose() => activity.Dispose();

  public void SetTag(string key, object? value) => activity.SetTag(key, value);

  public void RecordException(Exception e) => activity.RecordException(e);

  public string TraceId => activity.TraceId.ToString();

  public void SetStatus(SpeckleActivityStatusCode code) =>
    activity.SetStatus(
      code switch
      {
        SpeckleActivityStatusCode.Error => ActivityStatusCode.Error,
        SpeckleActivityStatusCode.Unset => ActivityStatusCode.Unset,
        SpeckleActivityStatusCode.Ok => ActivityStatusCode.Ok,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
      }
    );
}
