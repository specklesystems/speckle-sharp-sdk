namespace Speckle.Core.Reflection;

public class SingletonTypeInstanceResolver<TType> : ITypeInstanceResolver<TType>
  where TType : class
{
  private readonly ITypeFinder _typeFinder;
  private readonly Dictionary<string, TType> _typeInstances = new();
  
  public SingletonTypeInstanceResolver(ITypeFinder typeFinder)
  {
    _typeFinder = typeFinder;
    
    // POC: not wild about evaluating this during construction but... is that still a thing?
    // could be done on the fly... 
    var foundTypes = _typeFinder.GetTypesWhereSubclassOf(AppDomain.CurrentDomain.GetAssemblies(), typeof(TType));
    
    // let's make an instance of each of these
    // could also be done on the fly
    foreach (var type in foundTypes)
    {
      // the type must have the attribute
      // POC: we may want something other than default exception...
      // maybe but there should always be one ONLY one
      var namedType = (NamedTypeAttribute) type.GetCustomAttributes(typeof(NamedTypeAttribute), false).Single();
      _typeInstances[namedType.TypeNameWithSuffix] = (TType) Activator.CreateInstance(type);
    }
  }

  public TType Resolve(string typeNameWithSuffix) => _typeInstances[typeNameWithSuffix];

  public bool TryResolve(string typeNameWithSuffix, out TType instance)
  {
    if (_typeInstances.TryGetValue(typeNameWithSuffix, out instance))
    {
      return true;
    }

    return false;
  }
}
