using System.Diagnostics.CodeAnalysis;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;
using Speckle.Sdk.Helpers;

namespace Speckle.Sdk.Transports;

[GenerateAutoInterface]
[ExcludeFromCodeCoverage] //factories don't need coverage
public class ServerTransportFactory(
  ISpeckleHttpClientHandlerFactory speckleHttpClientHandlerFactory,
  ISpeckleHttp speckleHttp
) : IServerTransportFactory
{
  public IServerTransport Create(
    Account account,
    string streamId,
    int timeoutSeconds = 60,
    string? blobStorageFolder = null
  ) =>
    new ServerTransport(
      speckleHttpClientHandlerFactory,
      speckleHttp,
      account,
      streamId,
      timeoutSeconds,
      blobStorageFolder
    );
}
