using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation;

public record Serialized(string Id, string Json);
public class SerializeStage
{
  public async  ValueTask<Serialized> Execute(Base @base)
  {
    var serializer = new SpeckleObjectSerializer2();
    var json = await serializer.SerializeAsync(@base).ConfigureAwait(false);
    return new(@base.id, json);
  }
}
