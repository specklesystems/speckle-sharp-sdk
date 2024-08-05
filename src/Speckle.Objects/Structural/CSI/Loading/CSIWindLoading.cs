using Speckle.Objects.Structural.CSI.Analysis;
using Speckle.Objects.Structural.Loading;

namespace Speckle.Objects.Structural.CSI.Loading;

public class CSIWindLoadingFace : LoadFace
{
  public double Cp { get; set; }

  public WindPressureType WindPressureType { get; set; }
}
