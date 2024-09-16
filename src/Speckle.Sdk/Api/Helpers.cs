using System.Diagnostics.Contracts;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Api;

public static class Helpers
{
  public const string RELEASES_URL = "https://releases.speckle.dev";

  /// <inheritdoc cref="TimeAgo(DateTime)"/>
  /// <param name="fallback">value to fallback to if the given <paramref name="timestamp"/> is <see langword="null"/></param>
  public static string TimeAgo(DateTime? timestamp, string fallback = "Never")
  {
    return timestamp.HasValue ? TimeAgo(timestamp.Value) : fallback;
  }

  /// <summary>Formats the given difference between the current system time and the provided <paramref name="timestamp"/>
  /// into a human readable string
  /// </summary>
  /// <param name="timestamp"></param>
  /// <returns>A Human readable string</returns>
  public static string TimeAgo(DateTime timestamp)
  {
    TimeSpan timeAgo;

    timeAgo = DateTime.UtcNow.Subtract(timestamp);

    if (timeAgo.TotalSeconds < 60)
    {
      return "just now";
    }

    if (timeAgo.TotalMinutes < 60)
    {
      return $"{timeAgo.Minutes} minute{PluralS(timeAgo.Minutes)} ago";
    }

    if (timeAgo.TotalHours < 24)
    {
      return $"{timeAgo.Hours} hour{PluralS(timeAgo.Hours)} ago";
    }

    if (timeAgo.TotalDays < 7)
    {
      return $"{timeAgo.Days} day{PluralS(timeAgo.Days)} ago";
    }

    if (timeAgo.TotalDays < 30)
    {
      return $"{timeAgo.Days / 7} week{PluralS(timeAgo.Days / 7)} ago";
    }

    if (timeAgo.TotalDays < 365)
    {
      return $"{timeAgo.Days / 30} month{PluralS(timeAgo.Days / 30)} ago";
    }

    if (timestamp <= new DateTime(1800, 1, 1))
    {
      SpeckleLog.Logger.Warning(
        "Tried to calculate {functionName} of a DateTime value that was way in the past: {dateTimeValue}",
        nameof(TimeAgo),
        timestamp
      );
      // We assume this was an error, Likely a non-nullable DateTime was initialized/deserialized to the default
      // Instead of potentially lying to the user, lets tell them we don't know what happened.
      return "Unknown";
    }

    return $"{timeAgo.Days / 365} year{PluralS(timeAgo.Days / 365)} ago";
  }

  [Pure]
  public static string PluralS(int num) => num != 1 ? "s" : "";
}
