using System.Runtime.Serialization;
using Speckle.Newtonsoft.Json;
using Speckle.Objects.Other;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Objects.Geometry;

/// <summary>
/// Represents a "Boundary Representation" Solid
/// </summary>
[SpeckleType("Objects.Geometry.Brep")]
public class Brep : Base, IHasArea, IHasVolume, IHasBoundingBox, ITransformable<Brep>, IDisplayValue<List<Mesh>>
{
  /// <summary>
  /// The unit's this object's coordinates are in.
  /// </summary>
  /// <remarks>
  /// This should be one of <see cref="Units"/>
  /// </remarks>
  public required string units { get; set; }

  /// <summary>
  /// Gets or sets the list of surfaces in this <see cref="Brep"/> instance.
  /// </summary>
  [JsonIgnore]
  public required List<Surface> Surfaces { get; set; }

  /// <summary>
  /// Gets or sets the flat list of numbers representing the <see cref="Brep"/>'s surfaces.
  /// </summary>
  [DetachProperty, Chunkable(31250)]
  public List<double> SurfacesValue
  {
    get
    {
      var list = new List<double>();
      foreach (var srf in Surfaces)
      {
        list.AddRange(srf.ToList());
      }
      return list;
    }
    set
    {
      if (value == null)
      {
        return;
      }

      var list = new List<Surface>();
      var done = false;
      var currentIndex = 0;
      while (!done)
      {
        var len = (int)value[currentIndex];
        list.Add(Surface.FromList(value.GetRange(currentIndex + 1, len)));
        currentIndex += len + 1;
        done = currentIndex >= value.Count;
      }

      Surfaces = list;
    }
  }

  /// <summary>
  /// Gets or sets the list of 3-dimensional curves in this <see cref="Brep"/> instance.
  /// </summary>
  [JsonIgnore]
  public required List<ICurve> Curve3D { get; set; }

  /// <summary>
  /// Gets or sets the flat list of numbers representing the <see cref="Brep"/>'s 3D curves.
  /// </summary>
  /// <remarks>
  /// This is only used for the <see cref="Brep"/> class serialisation/deserialisation. You should use <see cref="Brep.Curve3D"/> instead.
  /// </remarks>
  [DetachProperty, Chunkable(31250)]
  public List<double> Curve3DValues
  {
    get => CurveArrayEncodingExtensions.ToArray(Curve3D);
    set
    {
      if (value != null)
      {
        Curve3D = CurveArrayEncodingExtensions.FromArray(value);
      }
    }
  }

  /// <summary>
  /// Gets or sets the list of 2-dimensional UV curves in this <see cref="Brep"/> instance.
  /// </summary>
  [JsonIgnore]
  public required List<ICurve> Curve2D { get; set; }

  /// <summary>
  /// Gets or sets the flat list of numbers representing the <see cref="Brep"/>'s 2D curves.
  /// </summary>
  /// <remarks>
  /// This is only used for the <see cref="Brep"/> class serialisation/deserialisation. You should use <see cref="Brep.Curve2D"/> instead.
  /// </remarks>
  [DetachProperty, Chunkable(31250)]
  public List<double> Curve2DValues
  {
    get => CurveArrayEncodingExtensions.ToArray(Curve2D);
    set
    {
      if (value != null)
      {
        Curve2D = CurveArrayEncodingExtensions.FromArray(value);
      }
    }
  }

  /// <summary>
  /// Gets or sets the list of vertices in this <see cref="Brep"/> instance.
  /// </summary>
  [JsonIgnore]
  public required List<Point> Vertices { get; set; }

  /// <summary>
  /// Gets or sets the flat list of numbers representing the <see cref="Brep"/>'s vertices.
  /// </summary>
  /// <remarks>
  /// This is only used for the <see cref="Brep"/> class serialisation/deserialisation. You should use <see cref="Brep.Vertices"/> instead.
  /// </remarks>
  [DetachProperty, Chunkable(31250)]
  public List<double> VerticesValue
  {
    get
    {
      var list = new List<double>((Vertices.Count * 3) + 1);
      list.Add(Units.GetEncodingFromUnit(units));
      foreach (var vertex in Vertices)
      {
        list.AddRange(vertex.ToList());
      }

      return list;
    }
    set
    {
      if (value != null)
      {
        var units = value.Count % 3 == 0 ? Units.None : Units.GetUnitFromEncoding(value[0]);
        Vertices = new(value.Count / 3);
        for (int i = value.Count % 3 == 0 ? 0 : 1; i < value.Count; i += 3)
        {
          Vertices.Add(new Point(value[i], value[i + 1], value[i + 2], units));
        }
      }
    }
  }

