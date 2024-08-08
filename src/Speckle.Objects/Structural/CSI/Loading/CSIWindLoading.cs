using Speckle.Objects.Structural.CSI.Analysis;
using Speckle.Objects.Structural.Loading;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.CSI.Loading;

[SpeckleType("Objects.Structural.CSI.Loading.CSIWindLoadingFace")]
public class CSIWindLoadingFace : LoadFace
{
  public double Cp { get; set; }

  public WindPressureType WindPressureType { get; set; }
}
