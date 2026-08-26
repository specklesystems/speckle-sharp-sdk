using System.Collections;

namespace Speckle.Sdk.Pipelines.Receive.Artifacts;

/// <summary>
/// The EAV property rows of a bundle kept in the layout the file has: parallel columns
/// (<c>key</c>, <c>path_index</c>, <c>value_*</c>) sorted by key, plus the interned path list. An object's properties
/// are a contiguous row range — two ints per object — instead of a tree of dictionaries and boxed values, so memory
/// stays ≈ the parquet size and "which objects carry path X" is one column scan.
/// </summary>
/// <remarks>
/// <c>key</c> is <c>object_index</c> for the instance table (<c>eav.eav</c>) and <c>type_index</c> for the type table
/// (<c>eav.type_eav</c>); both share the row shape. Values are coalesced boolean → double → string, like the nested
/// reader, and rows with no value or an unknown path are dropped at load.
/// </remarks>
public sealed class PropertyTable
{
  private readonly int[] _key;
  private readonly int[] _pathId;
  private readonly string?[] _str;
  private readonly double?[] _dbl;
  private readonly bool?[] _bool;
  private readonly string[] _pathById;
  private readonly Dictionary<string, int> _idByPath;
  private readonly Dictionary<int, (int Start, int Count)> _range;

  private PropertyTable(
    int[] key,
    int[] pathId,
    string?[] str,
    double?[] dbl,
    bool?[] @bool,
    string[] pathById,
    Dictionary<string, int> idByPath,
    Dictionary<int, (int, int)> range
  )
  {
    _key = key;
    _pathId = pathId;
    _str = str;
    _dbl = dbl;
    _bool = @bool;
    _pathById = pathById;
    _idByPath = idByPath;
    _range = range;
  }

  public static readonly PropertyTable Empty = new(
    [],
    [],
    [],
    [],
    [],
    [],
    new Dictionary<string, int>(StringComparer.Ordinal),
    new Dictionary<int, (int, int)>()
  );

  /// <summary>Builds the table from an eav-shaped parquet table and the paths table.</summary>
  /// <param name="eav">Rows of (<paramref name="keyColumn"/>, path_index, value_string, value_double, value_boolean).</param>
  /// <param name="paths">Rows of (path_index, path).</param>
  /// <param name="keyColumn"><c>"object_index"</c> or <c>"type_index"</c>.</param>
  public static PropertyTable Load(ParquetTable? eav, ParquetTable paths, string keyColumn)
  {
    // ── paths: dense array by path_index + reverse map ─────────────────────────────────────────────────
    var pIdx = paths.Ints("path_index");
    var pStr = paths.Strings("path");
    int maxId = -1;
    for (int i = 0; i < pIdx.Length; i++)
    {
      maxId = Math.Max(maxId, pIdx[i]);
    }
    var pathById = new string[maxId + 1];
    var idByPath = new Dictionary<string, int>(pIdx.Length, StringComparer.Ordinal);
    for (int i = 0; i < pIdx.Length; i++)
    {
      string p = pStr[i] ?? "";
      pathById[pIdx[i]] = p;
      if (p.Length > 0)
      {
        idByPath[p] = pIdx[i];
      }
    }

    if (eav is null || !eav.Has(keyColumn))
    {
      return new PropertyTable([], [], [], [], [], pathById, idByPath, new Dictionary<int, (int, int)>());
    }

    // ── rows: keep only valued rows with a known path, then order by key (stable) ─────────────────────
    var key = eav.Ints(keyColumn);
    var pathId = eav.Ints("path_index");
    var str = eav.Strings("value_string");
    var dbl = eav.NullableDoubles("value_double");
    var bln = eav.NullableBools("value_boolean");

    var keep = new List<int>(key.Length);
    for (int i = 0; i < key.Length; i++)
    {
      bool hasValue = bln[i].HasValue || dbl[i].HasValue || str[i] is not null;
      bool knownPath = (uint)pathId[i] < (uint)pathById.Length && pathById[pathId[i]].Length > 0;
      if (hasValue && knownPath)
      {
        keep.Add(i);
      }
    }
    var order = keep.ToArray();
    Array.Sort(order, (a, b) => key[a] != key[b] ? key[a].CompareTo(key[b]) : a.CompareTo(b));

    int n = order.Length;
    var sKey = new int[n];
    var sPath = new int[n];
    var sStr = new string?[n];
    var sDbl = new double?[n];
    var sBool = new bool?[n];
    var range = new Dictionary<int, (int, int)>();
    for (int r = 0; r < n; r++)
    {
      int i = order[r];
      sKey[r] = key[i];
      sPath[r] = pathId[i];
      sStr[r] = str[i];
      sDbl[r] = dbl[i];
      sBool[r] = bln[i];
      if (r == 0 || sKey[r] != sKey[r - 1])
      {
        range[sKey[r]] = (r, 1);
      }
      else
      {
        var (start, count) = range[sKey[r]];
        range[sKey[r]] = (start, count + 1);
      }
    }
    return new PropertyTable(sKey, sPath, sStr, sDbl, sBool, pathById, idByPath, range);
  }

