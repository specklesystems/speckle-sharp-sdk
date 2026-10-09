using Speckle.Objects.Utils;

namespace Speckle.Sdk.Tests.Unit.Objects.Utils;

/// <summary>The merge underneath the ENG-9302 projection: instance wins on collision, type-only subtrees are
/// shared by reference, and the shared type dictionary is never mutated.</summary>
public sealed class MergeTypeScopedTests
{
  [Fact]
  public void InstanceWins_TypeOnlyShared_TypeDictNeverMutated()
  {
    var typeOnly = new Dictionary<string, object?> { ["Width"] = 265.0 };
    var typeProps = new Dictionary<string, object?>
    {
      ["shared"] = typeOnly,
      ["both"] = new Dictionary<string, object?> { ["a"] = "type", ["typeOnlyKey"] = 1.0 },
      ["scalar"] = "type",
    };
    var instanceProps = new Dictionary<string, object?>
    {
      ["both"] = new Dictionary<string, object?> { ["a"] = "instance" },
      ["scalar"] = "instance",
      ["instOnly"] = true,
    };

    var merged = ObjectsArtifactReader.MergeTypeScoped(typeProps, instanceProps);

    Assert.Same(typeOnly, merged["shared"]); // type-only subtree shared by reference
    var both = (Dictionary<string, object?>)merged["both"]!;
    Assert.Equal("instance", both["a"]); // leaf collision: instance wins
    Assert.Equal(1.0, both["typeOnlyKey"]); // type-side sibling still arrives
    Assert.Equal("instance", merged["scalar"]);
    Assert.Equal(true, merged["instOnly"]);

    // copy-on-write: inputs untouched
    Assert.Equal("type", ((Dictionary<string, object?>)typeProps["both"]!)["a"]);
    Assert.DoesNotContain("instOnly", (IDictionary<string, object?>)typeProps);
    Assert.DoesNotContain("shared", (IDictionary<string, object?>)instanceProps);
  }
}
