namespace Speckle.Sdk.Logging;

public interface ISdkCounter<T>
  where T : struct
{
  void Add(T value);
  void Add(T value, KeyValuePair<string, object?> tag);
  void Add(T value, KeyValuePair<string, object?> tag1, KeyValuePair<string, object?> tag2);
  void Add(T value, KeyValuePair<string, object?> tag1, KeyValuePair<string, object?> tag2, KeyValuePair<string, object?> tag3);
  void Add(T value, params KeyValuePair<string, object?>[] tag);
}
