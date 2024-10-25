using Speckle.Sdk.Models;

namespace Speckle.Objects.Other;

[SpeckleType("Objects.Other.ObjectSymbols")]
public class ObjectSymbols : Base
{
  // in QGIS: https://qgis.org/pyqgis/master/core/QgsSymbol.html
  // in ArcGIS: https://pro.arcgis.com/en/pro-app/latest/sdk/api-reference/topic5484.html
  public List<ISymbol> symbols { get; set; }
}

[SpeckleType("Objects.Other.MarkerSymbolLayer")]
public class MarkerSymbolLayer : Base, ISymbolLayer
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

[SpeckleType("Objects.Other.LineSymbolLayer")]
public class LineSymbolLayer : Base, ISymbolLayer
{
  public string symbolUnits { get; set; }
  public LineType lineType { get; set; }
  public double lineWidth { get; set; }
  public int lineColor { get; set; }
  public int displayPriority { get; set; }
}

[SpeckleType("Objects.Other.FillSymbolLayer")]
public class FillSymbolLayer : Base, ISymbolLayer
{
  public string symbolUnits { get; set; }
  public LineType lineType { get; set; }
  public double lineWidth { get; set; }
  public int lineColor { get; set; }
  public int displayPriority { get; set; }

  public int fillColor { get; set; }
  public FillType type { get; set; }
}
