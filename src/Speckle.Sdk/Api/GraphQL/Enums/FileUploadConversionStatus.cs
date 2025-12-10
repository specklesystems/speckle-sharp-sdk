// ReSharper disable InconsistentNaming
namespace Speckle.Sdk.Api.GraphQL.Enums;

/// <remarks>
/// This enum isn't explicitly defined in the schema, instead its usages are int typed (But represent an enum)
/// </remarks>
public enum FileUploadConversionStatus
{
  Queued = 0,
  Processing = 1,
  Success = 2,
  Error = 3,
}
