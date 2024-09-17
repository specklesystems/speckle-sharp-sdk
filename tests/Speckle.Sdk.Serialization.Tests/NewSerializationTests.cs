﻿using System.IO.Compression;
using System.Reflection;
using NUnit.Framework;
using Shouldly;
using Speckle.Newtonsoft.Json;
using Speckle.Newtonsoft.Json.Linq;
using Speckle.Objects.BuiltElements;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Serialisation;
using Speckle.Sdk.Serialisation.Receive;
using Speckle.Sdk.Serialisation.Send;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Serialization.Tests;

[TestFixture]
[Description("For certain types, changing property from one type to another should be implicitly backwards compatible")]
public class NewSerializationTests
{
  private readonly Assembly _assembly = Assembly.GetExecutingAssembly();

  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(Wall).Assembly, _assembly);
  }

  private async Task<string> ReadJson(string fullName)
  {
    await using var stream = _assembly.GetManifestResourceStream(fullName).NotNull();
    if (fullName.EndsWith(".json.gz"))
    {
      await using var gZipStream = new GZipStream(stream, CompressionMode.Decompress);
      using var reader = new StreamReader(gZipStream);
      return await reader.ReadToEndAsync();
    }
    else
    {
      using var reader = new StreamReader(stream);
      return await reader.ReadToEndAsync();
    }
  }

  private async Task<Dictionary<string, string>> ReadAsObjects(string fullName)
  {
    var jsonObjects = new Dictionary<string, string>();
    var json = await ReadJson(fullName);
    var array = JArray.Parse(json);
    foreach (var obj in array)
    {
      if (obj is JObject jobj)
      {
        jsonObjects.Add(jobj["id"].NotNull().Value<string>().NotNull(), jobj.ToString());
      }
    }
    return jsonObjects;
  }

  [Test]
  [TestCase("RevitObject.json")]
  public async Task Deserialize_Serialize_Test(string fileName)
  {
    var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
    var objects = await ReadAsObjects(fullName);
    var bases = new Dictionary<string, (string, Base)>();
    foreach (var (id, objJson) in objects)
    {
      var jObject = JObject.Parse(objJson);
      var oldSpeckleType = jObject["speckle_type"].NotNull().Value<string>().NotNull();
      var starts = oldSpeckleType.StartsWith("Speckle.Core.") || oldSpeckleType.StartsWith("Objects.");
      starts.ShouldBeTrue($"{oldSpeckleType} isn't expected");

      using var stage = new ReceiveProcess(new MemorySource(objects));
      var baseType = await stage.GetObject(id, _ => { }, default).ConfigureAwait(false);
      baseType.id.ShouldBe(id);
      bases.Add(baseType.id, (objJson, baseType));

      starts = baseType.speckle_type.StartsWith("Speckle.Core.") || baseType.speckle_type.StartsWith("Objects.");
      starts.ShouldBeTrue($"{baseType.speckle_type} isn't expected");

      var type = TypeLoader.GetAtomicType(baseType.speckle_type);
      type.ShouldNotBeNull();
      var name = TypeLoader.GetTypeString(type) ?? throw new ArgumentNullException();
      starts = name.StartsWith("Speckle.Core") || name.StartsWith("Objects");
      starts.ShouldBeTrue($"{name} isn't expected");
    }

    var target = new MemoryTarget();
    foreach (var (id, (objJson, baseObj)) in bases)
    {
      using var stage = new SendProcess(target);
      var (objId, references) = await stage.SaveObject(baseObj, _ => { }, default).ConfigureAwait(false);
      var orig = JObject.Parse(target.Sent[objId]);
      var targ = JObject.Parse(objJson);
      JToken.DeepEquals(orig, targ);
      //      objId.ShouldBe(id);
    }
  }

  [Test]
  [TestCase("7238c0e0bbd1bafbe1a287aa7fc88619.json.gz")]
  public async Task Channels_Deserialize(string fileName)
  {
    var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
    var closures = await ReadAsObjects(fullName);

    using var process = new ReceiveProcess(new MemorySource(closures), new ReceiveProcessSettings(1, 1));
    var root = await process.GetObject("7238c0e0bbd1bafbe1a287aa7fc88619", _ => { }, default);
    root.id.ShouldBe("7238c0e0bbd1bafbe1a287aa7fc88619");
  }

  [Test]
  [TestCase("RevitObjectsTop.json")]
  public async Task Deserialize_Both_Serialize_Both_Compare(string fileName)
  {
    var fullName = _assembly.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
    var json = await ReadJson(fullName);
    
    var oldDictionary = new Dictionary<string, string>();
    var oldDeserializer = new SpeckleObjectDeserializer
    {
      ReadTransport = new TestTransport(oldDictionary),
      CancellationToken = default
    };
    var oldBase = await oldDeserializer.DeserializeJsonAsync(json);
    oldBase.id.ShouldBe("7238c0e0bbd1bafbe1a287aa7fc88619");
    var oldCollection = oldBase as Collection;
    oldCollection.ShouldNotBeNull();
    oldCollection.elements.Count.ShouldBe(78);

    var newDictionary = new Dictionary<string, Base>();
    SpeckleObjectDeserializer2 newDeserializer = new(newDictionary, SpeckleObjectSerializer2Pool.Instance, new(false));
    var newBase = newDeserializer.Deserialize(json);
    newBase.id.ShouldBe("7238c0e0bbd1bafbe1a287aa7fc88619");
    var newCollection = newBase as Collection;
    newCollection.ShouldNotBeNull();
    newCollection.elements.Count.ShouldBe(78);

    SpeckleObjectSerializer2 newSerializer = new(SpeckleObjectSerializer2Pool.Instance);
    var newJson = newSerializer.Serialize(newBase);

    SpeckleObjectSerializer oldSerializer = new();
    var oldJson = oldSerializer.Serialize(oldBase);
    newJson.Length.ShouldBe(oldJson.Length);

    JToken.DeepEquals(JObject.Parse(newJson), JObject.Parse(oldJson)).ShouldBeTrue();
  }
  
}
