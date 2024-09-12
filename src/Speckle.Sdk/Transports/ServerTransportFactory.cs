using System.Diagnostics.CodeAnalysis;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Transports;

[GenerateAutoInterface]
[ExcludeFromCodeCoverage] //factories don't need coverage
public class ServerTransportFactory(ISpeckleHttp http, ISpeckleHttpClientHandlerFactory speckleHttpClientHandlerFactory) : IServerTransportFactory
{
  public IServerTransport Create(
    Account account,
    string streamId,
    int timeoutSeconds = 60,
    string? blobStorageFolder = null
  ) => new ServerTransport(http, speckleHttpClientHandlerFactory, account, streamId, timeoutSeconds, blobStorageFolder);
}
