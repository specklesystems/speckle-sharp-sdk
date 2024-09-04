﻿using Speckle.Newtonsoft.Json;
using Speckle.Sdk.Common;

namespace Speckle.Sdk.Serialisation.Utilities;

public static class ClosureParser
{
  public static async Task<IReadOnlyList<(string, int)>> GetClosuresAsync(string rootObjectJson)
  {
    try
    {
      using JsonTextReader reader = new(new StringReader(rootObjectJson));
      reader.Read();
      while (reader.TokenType != JsonToken.EndObject)
      {
        switch (reader.TokenType)
        {
          case JsonToken.StartObject:
          {
            var closureList = await ReadObjectAsync(reader).ConfigureAwait(false);
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

  public static async Task<IEnumerable<string>> GetChildrenIdsAsync(string rootObjectJson) =>
    (await GetClosuresAsync(rootObjectJson).ConfigureAwait(false)).Select(x => x.Item1);

  private static async Task<IReadOnlyList<(string, int)>> ReadObjectAsync(JsonTextReader reader)
  {
    await reader.ReadAsync().ConfigureAwait(false);
    while (reader.TokenType != JsonToken.EndObject)
    {
      switch (reader.TokenType)
      {
        case JsonToken.PropertyName:
          {
            if (reader.Value as string == "__closure")
            {
              await reader.ReadAsync().ConfigureAwait(false); //goes to prop vale
              var closureList = await ReadClosureEnumerableAsync(reader).ConfigureAwait(false);
              return closureList;
            }
            await reader.ReadAsync().ConfigureAwait(false); //goes to prop vale
            await reader.SkipAsync().ConfigureAwait(false);
            await reader.ReadAsync().ConfigureAwait(false); //goes to next
          }
          break;
        default:
          await reader.ReadAsync().ConfigureAwait(false);
          await reader.SkipAsync().ConfigureAwait(false);
          await reader.ReadAsync().ConfigureAwait(false);
          break;
      }
    }
    return [];
  }

  public static async Task<IReadOnlyList<(string, int)>> GetClosuresAsync(JsonReader reader)
  {
    if (reader.TokenType != JsonToken.StartObject)
    {
      return Array.Empty<(string, int)>();
    }

    var closureList = await ReadClosureEnumerableAsync(reader).ConfigureAwait(false);
    closureList.Sort((a, b) => b.Item2.CompareTo(a.Item2));
    return closureList;
  }

  private static async Task<List<(string, int)>> ReadClosureEnumerableAsync(JsonReader reader)
  {
    List<(string, int)> closureList = new();
    await reader.ReadAsync().ConfigureAwait(false); //startobject
    while (reader.TokenType != JsonToken.EndObject)
    {
      var childId = (reader.Value as string).NotNull(); // propertyName
      int childMinDepth = (await reader.ReadAsInt32Async().ConfigureAwait(false)).NotNull(); //propertyValue
      await reader.ReadAsync().ConfigureAwait(false);
      closureList.Add((childId, childMinDepth));
    }
    return closureList;
  }
}
