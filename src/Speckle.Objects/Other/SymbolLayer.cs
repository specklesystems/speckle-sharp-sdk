using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

[SpeckleType("Objects.Other.Symbol")]
public class Symbol : Base
{
  public List<ISymbolLayer> symbolLayers { get; set; }
}

[SpeckleType("Objects.Other.MarkerSymbolLayer")]
public class MarkerSymbolLayer : Base, ISymbolLayer
{
  public string symbolUnits { get; set; }
  public LineStyleType lineType { get; set; }
  public double lineWidth { get; set; }
  public int lineColor { get; set; }
  public int displayPriority { get; set; }

  public int fillColor { get; set; }
  public MarkerType type { get; set; }
}

[SpeckleType("Objects.Other.LineSymbolLayer")]
public class LineSymbolLayer : Base, ISymbolLayer
{
  public string symbolUnits { get; set; }
  public LineStyleType lineType { get; set; }
  public double lineWidth { get; set; }
  public int lineColor { get; set; }
  public int displayPriority { get; set; }
}

[SpeckleType("Objects.Other.FillSymbolLayer")]
public class FillSymbolLayer : Base, ISymbolLayer
{
  public string symbolUnits { get; set; }
  public LineStyleType lineType { get; set; }
  public double lineWidth { get; set; }
  public int lineColor { get; set; }
  public int displayPriority { get; set; }

  public int fillColor { get; set; }
  public FillType type { get; set; }
}
