namespace Speckle.Sdk.Logging;

public interface ISpeckleCounter<T>
  where T : struct
{
  void Add(T delta);
  void Add(T delta, params KeyValuePair<string, object?>[] tags);
  void Add(T delta, KeyValuePair<string, object?> tag);
  void Add(T delta, KeyValuePair<string, object?> tag1, KeyValuePair<string, object?> tag2);

  void Add(
    T delta,
    KeyValuePair<string, object?> tag1,
    KeyValuePair<string, object?> tag2,
    KeyValuePair<string, object?> tag3
  );
}

public interface ISpeckleHistogram<T>
  where T : struct
{
  void Record(T value);
}

public interface ISpeckleUpDownCounter<T>
  where T : struct
{
  void Add(T value);
}
