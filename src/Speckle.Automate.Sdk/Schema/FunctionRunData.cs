using System.Text.Json.Serialization;

namespace Speckle.Automate.Sdk.Schema;

/// <summary>
/// Required data to run a function.
/// </summary>
/// <typeparam name="T"> Type for <see cref="FunctionInputs"/>.</typeparam>
public sealed class FunctionRunData<T>
{
  [JsonRequired]
  public required string SpeckleToken { get; init; }

  [JsonRequired]
  public required AutomationRunData AutomationRunData { get; init; }

  public required T? FunctionInputs { get; init; }
}
