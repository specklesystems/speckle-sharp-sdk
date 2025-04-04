using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Unit.Host;

[SpeckleType("Speckle.Core.Tests.Unit.Kits.FakeMesh")]
public class FakeMesh : Base
{
  [DetachProperty, Chunkable]
  public List<double> Vertices { get; set; } = new();

  [DetachProperty, Chunkable]
  public double[] ArrayOfDoubles { get; set; }

  [DetachProperty, Chunkable]
  public TableLeg[] ArrayOfLegs { get; set; }

  [DetachProperty, Chunkable(2500)]
  public List<Tabletop> Tables { get; set; } = new();
}

[SpeckleType("Speckle.Core.Tests.Unit.Kits.DiningTable")]
public class DiningTable : Base
{
  public DiningTable()
  {
    LegOne = new TableLeg { height = 2 * 3, radius = 10 };
    LegTwo = new TableLeg { height = 1, radius = 5 };

    MoreLegs.Add(new TableLeg { height = 4 });
    MoreLegs.Add(new TableLeg { height = 10 });

    Tabletop = new Tabletop
    {
      length = 200,
      width = 12,
      thickness = 3,
    };
  }

  [DetachProperty]
  public TableLeg LegOne { get; set; }

  [DetachProperty]
  public TableLeg LegTwo { get; set; }

  [DetachProperty]
  public List<TableLeg> MoreLegs { get; set; } = new();

  [DetachProperty]
  public Tabletop Tabletop { get; set; }

  public string TableModel { get; set; } = "Sample Table";
}

[SpeckleType("Speckle.Core.Tests.Unit.Kits.Tabletop")]
public class Tabletop : Base
{
  public double length { get; set; }
  public double width { get; set; }
  public double thickness { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Kits.TableLeg")]
public class TableLeg : Base
{
  public double height { get; set; }
  public double radius { get; set; }

  [DetachProperty]
  public TableLegFixture fixture { get; set; } = new();
}

[SpeckleType("Speckle.Core.Tests.Unit.Kits.TableLegFixture")]
public class TableLegFixture : Base
{
  public string nails { get; set; } = "MANY NAILS WOW ";
}

[SpeckleType("Speckle.Core.Tests.Unit.Kits.Point")]
public class Point : Base
{
  public Point() { }

  public Point(double x, double y, double z)
  {
    X = x;
    Y = y;
    Z = z;
  }

  public double X { get; set; }
  public double Y { get; set; }
  public double Z { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Kits.SuperPoint")]
public class SuperPoint : Point
{
  public double W { get; set; }
}

[SpeckleType("Speckle.Core.Tests.Unit.Kits.Mesh")]
public class Mesh : Base
{
  public List<int> Faces = new();

  [JsonIgnore]
  public List<Point> Points = new();

  public List<double> Vertices
  {
    get => Points.SelectMany(pt => new List<double> { pt.X, pt.Y, pt.Z }).ToList();
    set
    {
      for (int i = 0; i < value.Count; i += 3)
      {
        Points.Add(new Point(value[i], value[i + 1], value[i + 2]));
      }
    }
  }
}

public interface ICurve
{
  // Just for fun
}

/// <summary>
/// Store individual points in a list structure for developer ergonomics. Nevertheless, for performance reasons (hashing, serialisation and storage) expose the same list of points as a typed array.
/// </summary>
[SpeckleType("Speckle.Core.Tests.Unit.Kits.Polyline")]
public class Polyline : Base, ICurve
{
  [JsonIgnore]
  public List<Point> Points { get; set; } = new();

  public List<double> Vertices
  {
    get => Points.SelectMany(pt => new List<double> { pt.X, pt.Y, pt.Z }).ToList();
    set
    {
      for (int i = 0; i < value.Count; i += 3)
      {
        Points.Add(new Point(value[i], value[i + 1], value[i + 2]));
      }
    }
  }
}

[SpeckleType("Speckle.Core.Tests.Unit.Kits.Line")]
public class Line : Base, ICurve
{
  public Point Start { get; set; }
  public Point End { get; set; }
}

/// <summary>
/// This class exists to purely test some weird cases in which Intefaces might trash serialisation.
/// </summary>
[SpeckleType("Speckle.Core.Tests.Unit.Kits.PolygonalFeline")]
public class PolygonalFeline : Base
{
  public List<ICurve> Whiskers { get; set; } = new();

  public Dictionary<string, ICurve> Claws { get; set; } = new();

  [DetachProperty]
  public ICurve Tail { get; set; }

  public ICurve[] Fur { get; set; } = new ICurve[1000];
}