  /// <summary>
  /// Gets or sets the list of edges in this <see cref="Brep"/> instance.
  /// </summary>
  [JsonIgnore]
  public required List<BrepEdge> Edges { get; set; }

  /// <summary>
  /// Gets or sets the flat list of numbers representing the <see cref="Brep"/>'s edges.
  /// </summary>
  /// <remarks>
  /// This is only used for the <see cref="Brep"/> class serialisation/deserialisation. You should use <see cref="Brep.Edges"/> instead.
  /// </remarks>
  [DetachProperty, Chunkable(62500)]
  public List<double?> EdgesValue
  {
    get =>
      Edges
        .SelectMany(e =>
        {
          var ints = new List<double?>();
          ints.Add(e.Curve3dIndex);
          ints.Add(e.StartIndex);
          ints.Add(e.EndIndex);
          ints.Add(Convert.ToInt32(e.ProxyCurveIsReversed));
          ints.Add(e.Domain.start);
          ints.Add(e.Domain.end);
          ints.AddRange(e.TrimIndices.Select(Convert.ToDouble).Cast<double?>());
          return ints.Prepend(ints.Count);
        })
        .ToList();
    set
    {
      Edges = new List<BrepEdge>();
      if (value == null || value.Count == 0)
      {
        return;
      }

      var i = 0;
      while (i < value.Count)
      {
        int n = Convert.ToInt32(value[i]);

        var loopValues = value.GetRange(i + 1, n);
        var curve3dIndex = Convert.ToInt32(loopValues[0]);
        var startIndex = Convert.ToInt32(loopValues[1]);
        var endIndex = Convert.ToInt32(loopValues[2]);
        var proxyReversed = Convert.ToBoolean(loopValues[3]);
        var domainStart = loopValues[4];
        var domainEnd = loopValues[5];
        Interval domain =
          domainStart.HasValue && domainEnd.HasValue
            ? new() { start = domainStart.Value, end = domainEnd.Value }
            : Interval.UnitInterval;

        var trimIndices = loopValues.GetRange(6, loopValues.Count - 6).Select(d => Convert.ToInt32(d)).ToArray();

        var edge = new BrepEdge
        {
          Brep = this,
          Curve3dIndex = curve3dIndex,
          TrimIndices = trimIndices,
          StartIndex = startIndex,
          EndIndex = endIndex,
          ProxyCurveIsReversed = proxyReversed,
          Domain = domain,
        };

        Edges.Add(edge);
        i += n + 1;
      }
    }
  }

  /// <summary>
  /// Gets or sets the list of closed UV loops in this <see cref="Brep"/> instance.
  /// </summary>
  [JsonIgnore]
  public required List<BrepLoop> Loops { get; set; }

  /// <summary>
  /// Gets or sets the flat list of numbers representing the <see cref="Brep"/>'s loops.
  /// </summary>
  /// <remarks>
  /// This is only used for the <see cref="Brep"/> class serialisation/deserialisation. You should use <see cref="Brep.Loops"/> instead.
  /// </remarks>
  [DetachProperty, Chunkable(62500)]
  public List<int> LoopsValue
  {
    get =>
      Loops
        .SelectMany(l =>
        {
          var ints = new List<int>();
          ints.Add(l.FaceIndex);
          ints.Add((int)l.Type);
          ints.AddRange(l.TrimIndices);
          return ints.Prepend(ints.Count);
        })
        .ToList();
    set
    {
      Loops = new List<BrepLoop>();
      if (value == null || value.Count == 0)
      {
        return;
      }

      var i = 0;
      while (i < value.Count)
      {
        int n = value[i];

        var loopValues = value.GetRange(i + 1, n);
        var faceIndex = loopValues[0];
        var type = (BrepLoopType)loopValues[1];
        var trimIndices = loopValues.GetRange(2, loopValues.Count - 2);
        var loop = new BrepLoop
        {
          Brep = this,
          FaceIndex = faceIndex,
          TrimIndices = trimIndices,
          Type = type,
        };
        Loops.Add(loop);
        i += n + 1;
      }
    }
  }

