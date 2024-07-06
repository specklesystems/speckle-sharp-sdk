using Speckle.Core.Models;

namespace Speckle.Core.SchemaVersioning;

public abstract class AbstractSchemaObjectBaseUpgrader<TInputType, TOutputType> : ISchemaObjectUpgrader<Base, Base>
  where TInputType : Base
  where TOutputType : Base
{
  public Base Upgrade(Base input)
  {
    return Upgrade((TInputType) input);
  }

  public abstract TOutputType Upgrade(TInputType input);
  
  public Version UpgradeFrom { get; protected set; }
  public Version UpgradeTo { get; protected set; }
}
