using System.Diagnostics.CodeAnalysis;
using Speckle.InterfaceGenerator;
using Speckle.Sdk.Credentials;

namespace Speckle.Sdk.Transports;

[GenerateAutoInterface]
[ExcludeFromCodeCoverage] //factories don't need coverage
public class ServerTransportFactory : IServerTransportFactory
{
  public IServerTransport Create(
    Account account,
    string streamId,
    int timeoutSeconds = 60,
    string? blobStorageFolder = null
  ) => new ServerTransport(account, streamId, timeoutSeconds, blobStorageFolder);
}