  public int RowCount => _key.Length;

  /// <summary>Every distinct property path in the table, as stored (e.g. <c>"properties.Constraints.Base Offset"</c>).</summary>
  public IReadOnlyList<string> Paths => _pathById;

  /// <summary>Number of keys (objects or types) that carry at least one property.</summary>
  public int KeyCount => _range.Count;

  public IEnumerable<int> Keys => _range.Keys;

  public bool Contains(int key) => _range.ContainsKey(key);

  /// <summary>The interned id of a path, or -1 when the model has no property under that path.</summary>
  public int PathId(string path) => _idByPath.TryGetValue(path, out int id) ? id : -1;

  /// <summary>A dictionary-shaped view over one key's rows. Empty view for unknown keys.</summary>
  public PropertyView this[int key] =>
    _range.TryGetValue(key, out var r) ? new PropertyView(this, r.Start, r.Count, prefix: null) : PropertyView.Empty;

  /// <summary>A view over one key's rows restricted to paths under <paramref name="prefix"/> (a dotted root such as
  /// <c>"properties"</c>), with the prefix stripped from the exposed keys.</summary>
  public PropertyView Under(int key, string prefix) =>
    _range.TryGetValue(key, out var r) ? new PropertyView(this, r.Start, r.Count, prefix + ".") : PropertyView.Empty;

  public bool TryGetValue(int key, string path, out object? value)
  {
    int row = FindRow(key, path);
    value = row >= 0 ? ValueAt(row) : null;
    return row >= 0;
  }

  public string? GetString(int key, string path) => FindRow(key, path) is var r && r >= 0 ? _str[r] : null;

  public double? GetDouble(int key, string path) => FindRow(key, path) is var r && r >= 0 ? _dbl[r] : null;

  public bool? GetBool(int key, string path) => FindRow(key, path) is var r && r >= 0 ? _bool[r] : null;

  /// <summary>Keys (objects/types) carrying a property under <paramref name="path"/> — one scan of the path column.</summary>
  public IEnumerable<int> KeysWith(string path)
  {
    int id = PathId(path);
    if (id < 0)
    {
      yield break;
    }
    int last = int.MinValue;
    for (int r = 0; r < _pathId.Length; r++)
    {
      if (_pathId[r] == id && _key[r] != last)
      {
        last = _key[r];
        yield return last;
      }
    }
  }

  /// <summary>(key, value) for every row under <paramref name="path"/> — the column projection a query wants.</summary>
  public IEnumerable<KeyValuePair<int, object?>> ValuesOf(string path)
  {
    int id = PathId(path);
    if (id < 0)
    {
      yield break;
    }
    for (int r = 0; r < _pathId.Length; r++)
    {
      if (_pathId[r] == id)
      {
        yield return new KeyValuePair<int, object?>(_key[r], ValueAt(r));
      }
    }
  }

  // ── row access (used by PropertyView) ────────────────────────────────────────────────────────────────

  internal string PathAt(int row) => _pathById[_pathId[row]];

  internal int PathIdAt(int row) => _pathId[row];

  internal object? ValueAt(int row) =>
    _bool[row].HasValue ? _bool[row]
    : _dbl[row].HasValue ? _dbl[row]
    : _str[row];

  internal string? StringAt(int row) => _str[row];

  internal double? DoubleAt(int row) => _dbl[row];

