namespace Speckle.Sdk.Credentials;

public readonly record struct AuthApp(string AppId, string AppSecret, Uri CallbackUrl)
{
  //These values are defined on the server, and specify the scopes the app is requesting
  public static AuthApp ConnectorsV3 { get; } =
    new()
    {
      AppId = "connectorsV3",
      AppSecret = "connectorsV3",
      CallbackUrl = new Uri("http://localhost:29355"),
    };
}
