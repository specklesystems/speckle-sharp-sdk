namespace Speckle.Sdk.Logging;

public static class ISpeckleActivityExtensions
{
  public static void SetTags<T>(this ISpeckleActivity activity, string prefix, IReadOnlyDictionary<string, T> tags)
  {
    foreach (var tag in tags)
    {
      activity.SetTag(tag.Key, $"{prefix}.{tag.Value}");
    }
  }
}