  internal bool? BoolAt(int row) => _bool[row];

  internal int FindRow(int key, string path)
  {
    if (!_range.TryGetValue(key, out var r) || !_idByPath.TryGetValue(path, out int id))
    {
      return -1;
    }
    return FindRow(r.Start, r.Count, id);
  }

  internal int FindRow(int start, int count, int pathId)
  {
    int end = start + count;
    for (int r = start; r < end; r++)
    {
      if (_pathId[r] == pathId)
      {
        return r;
      }
    }
    return -1;
  }
}

/// <summary>
/// One key's properties as a read-only dictionary, backed by a row range of a <see cref="PropertyTable"/> — no
/// per-object allocation until enumerated. Optionally scoped to a path prefix, with the prefix stripped from keys.
/// </summary>
public readonly struct PropertyView : IReadOnlyDictionary<string, object?>
{
  private readonly PropertyTable _table;
  private readonly int _start;
  private readonly int _count;
  private readonly string? _prefix; // "properties." — includes the trailing dot

  internal PropertyView(PropertyTable table, int start, int count, string? prefix)
  {
    _table = table;
    _start = start;
    _count = count;
    _prefix = prefix;
  }

  public static PropertyView Empty => new(PropertyTable.Empty, 0, 0, null);

  private int Row(string key) => _table.FindRow(_start, _count, _table.PathId(_prefix is null ? key : _prefix + key));

  private bool Included(int row, out string key)
  {
    string path = _table.PathAt(row);
    if (_prefix is null)
    {
      key = path;
      return true;
    }
    if (path.StartsWith(_prefix, StringComparison.Ordinal))
    {
      key = path.Substring(_prefix.Length);
      return true;
    }
    key = "";
    return false;
  }

  public object? this[string key] => TryGetValue(key, out var v) ? v : throw new KeyNotFoundException(key);

  public bool ContainsKey(string key) => Row(key) >= 0;

  public bool TryGetValue(string key, out object? value)
  {
    int row = Row(key);
    value = row >= 0 ? _table.ValueAt(row) : null;
    return row >= 0;
  }

  public string? GetString(string key) => Row(key) is var r && r >= 0 ? _table.StringAt(r) : null;

  public double? GetDouble(string key) => Row(key) is var r && r >= 0 ? _table.DoubleAt(r) : null;

  public bool? GetBool(string key) => Row(key) is var r && r >= 0 ? _table.BoolAt(r) : null;

  public int Count
  {
    get
    {
      if (_prefix is null)
      {
        return _count;
      }
      int n = 0;
      for (int r = _start; r < _start + _count; r++)
      {
        if (Included(r, out _))
        {
          n++;
        }
      }
      return n;
    }
  }

  public IEnumerable<string> Keys
  {
    get
    {
      for (int r = _start; r < _start + _count; r++)
      {
        if (Included(r, out var key))
        {
          yield return key;
        }
      }
    }
  }

  public IEnumerable<object?> Values
  {
    get
    {
      for (int r = _start; r < _start + _count; r++)
      {
        if (Included(r, out _))
        {
          yield return _table.ValueAt(r);
        }
      }
    }
  }

  public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
  {
    for (int r = _start; r < _start + _count; r++)
    {
      if (Included(r, out var key))
      {
        yield return new KeyValuePair<string, object?>(key, _table.ValueAt(r));
      }
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  /// <summary>Materializes this view as the nested dictionary shape (dotted paths split into sub-dictionaries) —
  /// what the <c>Base</c> projection and older consumers walk. Allocates; use only where nesting is required.</summary>
  public Dictionary<string, object?> ToNested()
  {
    var root = new Dictionary<string, object?>();
    foreach (var kv in this)
    {
      var parts = kv.Key.Split('.');
      var cursor = root;
      for (int i = 0; i < parts.Length - 1; i++)
      {
        if (cursor.TryGetValue(parts[i], out var next) && next is Dictionary<string, object?> nd)
        {
          cursor = nd;
        }
        else
        {
          var nd2 = new Dictionary<string, object?>();
          cursor[parts[i]] = nd2;
          cursor = nd2;
        }
      }
      cursor[parts[^1]] = kv.Value;
    }
    return root;
  }
}
