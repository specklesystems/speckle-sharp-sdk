using Speckle.Core.Credentials;
using Speckle.InterfaceGenerator;

namespace Speckle.Core.Transports;

[GenerateAutoInterface]
public class ServerTransportFactory : IServerTransportFactory
{
  public IServerTransport Create(Account account, string streamId, int timeoutSeconds, string? blobStorageFolder) =>
    new ServerTransport(account, streamId, timeoutSeconds, blobStorageFolder);
}
