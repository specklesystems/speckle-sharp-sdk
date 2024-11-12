using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Speckle.Sdk.Dependencies;

public static class Pools
{
  public static Pool<Dictionary<string, object?>> ObjectDictionaries { get; } = new(new ObjectDictionaryPolicy());

  private sealed class ObjectDictionaryPolicy : IPooledObjectPolicy<Dictionary<string, object?>>
  {
    public Dictionary<string, object?> Create() => new(50, StringComparer.OrdinalIgnoreCase);

    public bool Return(Dictionary<string, object?> obj)
    {
      obj.Clear();
      return true;
    }
  }

  public static Pool<StringBuilder> StringBuilders { get; } =
    new(new StringBuilderPooledObjectPolicy() { MaximumRetainedCapacity = 100 * 1024 * 1024 });
}
