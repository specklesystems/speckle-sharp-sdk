using Speckle.Objects.Structural.Materials;
using Speckle.Sdk.Models;

namespace Speckle.Objects.BuiltElements.TeklaStructures;

#region Obsolete
[Obsolete("Deprecated in 2.17: Create a TeklaRebarGroup class instead")]
[SpeckleType("Objects.BuiltElements.TeklaStructures.TeklaRebar")]
public class TeklaRebar : Rebar
{
  public string name { get; set; }

  [DetachProperty]
  public Hook startHook { get; set; }

  [DetachProperty]
  public Hook endHook { get; set; }

  public double classNumber { get; set; }
  public string size { get; set; }

  [DetachProperty]
  public StructuralMaterial material { get; set; }
}

[Obsolete("Deprecated in 2.17: Use a RebarHook class instead")]
[SpeckleType("Objects.BuiltElements.TeklaStructures.Hook")]
public class Hook : Base
{
  public double angle { get; set; }
  public double length { get; set; }
  public double radius { get; set; }
  public shape shape { get; set; }
}

[Obsolete("Deprecated in 2.17: set starthook and endhook to null or refer to hook angle instead")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Obsolete")]
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
public enum shape
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
{
  NO_HOOK = 0,
  HOOK_90_DEGREES = 1,
  HOOK_135_DEGREES = 2,
  HOOK_180_DEGREES = 3,
  CUSTOM_HOOK = 4,
}
#endregion
