using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

[SpeckleType("Objects.Other.MarkerSymbol")]
public class MarkerSymbol : Base, ISymbol
{
  public string symbolUnits { get; set; }
  public LineType lineType { get; set; }
  public double lineWidth { get; set; }
  public int lineColor { get; set; }
  public int displayPriority { get; set; }

  public double angle { get; set; }
  public int fillColor { get; set; }
  public MarkerType type { get; set; }
}

[SpeckleType("Objects.Other.LineSymbol")]
public class LineSymbol : Base, ISymbol
{
  public string symbolUnits { get; set; }
  public LineType lineType { get; set; }
  public double lineWidth { get; set; }
  public int lineColor { get; set; }
  public int displayPriority { get; set; }
}

[SpeckleType("Objects.Other.FillSymbol")]
public class FillSymbol : Base, ISymbol
{
  public string symbolUnits { get; set; }
  public LineType lineType { get; set; }
  public double lineWidth { get; set; }
  public int lineColor { get; set; }
  public int displayPriority { get; set; }

  public int fillColor { get; set; }
  public FillType type { get; set; }
}
