using System.Diagnostics.CodeAnalysis;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Transports;

[GenerateAutoInterface]
[ExcludeFromCodeCoverage] //factories don't need coverage
public class ServerTransportFactory(ISpeckleHttp http, ISdkActivityFactory activityFactory) : IServerTransportFactory
{
  public ServerTransport Create(
    Account account,
    string streamId,
    int timeoutSeconds = 60,
    string? blobStorageFolder = null
  ) => new ServerTransport(http, activityFactory, account, streamId, timeoutSeconds, blobStorageFolder);
}

[GenerateAutoInterface]
[ExcludeFromCodeCoverage] //factories don't need coverage
public class ServerTransport2Factory(ISpeckleHttp http, ISdkActivityFactory activityFactory) : IServerTransport2Factory
{
  public async ValueTask<ServerTransport2> Create(
    Account account,
    string streamId,
    int timeoutSeconds = 60,
    string? blobStorageFolder = null
  )
  {
    var t = new ServerTransport2(http, activityFactory, account, streamId, timeoutSeconds, blobStorageFolder);
    await t.Start().ConfigureAwait(false);
    return t;
  }
}
