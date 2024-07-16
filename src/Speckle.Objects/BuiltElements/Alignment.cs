using System;
using System.Collections.Generic;
using Objects.Geometry;
using Speckle.Core.Models;
using Speckle.Newtonsoft.Json;

namespace Objects.BuiltElements;

public class Alignment : Base, IDisplayValue<List<Polyline>>
{
  [JsonIgnore, Obsolete("Use curves property")]
  public ICurve baseCurve { get; set; }

  public List<ICurve> curves { get; set; }

  public string name { get; set; }

  public double startStation { get; set; }

  public double endStation { get; set; }

  public List<Profile> profiles { get; set; }

  /// <summary>
  /// Station equation list contains doubles indicating raw station back, station back, and station ahead for each station equation
  /// </summary>
  public List<double> stationEquations { get; set; }

  /// <summary>
  /// Station equation direction for the corresponding station equation should be true for increasing or false for decreasing
  /// </summary>
  public List<bool> stationEquationDirections { get; set; }

  public string units { get; set; }

  [DetachProperty]
  public List<Polyline> displayValue { get; set; }
}
