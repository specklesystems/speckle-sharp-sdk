using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Objects.Primitive;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Proxies;

namespace Speckle.Objects;

#region Generic interfaces.

/// <summary>
/// Represents an object that has a <see cref="IHasBoundingBox.bbox"/>
/// </summary>
public interface IHasBoundingBox : ISpeckleObject
{
  /// <summary>
  /// The bounding box containing the object.
  /// </summary>
  Box? bbox { get; }
}

/// <summary>
/// Represents a <see cref="Base"/> object that has <see cref="IHasArea.area"/>
/// </summary>
public interface IHasArea : ISpeckleObject
{
  /// <summary>
  /// The area of the object
  /// </summary>
  double area { get; }
}

/// <summary>
/// Represents an object that has <see cref="IHasVolume.volume"/>
/// </summary>
public interface IHasVolume : ISpeckleObject
{
  /// <summary>
  /// The volume of the object
  /// </summary>
  double volume { get; }
}

/// <summary>
/// Represents
/// </summary>
public interface ICurve : ISpeckleObject
{
  /// <summary>
  /// The length of the curve.
  /// </summary>
  double length { get; }

  /// <summary>
  /// The numerical domain driving the curve's internal parametrization.
  /// </summary>
  Interval domain { get; }

  string units { get; }
}

/// <summary>
/// Generic Interface for transformable objects.
/// </summary>
/// <typeparam name="T">The type of object to support transformations.</typeparam>
public interface ITransformable<T> : ITransformable
  where T : ITransformable<T>
{
  /// <inheritdoc cref="ITransformable.TransformTo"/>
  bool TransformTo(Transform transform, out T transformed);
}

/// <summary>
/// Interface for transformable objects where the type may not be known on convert (eg ICurve implementations)
/// </summary>
public interface ITransformable : ISpeckleObject
{
  /// <summary>
  /// Returns a copy of the object with it's coordinates transformed by the provided <paramref name="transform"/>
  /// </summary>
  /// <param name="transform">The <see cref="Transform"/> to be applied.</param>
  /// <param name="transformed">The transformed copy of the object.</param>
  /// <returns>True if the transform operation was successful, false otherwise.</returns>
  bool TransformTo(Transform transform, out ITransformable transformed);
}

/// <summary>
/// Specifies displayable <see cref="Base"/> simple geometries to be used as a fallback
/// if a displayable form cannot be converted.
/// </summary>
/// <example>
/// <see cref="Base"/> objects that represent conceptual / abstract / mathematically derived geometry
/// can use <see cref="displayValue"/> to be used in case the object lacks a natively displayable form.
/// (e.g <see cref="Spiral"/>)
/// </example>
/// <typeparam name="T">
/// Type of display value.
/// Expected to be either a <see cref="Base"/> type or a <see cref="List{T}"/> of <see cref="Base"/>s,
/// Should be constrained to types of <see cref="Point"/>, <see cref="Line"/>, <see cref="Mesh"/> or <see cref="Polyline"/>.
/// </typeparam>
public interface IDisplayValue<out T> : ISpeckleObject
{
  /// <summary>
  /// <see cref="displayValue"/> <see cref="Base"/>(s) will be used to display this <see cref="Base"/>
  /// if a native displayable object cannot be converted.
  /// </summary>
  T displayValue { get; }
}

#endregion

#region Data objects

public interface IDataObject : IProperties, IDisplayValue<IReadOnlyList<Base>>, ISpeckleObject
{
  /// <summary>
  /// The name of the object, primarily used to decorate the object for consumption in frontend and other apps
  /// </summary>
  string name { get; }
}

public interface IRevitObject : IDataObject
{
  string type { get; }

  string family { get; }

  string category { get; }

  Base? location { get; }

  IReadOnlyList<IRevitObject> elements { get; }
}

public interface ICivilObject : IDataObject
{
  string type { get; }

  List<ICurve>? baseCurves { get; }

  IReadOnlyList<Base> elements { get; }
}

public interface ITeklaObject : IDataObject
{
  string type { get; }

  IReadOnlyList<ITeklaObject> elements { get; }
}

public interface ICsiObject : IDataObject
{
  string type { get; }

  IReadOnlyList<ICsiObject> elements { get; }
}

public interface IGisObject : IDataObject
{
  string type { get; }
}

public interface IArchicadObject : IDataObject
{
  string type { get; }

  string level { get; }

  IReadOnlyList<IArchicadObject> elements { get; }
}

public interface INavisworksObject : IDataObject { }


#endregion
