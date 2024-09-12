using System.Diagnostics.CodeAnalysis;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;
using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Transports;

[GenerateAutoInterface]
[ExcludeFromCodeCoverage] //factories don't need coverage
public class ServerTransportFactory(ISpeckleHttp http, ISpeckleHttpClientHandlerFactory speckleHttpClientHandlerFactory, IActivityFactory activityFactory) : IServerTransportFactory
{
  public IServerTransport Create(
    Account account,
    string streamId,
    int timeoutSeconds = 60,
    string? blobStorageFolder = null
  ) => new ServerTransport(http, speckleHttpClientHandlerFactory, activityFactory, account, streamId, timeoutSeconds, blobStorageFolder);
}
