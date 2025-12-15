using System.Runtime.InteropServices;
using Speckle.Sdk.Api.GraphQL.Models;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Credentials;

[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public class Account : IEquatable<Account>
{
  private string? _id;

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

  /// <remarks>
  /// We often use <see langword="null"/> token to represent an unauthed account for testing purposes.
  /// However, for most other cases, you should specify a token
  /// </remarks>>
  public required string? token { get; set; }

  public string? refreshToken { get; set; }

  public bool isDefault { get; set; }
  public bool isOnline { get; set; } = true;

  public required ServerInfo serverInfo { get; set; }

  public required UserInfo userInfo { get; set; }

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

  internal const string LOCAL_IDENTIFIER_DEPRECATION_MESSAGE = "Local identifiers no longer nesseary";

  /// <summary>
  /// Retrieves the local identifier for the current user.
  /// </summary>
  /// <returns>
  /// Returns a <see cref="Uri"/> object representing the local identifier for the current user.
  /// The local identifier is created by appending the user ID as a query parameter to the server URL.
  /// </returns>
  /// <remarks>
  /// Notice that the generated Uri is not intended to be used as a functioning Uri, but rather as a
  /// unique identifier for a specific account in a local environment. The format of the Uri, containing a query parameter with the user ID,
  /// serves this specific purpose. Therefore, it should not be used for forming network requests or
  /// expecting it to lead to an actual webpage. The primary intent of this Uri is for unique identification in a Uri format.
  /// </remarks>
  /// <example>
  ///   This sample shows how to call the GetLocalIdentifier method.
  ///   <code>
  ///     Uri localIdentifier = GetLocalIdentifier();
  ///     Console.WriteLine(localIdentifier);
  ///   </code>
  ///   For a fictional `User ID: 123` and `Server: https://speckle.xyz`, the output might look like this:
  ///   <code>
  ///     https://speckle.xyz?id=123
  ///   </code>
  /// </example>
  [Obsolete(LOCAL_IDENTIFIER_DEPRECATION_MESSAGE)]
  internal Uri GetLocalIdentifier() => new($"{serverInfo.url}?id={userInfo.id}");
}
