using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.Send;

public record Serialized(
  string Id,
  string Json,
  Base BaseObject,
  IReadOnlyDictionary<string, ObjectReference> ConvertedReferences
);

public class SerializeStage
{
  public long Serialized { get; private set; }

  public Serialized Execute(Base @base)
  {
    var serializer = new SpeckleObjectSerializer2();
    var json = serializer.Serialize(@base);
    Serialized++;
    return new(@base.id, json, @base, serializer.ObjectReferences);
  }
}
