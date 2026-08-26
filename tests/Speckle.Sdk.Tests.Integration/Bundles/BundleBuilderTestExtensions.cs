using Speckle.Sdk.Bundles;

namespace Speckle.Sdk.Tests.Integration.Bundles;

/// <summary>Test sugar: intern + describe + place in one call, the shape most fixture lines want.</summary>
internal static class BundleBuilderTestExtensions
{
  public static BundleObject GetOrAddObject(
    this BundleBuilder b,
    string applicationId,
    BundleCollection? collection,
    IReadOnlyDictionary<string, object?>? properties,
    string? name = null,
    string? speckleType = null,
    string? sourceType = null,
    string? units = null,
    string? typeKey = null,
    IEnumerable<KeyValuePair<string, object?>>? rootScalars = null
  )
  {
    var obj = b.GetOrAddObject(applicationId);
    bool describes =
      properties is not null
      || name is not null
      || speckleType is not null
      || sourceType is not null
      || units is not null
      || typeKey is not null
      || rootScalars is not null;
    if (describes)
    {
      obj.SetProperties(properties, name, speckleType, sourceType, units, typeKey, rootScalars);
    }
    if (collection is not null)
    {
      obj.Collection = collection;
    }
    return obj;
  }
}
