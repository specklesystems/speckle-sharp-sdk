using System.Diagnostics.Metrics;

namespace Speckle.Sdk.Logging;

public class SpeckleCounter<T>(Counter<T> counter) : ISpeckleCounter<T>
  where T : struct
{
  public void Add(T delta) => counter.Add(delta);
  public void Add(T delta, params KeyValuePair<string, object?>[] tags) => counter.Add(delta, tags);
  public void Add(T delta, KeyValuePair<string, object?> tag)=> counter.Add(delta, tag);

  public void Add(T delta, KeyValuePair<string, object?> tag1, KeyValuePair<string, object?> tag2) => counter.Add(delta, tag1, tag2);

  public void Add(T delta, KeyValuePair<string, object?> tag1, KeyValuePair<string, object?> tag2, KeyValuePair<string, object?> tag3)=> counter.Add(delta, tag1, tag2, tag3);
}

public class SpeckleUpDownCounter<T>(UpDownCounter<T> counter) : ISpeckleUpDownCounter<T>
  where T : struct
{
  public void Add(T delta) => counter.Add(delta);
}

public class SpeckleHistogram<T>(Histogram<T> histogram) : ISpeckleHistogram<T>
  where T : struct
{
  public void Record(T delta) => histogram.Record(delta);
}