  /// <summary>
  /// Gets or sets the list of UV trim segments for each surface in this <see cref="Brep"/> instance.
  /// </summary>
  [JsonIgnore]
  public required List<BrepTrim> Trims { get; set; }

  /// <summary>
  /// Gets or sets the flat list of numbers representing the <see cref="Brep"/>'s trims.
  /// </summary>
  /// <remarks>
  /// This is only used for the <see cref="Brep"/> class serialisation/deserialisation. You should use <see cref="Brep.Trims"/> instead.
  /// </remarks>
  [DetachProperty, Chunkable(62500)]
  public List<int> TrimsValue
  {
    get
    {
      List<int> list = new(Trims.Count * TRIMS_ENCODING_LENGTH);
      foreach (var trim in Trims)
      {
        list.Add(trim.EdgeIndex);
        list.Add(trim.StartIndex);
        list.Add(trim.EndIndex);
        list.Add(trim.FaceIndex);
        list.Add(trim.LoopIndex);
        list.Add(trim.CurveIndex);
        list.Add(trim.IsoStatus);
        list.Add((int)trim.TrimType);
        list.Add(trim.IsReversed ? 1 : 0);
      }

      return list;
    }
    set
    {
      if (value == null)
      {
        return;
      }

      var list = new List<BrepTrim>(value.Count / TRIMS_ENCODING_LENGTH);
      for (int i = 0; i < value.Count; i += TRIMS_ENCODING_LENGTH)
      {
        var trim = new BrepTrim
        {
          Brep = this,
          EdgeIndex = value[i],
          StartIndex = value[i + 1],
          EndIndex = value[i + 2],
          FaceIndex = value[i + 3],
          LoopIndex = value[i + 4],
          CurveIndex = value[i + 5],
          IsoStatus = value[i + 6],
          TrimType = (BrepTrimType)value[i + 7],
          IsReversed = value[i + 8] == 1,
          Domain = Interval.UnitInterval, //TODO: This is a problem, see CXPLA-28
        };
        list.Add(trim);
      }

      Trims = list;
    }
  }

  private const int TRIMS_ENCODING_LENGTH = 9;

  /// <summary>
  /// Gets or sets the list of faces in this <see cref="Brep"/> instance.
  /// </summary>
  [JsonIgnore]
  public required List<BrepFace> Faces { get; set; }

  /// <summary>
  /// Gets or sets the flat list of numbers representing the <see cref="Brep"/>'s faces.
  /// </summary>
  /// <remarks>
  /// This is only used for the <see cref="Brep"/> class serialisation/deserialisation. You should use <see cref="Brep.Faces"/> instead.
  /// </remarks>
  [DetachProperty, Chunkable(62500)]
  public List<int> FacesValue
  {
    get =>
      Faces
        .SelectMany(f =>
        {
          var ints = new List<int>();
          ints.Add(f.SurfaceIndex);
          ints.Add(f.OuterLoopIndex);
          ints.Add(f.OrientationReversed ? 1 : 0);
          ints.AddRange(f.LoopIndices);
          return ints.Prepend(ints.Count);
        })
        .ToList();
    set
    {
      if (value == null || value.Count == 0)
      {
        return;
      }
      Faces = new List<BrepFace>();

      var i = 0;
      while (i < value.Count)
      {
        int n = value[i];

        var faceValues = value.GetRange(i + 1, n);
        var surfIndex = faceValues[0];
        var outerLoopIndex = faceValues[1];
        var orientationIsReversed = faceValues[2] == 1;
        var loopIndices = faceValues.GetRange(3, faceValues.Count - 3);
        var face = new BrepFace
        {
          Brep = this,
          SurfaceIndex = surfIndex,
          LoopIndices = loopIndices,
          OuterLoopIndex = outerLoopIndex,
          OrientationReversed = orientationIsReversed,
        };
        Faces.Add(face);
        i += n + 1;
      }
    }
  }

