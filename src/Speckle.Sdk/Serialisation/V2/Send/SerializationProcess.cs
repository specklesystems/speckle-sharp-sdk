using Speckle.Sdk.Models;
using Speckle.Sdk.Serialisation.V2.Receive;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialisation.V2.Send;

public class SerializationProcess(IProgress<ProgressArgs>? progress, IObjectLoader objectLoader)
{
  public async Task Serialize(
    IAsyncEnumerable<Base> rootId,
    CancellationToken cancellationToken
  )
  {
    
  }
}
