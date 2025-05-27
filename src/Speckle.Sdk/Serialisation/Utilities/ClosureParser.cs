using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation.Utilities;

public static class ClosureParser
{
  public static IReadOnlyList<(string, int)> GetClosures(string json, CancellationToken cancellationToken) =>
    GetClosuresPrivate(json, cancellationToken);

  private static List<(string, int)> GetClosuresPrivate(string json, CancellationToken cancellationToken)
  {
    try
    {
      using JsonTextReader reader = SpeckleObjectSerializerPool.Instance.GetJsonTextReader(new StringReader(json));
      reader.Read();
      while (reader.TokenType != JsonToken.EndObject)
      {
        switch (reader.TokenType)
        {
          case JsonToken.StartObject:
          {
            var closureList = ReadObject(reader, cancellationToken);
            return closureList;
          }
          default:
            reader.Read();
            reader.Skip();
            break;
        }
      }
    }
    catch (Exception ex) when (!ex.IsFatal()) { }
    return [];
  }

  public static IReadOnlyList<(string, int)> GetClosuresSorted(string json, CancellationToken cancellationToken)
  {
    var closures = GetClosuresPrivate(json, cancellationToken);
    closures.Sort((a, b) => b.Item2.CompareTo(a.Item2));
    return closures;
  }

  public static IEnumerable<string> GetChildrenIds(string json, CancellationToken cancellationToken) =>
    GetClosures(json, cancellationToken).Select(x => x.Item1);

  private static List<(string, int)> ReadObject(JsonTextReader reader, CancellationToken cancellationToken)
  {
    reader.Read();
    while (reader.TokenType != JsonToken.EndObject)
    {
      cancellationToken.ThrowIfCancellationRequested();
      switch (reader.TokenType)
      {
        case JsonToken.PropertyName:
          {
            if (reader.Value as string == "__closure")
            {
              reader.Read(); //goes to prop vale
              var closureList = ReadClosureEnumerable(reader, cancellationToken);
              return closureList;
            }
            reader.Read(); //goes to prop vale
            reader.Skip();
            reader.Read(); //goes to next
          }
          break;
        default:
          reader.Read();
          reader.Skip();
          reader.Read();
          break;
      }
    }
    return [];
  }

  public static IReadOnlyList<(string, int)> GetClosures(JsonReader reader, CancellationToken cancellationToken)
  {
    if (reader.TokenType != JsonToken.StartObject)
    {
      return Array.Empty<(string, int)>();
    }

    var closureList = ReadClosureEnumerable(reader, cancellationToken);
    closureList.Sort((a, b) => b.Item2.CompareTo(a.Item2));
    return closureList;
  }

  private static List<(string, int)> ReadClosureEnumerable(JsonReader reader, CancellationToken cancellationToken)
  {
    List<(string, int)> closureList = new();
    reader.Read(); //startobject
    while (reader.TokenType != JsonToken.EndObject)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var childId = (reader.Value as string).NotNull(); // propertyName
      int childMinDepth = (reader.ReadAsInt32()).NotNull(); //propertyValue
      reader.Read();
      closureList.Add((childId, childMinDepth));
    }
    return closureList;
  }
}
