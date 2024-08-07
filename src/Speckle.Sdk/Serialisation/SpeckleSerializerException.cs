﻿using Speckle.Sdk.Logging;

namespace Speckle.Sdk.Serialisation;

public class SpeckleSerializeException : SpeckleException
{
  public SpeckleSerializeException() { }

  public SpeckleSerializeException(string message, Exception? inner = null)
    : base(message, inner) { }

  public SpeckleSerializeException(string message)
    : base(message) { }
}

public class SpeckleDeserializeException : SpeckleException
{
  public SpeckleDeserializeException() { }

  public SpeckleDeserializeException(string message, Exception? inner = null)
    : base(message, inner) { }

  public SpeckleDeserializeException(string message)
    : base(message) { }
}
