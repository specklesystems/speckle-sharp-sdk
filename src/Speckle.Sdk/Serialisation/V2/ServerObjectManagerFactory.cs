using Speckle.InterfaceGenerator;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
public class ServerObjectManagerFactory(ISpeckleHttp speckleHttp, ISdkActivityFactory activityFactory)
  : IServerObjectManagerFactory
{
  public IServerObjectManager Create(Uri url, string streamId, string? authorizationToken, int timeoutSeconds = 120) =>
    new ServerObjectManager(speckleHttp, activityFactory, url, streamId, authorizationToken, timeoutSeconds);
}
