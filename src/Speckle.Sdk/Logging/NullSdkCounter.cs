namespace Speckle.Sdk.Logging;

public sealed class NullSdkCounter<T> : ISdkCounter<T>
  where T : struct
{
  public void Add(T value) { }

  public void Add(T value, KeyValuePair<string, object?> tag) { }

  public void Add(T value, KeyValuePair<string, object?> tag1, KeyValuePair<string, object?> tag2) { }

  public void Add(
    T value,
    KeyValuePair<string, object?> tag1,
    KeyValuePair<string, object?> tag2,
    KeyValuePair<string, object?> tag3
  ) { }

  public void Add(T value, params KeyValuePair<string, object?>[] tag) { }
}
