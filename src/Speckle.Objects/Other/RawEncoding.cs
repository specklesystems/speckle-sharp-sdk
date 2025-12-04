using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

/// <summary>
/// Keeps track of a raw-encoded object in a native supported format. see <see cref="RawEncodingFormats"/>
/// </summary>
[SpeckleType("Objects.Other.RawEncoding")]
public class RawEncoding : Base // note: at this stage, since we're using this for extrusions and subds the name doesn't make sense anymore
{
  public required string format { get; set; }
  public required string contents { get; set; }

  public RawEncoding() { }
}

/// <summary>
/// Supported encoding types "strongly" typed strings. This needs to match the extension of the file format.
/// </summary>
public static class RawEncodingFormats
{
  public const string RHINO_3DM = "3dm";
  public const string ACAD_DWG = "dwg";
  public const string ACAD_SAT = "sat";
}
