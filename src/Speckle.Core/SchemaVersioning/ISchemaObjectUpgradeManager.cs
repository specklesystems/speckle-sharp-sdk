namespace Speckle.Core.SchemaVersioning;

public interface ISchemaObjectUpgradeManager<TInputType, TOutputType>
  where TInputType : class where TOutputType : class
{
  TOutputType UpgradeObject(TInputType input, string typeName, Version inputVersion, Version schemaVersion);
}
