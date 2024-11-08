namespace Speckle.Sdk.Serialisation;

public readonly record struct SerializationResult(Json Json, Id? Id);

public readonly record struct Json(string Value);

public readonly record struct Id(string Value);
