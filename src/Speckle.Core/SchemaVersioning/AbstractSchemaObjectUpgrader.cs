using Speckle.Core.Models;

namespace Speckle.Core.SchemaVersioning;

public abstract class AbstractSchemaObjectBaseUpgrader<TInputType, TOutputType> : ISchemaObjectUpgrader<Base, Base>
  where TInputType : Base
  where TOutputType : Base
{
  public AbstractSchemaObjectBaseUpgrader(Version from, Version to)
  {
    UpgradeFrom = from;
    UpgradeTo = to;
  }
  
  public Base Upgrade(Base input)
  {
    return Upgrade((TInputType) input);
  }

  public abstract TOutputType Upgrade(TInputType input);
  
  public Version UpgradeFrom { get; private set; }
  public Version UpgradeTo { get; private set; }
}
