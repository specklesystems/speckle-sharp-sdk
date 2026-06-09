namespace Speckle.Sdk.Logging;

public interface ISdkActivity : IDisposable
{
  void SetTag(string key, object? value);
  void RecordException(Exception ex);

  // W3C <c>tracestate</c> header
  string? TraceState { get; }

  // W3C <c>traceparent</c> header
  string TraceParent { get; }

  string TraceId { get; }
  string SpanId { get; }
  void SetStatus(SdkActivityStatusCode code);

  void InjectHeaders(Action<string, string> header);
}

public enum SdkActivityStatusCode
{
  /// <summary>Unset status code is the default value indicating the status code is not initialized.</summary>
  Unset,

  /// <summary>Status code indicating the operation has been validated and completed successfully.</summary>
  Ok,

  /// <summary>Status code indicating an error is encountered during the operation.</summary>
  Error,
}
