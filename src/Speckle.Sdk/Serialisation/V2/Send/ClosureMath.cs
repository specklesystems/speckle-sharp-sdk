namespace Speckle.Sdk.Serialisation.V2.Send;

public static class ClosureMath
{
  public static void MergeClosures(this Dictionary<Id, int> current, IEnumerable<KeyValuePair<Id, int>> child)
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

  public static void AddOne(this Dictionary<Id, int> current, Id id)
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

  public static bool SetOne(this Dictionary<Id, int> current, Id id)
  {
    if (!current.TryGetValue(id, out _))
    {
      current[id] = 1;
      return true;
    }

    return false;
  }
}
