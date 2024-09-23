using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialisation.Send;

public record Serialized(
  string Id,
  string Json,
  Base BaseObject,
  IReadOnlyDictionary<string, ObjectReference> ConvertedReferences
);

public sealed class SerializeStage(SqliteManagerOptions options) : IDisposable
{
  private readonly SqliteManager _sqliteManager = new(options);
  public long Serialized { get; private set; }

  public Serialized? Execute(Base @base, CancellationToken cancellationToken)
  {
    if (options.Enabled && _sqliteManager.HasObject(@base.id, cancellationToken))
    {
      return null;
    }
    var serializer = new SpeckleObjectSerializer2(SpeckleObjectSerializer2Pool.Instance);
    var json = serializer.Serialize(@base);
    Serialized++;
    return new(@base.id, json, @base, serializer.ObjectReferences);
  }

  public void Dispose() => _sqliteManager.Dispose();
}
