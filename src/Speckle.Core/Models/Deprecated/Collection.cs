using System;

namespace Speckle.Core.Models.Deprecated;

[Obsolete("Replaced by " + nameof(Speckle.Core.Models.Collections.Collection))]
public class Collection : Speckle.Core.Models.Collections.Collection
{
  //Deserializer target DUI3 Collection objects in the `Speckle.Core.Models` namespace

  //Speckle.Core.Models.Deprecated.Collection:Speckle.Core.Models.Collections.Collection

  //POC: This could be handled with object version
}
