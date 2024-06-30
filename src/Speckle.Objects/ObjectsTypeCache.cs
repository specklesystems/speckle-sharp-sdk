using System.Diagnostics;
using System.Reflection;
using Speckle.Core.Models;
using Speckle.Core.Serialisation.TypeCache;

namespace Speckle.Objects;

public sealed class ObjectsTypeCache() : AbstractTypeCache(new[] { typeof(ObjectsTypeCache).Assembly }, typeof(Base));

