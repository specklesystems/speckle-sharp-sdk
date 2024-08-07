using Speckle.Sdk.Models;

namespace Speckle.Objects.Structural.CSI.Analysis;

[SpeckleType("Objects.Structural.CSI.Analysis.CSIAnalysis")]
public class CSIAnalysis : Base
{
  public ActiveDOFs activeDOFs { get; set; }
  public FloorMeshSettings floorMeshSettings { get; set; }
}

[SpeckleType("Objects.Structural.CSI.Analysis.ActiveDOFs")]
public class ActiveDOFs : Base
{
  public ActiveDOFs() { }

  public ActiveDOFs(bool UX, bool UY, bool UZ, bool RX, bool RY, bool RZ)
  {
    this.UX = UX;
    this.UY = UY;
    this.UZ = UZ;
    this.RX = RX;
    this.RY = RY;
    this.RZ = RZ;
  }

  public bool UX { get; set; }
  public bool UY { get; set; }
  public bool UZ { get; set; }
  public bool RX { get; set; }
  public bool RY { get; set; }
  public bool RZ { get; set; }
}

[SpeckleType("Objects.Structural.CSI.Analysis.FloorMeshSettings")]
public class FloorMeshSettings : Base
{
  public FloorMeshSettings() { }

  public FloorMeshSettings(MeshOption meshOption, double maximumMeshSize)
  {
    this.meshOption = meshOption;
    this.maximumMeshSize = maximumMeshSize;
  }

  public MeshOption meshOption { get; set; }
  public double maximumMeshSize { get; set; }
}

[SpeckleType("Objects.Structural.CSI.Analysis.WallMeshSettings")]
public class WallMeshSettings : Base
{
  public WallMeshSettings() { }

  public WallMeshSettings(double maximumMeshSize)
  {
    this.maximumMeshSize = maximumMeshSize;
  }

  public double maximumMeshSize { get; set; }
}

[SpeckleType("Objects.Structural.CSI.Analysis.CrackingAnalysisOptions")]
public class CrackingAnalysisOptions : Base
{
  public CrackingAnalysisOptions() { }

  public CrackingAnalysisOptions(string reinforcementSource, double minTensionRatio, double minCompressionRatio)
  {
    this.reinforcementSource = reinforcementSource;
    this.minTensionRatio = minTensionRatio;
    this.minCompressionRatio = minCompressionRatio;
  }

  public string reinforcementSource { get; set; }
  public double minTensionRatio { get; set; }
  public double minCompressionRatio { get; set; }
}

[SpeckleType("Objects.Structural.CSI.Analysis.SAPFireOptions")]
public class SAPFireOptions : Base
{
  public SAPFireOptions() { }

  public SAPFireOptions(SolverOption solverOption, AnalysisProcess analysisProcess)
  {
    this.solverOption = solverOption;
    this.analysisProcess = analysisProcess;
  }

  public SolverOption solverOption { get; set; }
  public AnalysisProcess analysisProcess { get; set; }
}

public enum MeshOption { }

public enum SolverOption { }

public enum AnalysisProcess { }
