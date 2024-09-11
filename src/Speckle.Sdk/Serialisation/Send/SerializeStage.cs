using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.Send;

public record Serialized(string Id, string Json);

public class SerializeStage
{
  public long Serialized { get; private set; }

  public async ValueTask<Serialized> Execute(Base @base)
  {
    var serializer = new SpeckleObjectSerializer2();
    var json = await serializer.SerializeAsync(@base).ConfigureAwait(false);
    Serialized++;
    return new(@base.id, json);
  }
}
