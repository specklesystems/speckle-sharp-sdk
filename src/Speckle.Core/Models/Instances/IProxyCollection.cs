﻿namespace Speckle.Core.Models.Instances;

/// <summary>
/// Collection to proxy objects that lies in definitions, groups or whatever logic in the host app.
/// </summary>
public interface IProxyCollection
{
  /// <summary>
  /// The original ids of the objects that are part of this definition, as present in the source host app.
  /// On receive, they will be mapped to corresponding newly created definition ids.
  /// </summary>
  public List<string> objects { get; set; }

  /// <summary>
  /// Name of the proxy collection which is unique for rhino, autocad and sketchup
  /// </summary>
  public string name { get; set; }
}
