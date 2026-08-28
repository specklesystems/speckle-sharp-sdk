using System.Diagnostics;
using Speckle.Connectors.Logging;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.BundleMigrator.Logging;

/// <summary>
/// To avoid the SDK having a dependency on <c>System.Diagnostics.DiagnosticSource</c>
/// All activities created by the SDK are abstracted behind a <see cref="ISdkActivity"/>
/// This dll avoidance is only useful for avoiding dll conflicts in .NET Framework connectors,
/// it provides no value here, but we must work with the SDKs abstractions.
/// </summary>
internal sealed class SdkActivityFactory : ISdkActivityFactory
{
  public void Dispose() { }

  public ISdkActivity? Start(
    string? name = null,
    SdkActivityKind kind = SdkActivityKind.Internal,
    IReadOnlyDictionary<string, object?>? tags = null,
    DateTimeOffset startTime = default,
    string source = ""
  )
  {
    var activity = Telemetry.Activity.StartActivity(name ?? source, ToLoggingType(kind), null, tags, null, startTime);
    if (activity is null)
    {
      return null;
    }
    return new SdkActivityWrapper(activity);
  }

  public ISdkActivity? StartRemote(
    string? traceParent,
    string? traceState,
    SdkActivityKind kind,
    string? name = null,
    IReadOnlyDictionary<string, object?>? tags = null,
    DateTimeOffset startTime = default,
    string source = ""
  )
  {
    if (
      !System.Diagnostics.ActivityContext.TryParse(
        traceParent,
        traceState,
        true,
        out System.Diagnostics.ActivityContext context
      )
    )
    {
      if (traceParent is not null)
      {
        throw new ArgumentException(
          $"traceParent {traceParent} and/or traceState {traceState} was not parsable to a valid W3C context Header",
          nameof(traceParent)
        );
      }
    }

    var activity = Telemetry.Activity.StartActivity(
      name ?? source,
      ToLoggingType(kind),
      context,
      tags,
      null,
      startTime
    );

    if (activity is null)
    {
      return null;
    }
    return new SdkActivityWrapper(activity);
  }

  private static ActivityKind ToLoggingType(SdkActivityKind kind) =>
    kind switch
    {
      SdkActivityKind.Internal => ActivityKind.Internal,
      SdkActivityKind.Server => ActivityKind.Server,
      SdkActivityKind.Client => ActivityKind.Client,
      SdkActivityKind.Producer => ActivityKind.Producer,
      SdkActivityKind.Consumer => ActivityKind.Consumer,
      _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

  private sealed class SdkActivityWrapper(Activity activity) : ISdkActivity
  {
    public void Dispose() => activity.Dispose();

    public void SetTag(string key, object? value) => activity.SetTag(key, value);

    public void SetBaggage(string key, string? value) => activity.SetBaggage(key, value);

    public void RecordException(Exception ex) => activity.AddException(ex);

    /// <summary>
    /// <paramref name="headerHandler"/> will be called to handle each trace values
    /// stored in the <see cref="Activity"/> object
    /// so it can be injected into the headers of an HTTP request.
    /// </summary>
    public void InjectHeaders(Action<string, string> headerHandler) =>
      DistributedContextPropagator.Current.Inject(
        activity,
        headerHandler,
        static (carrier, key, value) =>
        {
          if (carrier is Action<string, string> request)
          {
            request.Invoke(key, value);
          }
        }
      );

    public string? TraceState => activity.TraceStateString;
    public string TraceParent => $"00-{TraceId}-{SpanId}-{checked((byte)activity.ActivityTraceFlags):X2}";
    public string TraceId => activity.TraceId.ToHexString();
    public string SpanId => activity.SpanId.ToHexString();

    public void SetStatus(SdkActivityStatusCode code) =>
      activity.SetStatus(
        code switch
        {
          SdkActivityStatusCode.Error => ActivityStatusCode.Error,
          SdkActivityStatusCode.Unset => ActivityStatusCode.Unset,
          SdkActivityStatusCode.Ok => ActivityStatusCode.Ok,
          _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
        }
      );
  }
}
