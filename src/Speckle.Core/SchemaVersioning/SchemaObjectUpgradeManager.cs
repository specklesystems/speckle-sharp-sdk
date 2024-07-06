using System.Reflection;
using Speckle.Core.Models;
using Speckle.Core.Reflection;

namespace Speckle.Core.SchemaVersioning;

public class SchemaObjectUpgradeManager<TInputType, TOutputType> : ISchemaObjectUpgradeManager<TInputType, TOutputType>
  where TInputType : class where TOutputType : class
{
  // POC: use manual activation, newing the interface and caching the upgrader but...
  // we will need to inject this later most likely
  private readonly ITypeInstanceResolver<ISchemaObjectUpgrader<TInputType, TOutputType>> _typeInstanceResolver;
  
  public SchemaObjectUpgradeManager(ITypeInstanceResolver<ISchemaObjectUpgrader<TInputType, TOutputType>>  typeInstanceResolver)
  {
    _typeInstanceResolver = typeInstanceResolver;
  }

  public TOutputType UpgradeObject(TInputType input, string typeName, Version inputVersion, Version schemaVersion)
  {
    TOutputType upgraded;
    
    // we try and upgrade while-ever the versions don't match
    while (inputVersion < schemaVersion)
    {
      // building this must be done consistently - maybe some helper?
      string typeKey = $"{typeName}{inputVersion}";

      if (!_typeInstanceResolver.TryResolve(typeKey, out ISchemaObjectUpgrader<TInputType, TOutputType> upgrader))
      {
        // there's no upgrader for this
        break;
      }

      upgraded = upgrader.Upgrade(input);
      inputVersion = upgrader.UpgradeTo;
    }

    return null!;//input;
  }
}
