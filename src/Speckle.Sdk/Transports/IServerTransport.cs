namespace Speckle.Sdk.Transports;

public interface IServerTransport : IDisposable, ITransport, IBlobCapableTransport, ICloneable
{
  Credentials.Account Account { get; }
  Uri BaseUri { get; }
  string StreamId { get; }
  int TimeoutSeconds { get; set; }
}
