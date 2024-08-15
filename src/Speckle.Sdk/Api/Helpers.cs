using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.InteropServices;
using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Common;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Host;
using Speckle.Sdk.Logging;
using Speckle.Sdk.Models;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Api;

public static class Helpers
{
  public const string RELEASES_URL = "https://releases.speckle.dev";
  private const string FEEDS_ENDPOINT = RELEASES_URL + "/manager2/feeds";

  /// <summary>
  /// Helper method to Receive from a Speckle Server.
  /// </summary>
  /// <param name="stream">Stream URL or Id to receive from. If the URL contains branchName, commitId or objectId those will be used, otherwise the latest commit from main will be received.</param>
  /// <param name="account">Account to use. If not provided the default account will be used.</param>
  /// <param name="onProgressAction">Action invoked on progress iterations.</param>
  /// <param name="onTotalChildrenCountKnown">Action invoked once the total count of objects is known.</param>
  /// <returns></returns>
  public static async Task<Base> Receive(
    this IServerTransportFactory serverTransportFactory,
    string stream,
    Account? account = null,
    Action<ConcurrentBag<ProgressArgs>>? onProgressAction = null,
    Action<int>? onTotalChildrenCountKnown = null
  )
  {
    var sw = new StreamWrapper(stream);

    try
    {
      account ??= await sw.GetAccount().ConfigureAwait(false);
    }
    catch (SpeckleException)
    {
      if (string.IsNullOrEmpty(sw.StreamId))
      {
        throw;
      }

      //Fallback to a non authed account
      account = new Account
      {
        token = "",
        serverInfo = new ServerInfo { url = sw.ServerUrl },
        userInfo = new UserInfo()
      };
    }

    using var client = new Client(account);
    using var transport = serverTransportFactory.Create(client.Account, sw.StreamId);

    string objectId = "";
    Commit? commit = null;

    //OBJECT URL
    if (!string.IsNullOrEmpty(sw.ObjectId))
    {
      objectId = sw.ObjectId.NotNull();
    }
    //COMMIT URL
    else if (!string.IsNullOrEmpty(sw.CommitId))
    {
      commit = await client.CommitGet(sw.StreamId, sw.CommitId.NotNull()).ConfigureAwait(false);
      objectId = commit.referencedObject;
    }
    //BRANCH URL OR STREAM URL
    else
    {
      var branchName = string.IsNullOrEmpty(sw.BranchName) ? "main" : sw.BranchName;

      var branch = await client.BranchGet(sw.StreamId, branchName.NotNull(), 1).ConfigureAwait(false);
      if (branch.commits.items.Count == 0)
      {
        throw new SpeckleException("The selected branch has no commits.");
      }

      commit = branch.commits.items[0];
      objectId = branch.commits.items[0].referencedObject;
    }

    Analytics.TrackEvent(
      client.Account,
      Analytics.Events.Receive,
      new Dictionary<string, object>
      {
        { "sourceHostApp", HostApplications.GetHostAppFromString(commit.NotNull().sourceApplication).Slug },
        { "sourceHostAppVersion", commit.sourceApplication }
      }
    );

    var receiveRes = await Operations
      .Receive(
        objectId,
        transport,
        onProgressAction: onProgressAction,
        onTotalChildrenCountKnown: onTotalChildrenCountKnown
      )
      .ConfigureAwait(false);

    try
    {
      await client
        .CommitReceived(
          new CommitReceivedInput
          {
            streamId = sw.StreamId,
            commitId = commit?.id,
            message = commit?.message,
            sourceApplication = "Other"
          }
        )
        .ConfigureAwait(false);
    }
    catch (Exception ex) when (!ex.IsFatal())
    {
      // Do nothing!
    }
    return receiveRes;
  }

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
