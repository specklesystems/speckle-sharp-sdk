using Speckle.InterfaceGenerator;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public class ServerBlobManagerFactory(ISpeckleHttp speckleHttp, ISdkActivityFactory activityFactory)
  : IServerBlobManagerFactory
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
    return new ServerBlobManager(client);
  }
}
