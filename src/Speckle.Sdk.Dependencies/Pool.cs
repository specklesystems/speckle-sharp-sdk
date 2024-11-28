using Microsoft.Extensions.ObjectPool;

namespace Speckle.Sdk.Dependencies;

public class Pool<T>
  where T : class, new()
{
  private readonly ObjectPool<T> _objectPool;
  
  public Pool() : this(new DefaultPooledObjectPolicy<T>())
  {
  }

  internal Pool(IPooledObjectPolicy<T> objectPolicy)
  {
    _objectPool = ObjectPool.Create(objectPolicy);
  }

  public T Get() => _objectPool.Get();

  public void Return(T obj) => _objectPool.Return(obj);
}
