using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Credentials;

public class Account : IEquatable<Account>
{
  private string _id;

  /// <remarks>
  /// The account id is unique to user and server url.
  /// </remarks>
  /// <exception cref="InvalidOperationException">Account object invalid: missing required info</exception>
  public string id
  {
    get
    {
      if (_id == null)
      {
        if (serverInfo == null || userInfo == null)
        {
          throw new InvalidOperationException("Incomplete account info: cannot generate id.");
        }

        _id = Md5.GetString(userInfo.email + serverInfo.url).ToUpperInvariant();
      }
      return _id;
    }
    set => _id = value;
  }

  public string token { get; set; }

  public string? refreshToken { get; set; }

  public bool isDefault { get; set; }

  [Obsolete("Not used in v3")]
  public bool isOnline { get; set; } = true;

  public ServerInfo serverInfo { get; set; }

  public UserInfo userInfo { get; set; }

  #region private methods

  private static string CleanURL(string server)
  {
    if (Uri.TryCreate(server, UriKind.Absolute, out Uri? newUri))
    {
      server = newUri.Authority;
    }

    return server;
  }

  #endregion

  #region public methods


  /// <remarks>The logic is aligned with <c>distinct_id</c> mixpanel property</remarks>
  /// <exception cref="ArgumentNullException">Thrown if <see name="userInfo.email"/> was <see langword="null"/></exception>
  public string GetHashedEmail()
  {
    string email = userInfo.email.NotNull();
    return "@" + Md5.GetString(email).ToUpperInvariant();
  }

  /// <remarks>The logic is aligned with <c>server</c> mixpanel property</remarks>
  /// <exception cref="ArgumentNullException">Thrown if <see name="serverInfo.url"/> was <see langword="null"/></exception>
  public string GetHashedServer()
  {
    string url = serverInfo.url.NotNull();
    return Md5.GetString(CleanURL(url)).ToUpperInvariant();
  }

  public override string ToString()
  {
    return $"Account ({userInfo.email} | {serverInfo.url})";
  }

  public bool Equals(Account? other)
  {
    return other is not null && other.userInfo.email == userInfo.email && other.serverInfo.url == serverInfo.url;
  }

  public override bool Equals(object? obj)
  {
    return obj is Account acc && Equals(acc);
  }

  public override int GetHashCode()
  {
#if  NETSTANDARD2_0
    return Speckle.Sdk.Common.HashCode.Of(userInfo.email).And(serverInfo.url);
#else
    return HashCode.Combine(userInfo.email, serverInfo.url);
#endif
  }

  #endregion
}
