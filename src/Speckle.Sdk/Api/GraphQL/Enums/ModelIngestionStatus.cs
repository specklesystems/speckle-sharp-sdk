// ReSharper disable InconsistentNaming
namespace Speckle.Sdk.Api.GraphQL.Enums;

/// <remarks>
/// string based enum
/// </remarks>
public enum ModelIngestionStatus
{
  cancelled,
  failed,
  invalidInput,
  processing,
  queued,
  success,
  timeout,
}
