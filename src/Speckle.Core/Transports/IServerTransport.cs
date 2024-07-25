namespace Speckle.Core.Transports;

public interface IServerTransport : IDisposable, ITransport, IBlobCapableTransport, ICloneable
{
  int TotalSentBytes { get; }
  Credentials.Account Account { get; }
  Uri BaseUri { get; }
  string StreamId { get; }
  int TimeoutSeconds { get; set; }
}
