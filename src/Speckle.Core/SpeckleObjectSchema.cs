namespace Speckle.Core;

// POC: core needs to know the version of the schema we have baked into the Objects DLL
// you'll notice this version isn't IN the objects DLL, that's because Objects is dependent upon core
// but core needs to know the current schema version we have loaded. Core should depend on objects and NOT
// the other way around. This needs some consideration but for POC of object versioning, this is where it lives.
public class SpeckleObjectSchema
{
  // POC: I'm not sure about using the Version object ATM, strings may be just as a straight-forward... 
  public static readonly Version Version = new Version(3, 0, 0);
}
