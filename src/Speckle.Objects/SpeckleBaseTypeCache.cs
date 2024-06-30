using System.Reflection;
using Speckle.Core;
using Speckle.Core.Models;
using Speckle.Core.Serialisation;

namespace Speckle.Objects;

public class SpeckleBaseTypeCache : AbstractTypeCache
{
  static SpeckleBaseTypeCache()
  {
    SpeckleObjectSchema.TypeCache = new SpeckleBaseTypeCache();
  }

  public SpeckleBaseTypeCache()
    : base(Assembly.GetExecutingAssembly(), typeof(Base)) { }
}
