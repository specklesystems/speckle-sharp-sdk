using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Api.Blob;

[GenerateAutoInterface]
public sealed class BlobApiFactory(ISpeckleHttp speckleHttp, ISdkActivityFactory activityFactory) : IBlobApiFactory
{
  public IBlobApi Create(Account account, int timeoutSeconds = BlobApi.DEFAULT_TIMEOUT_SECONDS) =>
    new BlobApi(speckleHttp, activityFactory, account, timeoutSeconds);
}
