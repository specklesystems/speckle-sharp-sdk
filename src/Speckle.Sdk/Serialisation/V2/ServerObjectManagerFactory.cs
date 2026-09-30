using Speckle.InterfaceGenerator;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Serialisation.V2;

[GenerateAutoInterface]
[Obsolete(Json.DEPRECATION_MESSAGE)]
public class ServerObjectManagerFactory(ISpeckleHttp speckleHttp, ISdkActivityFactory activityFactory)
  : IServerObjectManagerFactory
{
  public IServerObjectManager Create(Uri url, string streamId, string? authorizationToken, TimeSpan? timeout = null) =>
    new ServerObjectManager(
      speckleHttp,
      activityFactory,
      url,
      streamId,
      authorizationToken,
      new ServerObjectManagerOptions(timeout: timeout)
    );
}
