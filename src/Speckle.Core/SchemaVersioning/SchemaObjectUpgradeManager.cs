using Speckle.Core.Common;
using Speckle.Core.Logging;
using Speckle.Core.Reflection;
using Speckle.Core.Serialisation;

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

  public TOutputType UpgradeObject(TInputType input, string typeName, Version inputSchemaVersion, Version loadedSchemaVersion)
  {
    TInputType? upgradeTarget = input;
    TOutputType? upgraded = null;
    
    // we try and upgrade while-ever the versions don't match
    while (inputSchemaVersion < loadedSchemaVersion)
    {
      // building this must be done consistently
      string typeKey = NamedTypeAttribute.CreateTypeNameWithKeySuffix(typeName, inputSchemaVersion.ToString());

      // POC: do we expect there must always be types?
      if (!_typeInstanceResolver.TryResolve(typeKey, out ISchemaObjectUpgrader<TInputType, TOutputType> upgrader))
      {
        // there's no upgrader for this
        break;
      }

      upgraded = upgrader.Upgrade(upgradeTarget.NotNull());
      inputSchemaVersion = upgrader.UpgradeTo;
      
      upgradeTarget = upgraded as TInputType;
      if (upgradeTarget is null)
      {
        throw new SpeckleDeserializeException(
          $"The type {upgraded.GetType()} cannot be cast to type {typeof(TInputType)}");
      }
      
      // POC - we should support mutation of types so the typeName must be able to change:
      // https://spockle.atlassian.net/browse/DUI3-502
    }

    // if we didn't do any upgrading, then we should just pass the input directly to the output
    // BUT in cases where there is a conversion 
    if (upgraded is null)
    {
      upgraded = input as TOutputType;
      if (upgraded is null)
      {
        // POC: we'll want some exception type here because we probably want this to explode always
        // even if we change from Base to an IBase, it will be easy to do in a big bang, because Base can IBase cam implement
        // I *think* this means we can neatly use this same class with different input and output types
        // to nicely migrate to a different base if we wished to
        // could be tackled in: https://spockle.atlassian.net/browse/DUI3-502
        throw new SpeckleException(
          ($"Failed to convert '{input.GetType().FullName}' to '{typeof(TOutputType).FullName}'"));
      }
    }

    return upgraded;
  }
}
