#if NET8_0_OR_GREATER
using AwesomeAssertions;
using Speckle.Sdk.Pipelines.Send.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines;

/// <summary>
/// Regression: the Revit `ParameterExtractor` builds parameter groups as strongly-typed
/// nested dictionaries (<c>Dictionary&lt;string, Dictionary&lt;string, object?&gt;&gt;</c>). The native
/// EAV walk used to test <c>val is IReadOnlyDictionary&lt;string, object?&gt;</c>, which is FALSE for
/// those typed dicts (invariant TValue), so the entire Instance/Type parameter subtree was silently
/// dropped from eav. This asserts the typed nested dicts now flatten to parameter rows.
/// </summary>
public sealed class EavExtractionParameterTests
{
  [Fact]
  public void TypedNestedParameterDicts_AreFlattened()
  {
    // Mirrors RevitObject.properties shape: a top-level "Parameters" dict whose
    // "Instance Parameters" value is a Dictionary<string, Dictionary<string, object?>>
    // (group -> { paramName -> { value, name, units, internalDefinitionName } }).
    var properties = new Dictionary<string, object?>
    {
      ["elementId"] = "698364",
      ["Parameters"] = new Dictionary<string, object?>
      {
        ["Instance Parameters"] = new Dictionary<string, Dictionary<string, object?>>
        {
          ["Constraints"] = new Dictionary<string, object?>
          {
            ["Base Offset"] = new Dictionary<string, object?>
            {
              ["value"] = 0.0,
              ["name"] = "Base Offset",
              ["units"] = "ft",
              ["internalDefinitionName"] = "WALL_BASE_OFFSET_PARAM",
            },
          },
        },
        ["Type Parameters"] = new Dictionary<string, Dictionary<string, object?>>
        {
          ["Other"] = new Dictionary<string, object?>
          {
            ["Type Mark"] = new Dictionary<string, object?>
            {
              ["value"] = "W1",
              ["name"] = "Type Mark",
            },
          },
        },
      },
    };

    var rows = new List<EavRow>();
    EavExtraction.FlattenProperties("obj1", properties, rootScalars: null, excludedTopLevelProperties: null, rows);

    // class prop still there
    rows.Should().ContainSingle(r => r.Path == "properties.elementId");

    // the instance parameter flattened to a single row at its grouped path, with value/units/idn
    var baseOffset = rows.Should()
      .ContainSingle(r => r.Path == "properties.Parameters.Instance Parameters.Constraints.Base Offset")
      .Subject;
    baseOffset.ValueNum.Should().Be(0.0);
    baseOffset.Units.Should().Be("ft");
    baseOffset.InternalDefinitionName.Should().Be("WALL_BASE_OFFSET_PARAM");

    // type parameter too
    rows.Should()
      .ContainSingle(r =>
        r.Path == "properties.Parameters.Type Parameters.Other.Type Mark" && r.ValueText == "W1"
      );
  }
}
#endif