  /// <summary>
  /// Gets or sets if this <see cref="Brep"/> instance is closed or not.
  /// </summary>
  public required bool IsClosed { get; set; }

  /// <summary>
  /// Gets or sets the list of surfaces in this <see cref="Brep"/> instance.
  /// </summary>
  public required BrepOrientation Orientation { get; set; }

  /// <inheritdoc/>
  [DetachProperty]
  public required List<Mesh> displayValue { get; set; }

  /// <inheritdoc/>
  public double area { get; set; }

  /// <inheritdoc/>
  public Box? bbox { get; set; }

  /// <inheritdoc/>
  public double volume { get; set; }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out Brep transformed)
  {
    // transform display values
    var displayValues = new List<Mesh>(displayValue.Count);
    foreach (Mesh v in displayValue)
    {
      v.TransformTo(transform, out Mesh mesh);
      displayValues.Add(mesh);
    }

    // transform surfaces
    var surfaces = new List<Surface>(Surfaces.Count);
    foreach (var srf in Surfaces)
    {
      srf.TransformTo(transform, out Surface surface);
      surfaces.Add(surface);
    }

    // transform curve3d
    var success3D = true;
    var transformedCurve3D = new List<ICurve>();
    foreach (var curve in Curve3D)
    {
      if (curve is ITransformable c)
      {
        c.TransformTo(transform, out ITransformable tc);
        transformedCurve3D.Add((ICurve)tc);
      }
      else
      {
        success3D = false;
      }
    }

    // transform vertices
    var transformedVertices = new List<Point>(Vertices.Count);
    foreach (var vertex in Vertices)
    {
      vertex.TransformTo(transform, out Point transformedVertex);
      transformedVertices.Add(transformedVertex);
    }

    transformed = new Brep
    {
      units = units,
      displayValue = displayValues,
      Surfaces = surfaces,
      Curve3D = transformedCurve3D,
      Curve2D = new List<ICurve>(Curve2D),
      Vertices = transformedVertices,
      Edges = new List<BrepEdge>(Edges.Count),
      Loops = new List<BrepLoop>(Loops.Count),
      Trims = new List<BrepTrim>(Trims.Count),
      Faces = new List<BrepFace>(Faces.Count),
      IsClosed = IsClosed,
      Orientation = Orientation,
      applicationId = applicationId ?? id,
    };

    foreach (var e in Edges)
    {
      transformed.Edges.Add(
        new BrepEdge
        {
          Brep = transformed,
          Curve3dIndex = e.Curve3dIndex,
          TrimIndices = e.TrimIndices,
          StartIndex = e.StartIndex,
          EndIndex = e.EndIndex,
          ProxyCurveIsReversed = e.ProxyCurveIsReversed,
          Domain = e.Domain,
        }
      );
    }

    foreach (var l in Loops)
    {
      transformed.Loops.Add(
        new BrepLoop
        {
          Brep = transformed,
          FaceIndex = l.FaceIndex,
          TrimIndices = l.TrimIndices,
          Type = l.Type,
        }
      );
    }

    foreach (var t in Trims)
    {
      transformed.Trims.Add(
        new BrepTrim
        {
          Brep = transformed,
          EdgeIndex = t.EdgeIndex,
          FaceIndex = t.FaceIndex,
          LoopIndex = t.LoopIndex,
          CurveIndex = t.CurveIndex,
          IsoStatus = t.IsoStatus,
          TrimType = t.TrimType,
          IsReversed = t.IsReversed,
          StartIndex = t.StartIndex,
          EndIndex = t.EndIndex,
          Domain = null!,
        }
      );
    }

    foreach (var f in Faces)
    {
      transformed.Faces.Add(
        new BrepFace
        {
          Brep = transformed,
          SurfaceIndex = f.SurfaceIndex,
          LoopIndices = f.LoopIndices,
          OuterLoopIndex = f.OuterLoopIndex,
          OrientationReversed = f.OrientationReversed,
        }
      );
    }

    return success3D;
  }

  /// <inheritdoc/>
  public bool TransformTo(Transform transform, out ITransformable transformed)
  {
    var res = TransformTo(transform, out Brep brep);
    transformed = brep;
    return res;
  }

  [OnDeserialized]
  internal void OnDeserialized(StreamingContext context)
  {
    Surfaces.ForEach(s => s.units = units);

    for (var i = 0; i < Edges.Count; i++)
    {
      var e = Edges[i];
      var existing = e;
      lock (existing)
      {
        if (e.Brep != null)
        {
          e = new BrepEdge
          {
            Brep = this,
            Curve3dIndex = e.Curve3dIndex,
            TrimIndices = e.TrimIndices,
            StartIndex = e.StartIndex,
            EndIndex = e.EndIndex,
            ProxyCurveIsReversed = e.ProxyCurveIsReversed,
            Domain = e.Domain,
          };

          Edges[i] = e;
        }
        else
        {
          e.Brep = this;
        }
      }
    }

    for (var i = 0; i < Loops.Count; i++)
    {
      var l = Loops[i];
      var existingLoop = l;
      lock (existingLoop)
      {
        if (l.Brep != null)
        {
          l = new BrepLoop
          {
            Brep = this,
            FaceIndex = l.FaceIndex,
            TrimIndices = l.TrimIndices,
            Type = l.Type,
          };

          Loops[i] = l;
        }
        else
        {
          l.Brep = this;
        }
      }
    }

    for (var i = 0; i < Trims.Count; i++)
    {
      var t = Trims[i];
      var existingTrim = t;
      lock (existingTrim)
      {
        if (t.Brep != null)
        {
          t = new BrepTrim
          {
            Brep = this,
            EdgeIndex = t.EdgeIndex,
            LoopIndex = t.LoopIndex,
            CurveIndex = t.CurveIndex,
            IsoStatus = t.IsoStatus,
            TrimType = t.TrimType,
            IsReversed = t.IsReversed,
            StartIndex = t.StartIndex,
            EndIndex = t.EndIndex,
            FaceIndex = t.FaceIndex,
            Domain = Interval.UnitInterval, //TODO: This is a problem, see CXPLA-28
          };
          Trims[i] = t;
        }
        else
        {
          t.Brep = this;
        }
      }
    }

    for (var i = 0; i < Faces.Count; i++)
    {
      var f = Faces[i];
      var existingFace = f;
      lock (existingFace)
      {
        if (f.Brep != null)
        {
          f = new BrepFace
          {
            Brep = this,
            SurfaceIndex = f.SurfaceIndex,
            LoopIndices = f.LoopIndices,
            OuterLoopIndex = f.OuterLoopIndex,
            OrientationReversed = f.OrientationReversed,
          };
          Faces[i] = f;
        }
        else
        {
          f.Brep = this;
        }
      }
    }
  }
}

/// <summary>
/// Represents the orientation of a <see cref="Brep"/>
/// </summary>
public enum BrepOrientation
{
  /// Brep has no specific orientation
  None = 0,

  /// Brep faces inward
  Inward = -1,

  /// Brep faces outward
  Outward = 1,

  /// Orientation is not known
  Unknown = 2,
}

/// <summary>
/// Represents the type of a loop in a <see cref="Brep"/>'s face.
/// </summary>
public enum BrepLoopType
{
  /// Loop type is not known
  Unknown,

  /// Loop is the outer loop of a face
  Outer,

  /// Loop is an inner loop of a face
  Inner,

  /// Loop is a closed curve with no area.
  Slit,

  /// Loop represents a curve on a surface
  CurveOnSurface,

  /// Loop is collapsed to a point.
  PointOnSurface,
}

/// <summary>
/// Represents the type of a trim in a <see cref="Brep"/>'s loop.
/// </summary>
public enum BrepTrimType
{
  Unknown,
  Boundary,
  Mated,
  Seam,
  Singular,
  CurveOnSurface,
  PointOnSurface,
  Slit,
}
