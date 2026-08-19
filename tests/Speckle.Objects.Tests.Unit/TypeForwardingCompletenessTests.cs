using System.Reflection;
using AwesomeAssertions;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Tests.Unit;

/// <summary>
/// The Speckle.Objects shell must forward every public Speckle.Objects.* type that lives in the
/// merged Speckle.Sdk assembly — a missed forwarder is a runtime <see cref="TypeLoadException"/>
/// for consumers compiled against a pre-merge Speckle.Objects (ADR: dissolve the Sdk/Objects split).
/// </summary>
public class TypeForwardingCompletenessTests
{
  /// <summary>
  /// Deliberately not forwarded: added during the big-truck major, never shipped in a released
  /// pre-merge Speckle.Objects nupkg, so no compiled consumer can reference them there.
  /// </summary>
  private static readonly HashSet<string> _intentionallyNotForwarded = new()
  {
    "Speckle.Objects.Utils.SgeoDecoder",
    "Speckle.Objects.Utils.SgeoEncoder",
    "Speckle.Objects.Utils.SgeoFormat",
    "Speckle.Objects.Utils.SgeoHeader",
    "Speckle.Objects.Utils.SgeoFlags",
    "Speckle.Objects.Utils.SgeoPrimitiveType",
    "Speckle.Objects.Utils.SgeoMesh",
    "Speckle.Objects.Utils.ObjectsArtifactPipeline",
    "Speckle.Objects.Utils.ObjectsArtifactReader",
    "Speckle.Objects.Utils.ArtifactReceiveOptions",
  };

  [Fact]
  public void Every_public_Objects_type_in_Sdk_is_forwarded_by_the_shell()
  {
    // Nested types resolve through their forwarded declaring type and cannot be forwarded directly.
    var exported = typeof(Base)
      .Assembly.GetExportedTypes()
      .Where(t =>
        !t.IsNested
        && t.Namespace is not null
        && (t.Namespace == "Speckle.Objects" || t.Namespace.StartsWith("Speckle.Objects."))
      )
      .Select(t => t.FullName!)
      .ToHashSet();

    // PolySharp also emits forwards for runtime polyfills (e.g. IsExternalInit); only the
    // Speckle.Objects.* surface is under test.
    var forwarded = Assembly
      .Load(new AssemblyName("Speckle.Objects"))
      .GetForwardedTypes()
      .Where(t => t.Namespace == "Speckle.Objects" || t.Namespace?.StartsWith("Speckle.Objects.") == true)
      .Select(t => t.FullName!)
      .ToHashSet();

    var missing = exported.Except(forwarded).Except(_intentionallyNotForwarded).Order().ToList();
    var stale = forwarded.Except(exported).Order().ToList();
    var pointlesslyAllowlisted = _intentionallyNotForwarded.Except(exported).Order().ToList();

    missing.Should().BeEmpty("every public Speckle.Objects.* type needs a TypeForwardedTo in the shell");
    stale.Should().BeEmpty("forwarders must point at types that still exist in Speckle.Sdk");
    pointlesslyAllowlisted.Should().BeEmpty("the allowlist should only name types that exist in Speckle.Sdk");
  }
}
