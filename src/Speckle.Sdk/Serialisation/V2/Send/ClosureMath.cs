﻿namespace Speckle.Sdk.Serialisation.V2.Send;

public static class ClosureMath
{
  public static void IncrementClosures(this Dictionary<Id, int> current, IEnumerable<KeyValuePair<Id, int>> child)
  {
    foreach (var closure in child)
    {
      if (current.TryGetValue(closure.Key, out var count))
      {
        current[closure.Key] = Math.Max(closure.Value, count) + 1;
      }
      else
      {
        current[closure.Key] = closure.Value + 1;
      }
    }
  }

  public static void MergeClosures(this Dictionary<Id, int> current, IEnumerable<KeyValuePair<Id, int>> child)
  {
    foreach (var closure in child)
    {
      if (current.TryGetValue(closure.Key, out var count))
      {
        current[closure.Key] = Math.Max(closure.Value, count);
      }
      else
      {
        current[closure.Key] = closure.Value;
      }
    }
  }

  public static void IncrementClosure(this Dictionary<Id, int> current, Id id)
  {
    if (current.TryGetValue(id, out var count))
    {
      current[id] = count + 1;
    }
    else
    {
      current[id] = 1;
    }
  }

  public static void MergeClosure(this Dictionary<Id, int> current, Id id)
  {
    if (current.TryGetValue(id, out var count))
    {
      current[id] = count;
    }
    else
    {
      current[id] = 1;
    }
  }
}
