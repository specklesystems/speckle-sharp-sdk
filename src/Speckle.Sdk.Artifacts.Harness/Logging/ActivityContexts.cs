using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Artifacts.Harness.Logging;

/// <summary>
/// Reusable helper functions for adding specific context to activity tags
/// </summary>
internal static class ActivityContexts
{
  public static IEnumerable<KeyValuePair<string, object?>> UserInfoContext(Account account)
  {
    yield return new(LoggingConfiguration.USER_ID, account.userInfo.id);
    yield return new(LoggingConfiguration.USER_DISTINCT_ID, account.GetHashedEmail());
    yield return new(LoggingConfiguration.USER_SERVER_URL, new Uri(account.serverInfo.url).ToString());
  }
}
