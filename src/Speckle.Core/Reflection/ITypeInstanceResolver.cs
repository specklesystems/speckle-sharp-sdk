namespace Speckle.Core.Reflection;

public interface ITypeInstanceResolver<TType> where TType : class
{
  TType Resolve(string typeNameWithSuffix);
  bool TryResolve(string typeNameWithSuffix, out TType instance);
}
