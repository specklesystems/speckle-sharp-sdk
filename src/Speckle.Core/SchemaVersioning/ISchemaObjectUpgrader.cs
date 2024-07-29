namespace Speckle.Core.SchemaVersioning;

public interface ISchemaObjectUpgrader<in TInputType, out TOutputType>
  where TInputType : class where TOutputType : class
{
  TOutputType Upgrade(TInputType input);
  
  Version UpgradeFrom { get; }
  Version UpgradeTo { get; }
}
