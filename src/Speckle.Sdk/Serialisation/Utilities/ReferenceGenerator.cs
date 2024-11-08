namespace Speckle.Sdk.Serialisation.Utilities;

public static class ReferenceGenerator
{
  private const string REFERENCE_JSON_START = "{\"speckle_type\":\"reference\",\"referencedId\":\"";
  private const string REFERENCE_JSON_END = "\",\"__closure\":null}";

  public static Json CreateReference(Id id) => new(REFERENCE_JSON_START + id.Value + REFERENCE_JSON_END);
}
