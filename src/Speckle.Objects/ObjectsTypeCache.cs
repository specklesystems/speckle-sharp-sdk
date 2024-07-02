using System.Diagnostics;
using System.Reflection;
using Objects;
using Speckle.Core.Models;
using Speckle.Core.Serialisation.TypeCache;

namespace Speckle.Objects;

public sealed class ObjectsTypeCache : AbstractTypeCache
{
  public ObjectsTypeCache(Version? version = null)
    : base(new[] { typeof(ObjectsTypeCache).Assembly },
      typeof(Base),
      version ?? SpeckleSchemaInfo.Version,
      "Objects")
  {
  }
}
