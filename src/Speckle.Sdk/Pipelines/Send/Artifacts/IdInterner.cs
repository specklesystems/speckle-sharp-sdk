#if NETSTANDARD2_0 || NET8_0_OR_GREATER
namespace Speckle.Sdk.Pipelines.Send.Artifacts;

/// <summary>
/// Dense per-namespace <c>string → int32</c> interner for the Speckle 4.0 artefact
/// identity scheme. Each identity namespace (object / geometry / node) owns one
/// interner; ids are <c>0..N-1</c> in first-seen order. The same key always maps to
/// the same id, which is how a value referenced from many edges (a shared material, a
/// reused geometry) collapses to one <c>K</c>.
///
/// <see cref="GetOrAdd(string, out int)"/> returns <c>true</c> only when the key is
/// newly minted, so callers write the backing row (the dictionary entry, the node, the
/// geometry blob) exactly once. Not thread-safe: the converter loop is sequential.
/// </summary>
public sealed class IdInterner
{
  private readonly Dictionary<string, int> _map = new(StringComparer.Ordinal);

  /// <summary>Number of distinct keys interned so far (also the next id to be minted).</summary>
  public int Count => _map.Count;

  /// <summary>
  /// Resolves <paramref name="key"/> to its dense id, minting it on first sight.
  /// Returns <c>true</c> iff the key was newly added — the caller should then write its
  /// backing row.
  /// </summary>
  public bool GetOrAdd(string key, out int id)
  {
    if (_map.TryGetValue(key, out id))
    {
      return false;
    }
    id = _map.Count;
    _map.Add(key, id);
    return true;
  }

  /// <summary>Convenience overload when the caller doesn't need the newly-added flag.</summary>
  public int GetOrAdd(string key)
  {
    GetOrAdd(key, out var id);
    return id;
  }
}
#endif
