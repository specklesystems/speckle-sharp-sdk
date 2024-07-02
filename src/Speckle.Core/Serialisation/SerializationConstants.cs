using Speckle.Core.Models;

namespace Speckle.Core.Serialisation;

public class SerializationConstants
{
  public const string TYPE_DISCRIMINATOR = nameof(Base.speckle_type);
  public const string CLOSURE_PROPERTY_NAME = "__closure";
  public const string SCHEMA_VERSION = "__schema_version";
}
