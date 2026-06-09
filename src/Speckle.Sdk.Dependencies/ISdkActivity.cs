namespace Speckle.Sdk.Logging;

public interface ISdkActivity : IDisposable
{
  void SetTag(string key, object? value);
  void SetBaggage(string key, string? value);
  void RecordException(Exception ex);

  ///<summary> W3C <c>tracestate</c> header</summary>
  string? TraceState { get; }

  ///<summary> W3C <c>traceparent</c> header</summary>
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
