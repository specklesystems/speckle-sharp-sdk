using Speckle.Sdk.Models;

namespace Speckle.Sdk.Transports;

/// <summary>
/// Interface defining the contract for transport implementations.
/// </summary>
public interface ITransport
{
  /// <summary>
  /// Human readable name for the transport
  /// </summary>
  public string TransportName { get; set; }

  /// <summary>
  /// Extra descriptor properties of the given transport.
  /// </summary>
  public Dictionary<string, object> TransportContext { get; }

  /// <summary>
  ///  Show how much time the transport was busy for.
  /// </summary>
  public TimeSpan Elapsed { get; }

  /// <summary>
  /// Should be checked often and gracefully stop all in progress sending if requested.
  /// </summary>
  public CancellationToken CancellationToken { get; set; }

  /// <summary>
  /// Used to report progress during the transport's longer operations.
  /// </summary>
  public IProgress<ProgressArgs>? OnProgressAction { get; set; }

  /// <summary>
  /// Signals to the transport that writes are about to begin.
  /// </summary>
  public void BeginWrite();

  /// <summary>
  /// Signals to the transport that no more items will need to be written.
  /// </summary>
  public void EndWrite();

  /// <summary>
  /// Saves an object.
  /// </summary>
  /// <param name="id">The hash of the object.</param>
  /// <param name="serializedObject">The full string representation of the object</param>
  /// <exception cref="TransportException">Failed to save object</exception>
  /// <exception cref="OperationCanceledException"><see cref="CancellationToken"/> requested cancel</exception>
  public void SaveObject(string id, string serializedObject);

  /// <summary>
  /// Awaitable method to figure out whether writing is completed.
  /// </summary>
  /// <returns></returns>
  public Task WriteComplete();

  /// <param name="id">The object's hash.</param>
  /// <returns>The serialized object data, or <see langword="null"/> if the transport cannot find the object</returns>
  /// <exception cref="OperationCanceledException"><see cref="CancellationToken"/> requested cancel</exception>
  public Task<string?> GetObject(string id);

  /// <summary>
  /// Copies the parent object and all its children to the provided transport.
  /// </summary>
  /// <param name="id">The id of the object you want to copy.</param>
  /// <param name="targetTransport">The transport you want to copy the object to.</param>
  /// <returns>The string representation of the root object.</returns>
  /// <exception cref="ArgumentException">The provided arguments are not valid</exception>
  /// <exception cref="TransportException">The transport could not complete the operation</exception>
  /// <exception cref="OperationCanceledException"><see cref="CancellationToken"/> requested cancel</exception>
  public Task<string> CopyObjectAndChildren(string id, ITransport targetTransport);

  /// <summary>
  /// Checks if objects are present in the transport
  /// </summary>
  /// <param name="objectIds">List of object ids to check</param>
  /// <returns>A dictionary with the specified object ids as keys and boolean values, whether each object is present in the transport or not</returns>
  /// <exception cref="TransportException">The transport could not complete the operation</exception>
  /// <exception cref="OperationCanceledException"><see cref="CancellationToken"/> requested cancel</exception>
  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds);
}

public interface IBlobCapableTransport
{
  public string BlobStorageFolder { get; }

  public void SaveBlob(Blob obj);

  // NOTE: not needed, should be implemented in "CopyObjectsAndChildren"
  //public void GetBlob(Blob obj);
}
