namespace Speckle.Sdk.Logging;

public interface ISpeckleActivity : IDisposable
{
  void SetTag(string key, object? value);
  void RecordException(Exception e);
  string TraceId { get; }
  void SetStatus(SpeckleActivityStatusCode code);
}
