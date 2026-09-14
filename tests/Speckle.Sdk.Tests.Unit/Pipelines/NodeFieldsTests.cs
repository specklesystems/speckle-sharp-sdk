using Speckle.Sdk.Pipelines;
using Speckle.Sdk.Pipelines.Receive.Artifacts;

namespace Speckle.Sdk.Tests.Unit.Pipelines;

/// <summary>The row → per-kind projection is where a mandatory column's NULL stops being silently defaulted.</summary>
public sealed class NodeFieldsTests
{
  private static ArtefactNode Material(double? opacity) =>
    new(NodeKind.Material, "Concrete", null, null, null, unchecked((int)0xFF808080), opacity, 0.0, 0.8, null);

  [Fact]
  public void Material_CompleteRow_Projects()
  {
    var fields = NodeFields.Material(7, Material(1.0));

    Assert.Equal("Concrete", fields.Name);
    Assert.Equal(unchecked((int)0xFF808080), fields.Argb);
    Assert.Equal(1.0, fields.Opacity);
    Assert.Equal(0.0, fields.Metalness);
    Assert.Equal(0.8, fields.Roughness);
    Assert.Null(fields.Emissive);
    Assert.Null(fields.Ior);
  }

  [Fact]
  public void Material_NullMandatoryColumn_ThrowsNamingNodeAndColumn()
  {
    var ex = Assert.Throws<InvalidOperationException>(() => NodeFields.Material(7, Material(null)));

    Assert.Contains("Node 7", ex.Message, StringComparison.Ordinal);
    Assert.Contains("nodes.opacity", ex.Message, StringComparison.Ordinal);
  }

  [Fact]
  public void Color_DoesNotCarryOpacity()
  {
    var node = new ArtefactNode(
      NodeKind.Color,
      null,
      null,
      null,
      null,
      unchecked((int)0xFFFF0000),
      0.5,
      null,
      null,
      null
    );

    // The spec dropped opacity from COLOR: alpha rides the argb byte, so a stray column value is not read.
    Assert.Equal(unchecked((int)0xFFFF0000), NodeFields.Color(3, node).Argb);
  }
}
