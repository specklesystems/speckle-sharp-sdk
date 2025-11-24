using Speckle.InterfaceGenerator;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public sealed class ServerBlobManagerFactory(ISpeckleHttp speckleHttp) : IServerBlobManagerFactory
{
  public IServerBlobManager Create(
    Uri serverUrl,
    string projectId,
    string? authorizationToken,
    TimeSpan? timeout = null
  )
  {
    var client = speckleHttp.CreateHttpClient(authorizationToken: authorizationToken);
    client.BaseAddress = serverUrl;
    return new ServerBlobManager(client, projectId);
  }
}
