using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation.SerializationUtilities;

public static class ClosureParser
{
  public static IReadOnlyList<(string, int)> GetClosures(string rootObjectJson)
  {
    using JsonTextReader reader = new(new StringReader(rootObjectJson));
    return GetClosures(reader);
  }

  public static IReadOnlyList<(string, int)> GetClosures(JsonReader reader)
  {
    var closureList = ReadClosureList(reader);
    closureList =   closureList.OrderBy<(string, int), int>((b) => b.Item2);
      return closureList.ToList();
  }


  private static IEnumerable<(string, int)> ReadClosureList(JsonReader reader)
  {
    reader.Read(); //startobject
    while (reader.TokenType != JsonToken.EndObject)
    {
      var childId = (reader.Value as string).NotNull(); // propertyName
      int childMinDepth = reader.ReadAsInt32().NotNull(); //propertyValue
      reader.Read();
      yield return (childId, childMinDepth);
    }
  }
}
