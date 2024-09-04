using System.Diagnostics.CodeAnalysis;
using Speckle.Sdk.Serialisation.Utilities;

namespace Speckle.Sdk.Transports;

public static class TransportHelpers
{
  public static async Task<string> CopyObjectAndChildrenAsync(
    string id,
    ITransport sourceTransport,
    ITransport targetTransport,
    Action<int>? onTotalChildrenCountKnown,
    CancellationToken cancellationToken
  )
  {
    if (string.IsNullOrEmpty(id))
    {
      throw new ArgumentException("Cannot copy object with empty id", nameof(id));
    }

    cancellationToken.ThrowIfCancellationRequested();

    var parent = await sourceTransport.GetObject(id).ConfigureAwait(false);
    if (parent is null)
    {
      throw new TransportException(
        $"Requested id {id} was not found within this transport {sourceTransport.TransportName}"
      );
    }

    targetTransport.SaveObject(id, parent);

    var closures = (await ClosureParser.GetChildrenIdsAsync(parent).ConfigureAwait(false)).ToList();

    onTotalChildrenCountKnown?.Invoke(closures.Count);

    int i = 0;
    foreach (var closure in closures)
    {
      cancellationToken.ThrowIfCancellationRequested();

      //skips blobs because ServerTransport downloads things separately
      if (closure.StartsWith("blob:"))
      {
        continue;
      }
      var child = await sourceTransport.GetObject(closure).ConfigureAwait(false);
      if (child is null)
      {
        throw new TransportException(
          $"Closure id {closure} was not found within this transport {sourceTransport.TransportName}"
        );
      }

      targetTransport.SaveObject(closure, child);
      var count = i++;
      sourceTransport.OnProgressAction?.Invoke(new ProgressArgs(ProgressEvent.UploadObject, count, closures.Count));
    }

    return parent;
  }

  [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Deserialization target for DTO")]
  internal sealed class Placeholder
  {
    public Dictionary<string, int>? __closure { get; set; }
  }
}
