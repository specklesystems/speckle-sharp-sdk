using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.Geometry;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Serialization.Tests.Framework;

public static class TestFileManager
{
  private static readonly Assembly s_assembly = Assembly.GetExecutingAssembly(); //test
  private static readonly Assembly s_speckleAssembly = typeof(Base).Assembly; //speckle.sdk
  private static readonly Assembly s_speckleObjectsAssembly = typeof(Polyline).Assembly; //speckle.sdk
  private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> s_objects = new();

  public static IReadOnlyDictionary<string, string> GetFileAsClosures(string fileName)
  {
    lock (s_objects)
    {
      if (!s_objects.TryGetValue(fileName, out var closure))
      {
        TypeLoader.Reset();
        TypeLoader.Initialize(s_assembly, s_speckleAssembly, s_speckleObjectsAssembly);
        var fullName = s_assembly.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
        var json = ReadJson(fullName);
        closure = ReadAsObjects(json);
        s_objects.Add(fileName, closure);
      }
      return closure;
    }
  }

  private static string ReadJson(string fullName)
  {
    using var stream = s_assembly.GetManifestResourceStream(fullName).NotNull();
    if (fullName.EndsWith(".gz"))
    {
      using var z = new GZipStream(stream, CompressionMode.Decompress);
      using var reader2 = new StreamReader(z);
      return reader2.ReadToEnd();
    }
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }

  private static ConcurrentDictionary<string, string> ReadAsObjects(string json)
  {
    var jsonObjects = new ConcurrentDictionary<string, string>();
    var array = JArray.Parse(json);
    foreach (var obj in array)
    {
      if (obj is JObject jobj)
      {
        jsonObjects.TryAdd(jobj["id"].NotNull().Value<string>().NotNull(), jobj.ToString());
      }
    }
    return jsonObjects;
  }
}
