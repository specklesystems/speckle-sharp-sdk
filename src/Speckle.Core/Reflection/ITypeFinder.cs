using System.Reflection;

namespace Speckle.Core.Reflection;

public interface ITypeFinder
{
  IList<Type> GetTypesWhereSubclassOf(IEnumerable<Assembly> assemblies, Type subclassOf);
}
