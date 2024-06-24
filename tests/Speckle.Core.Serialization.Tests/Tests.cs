using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using Speckle.Core.Common;
using Speckle.Core.Kits;
using Speckle.Core.Serialisation;
using Speckle.Core.Transports;
using Speckle.Objects.BuiltElements.Revit;

namespace Speckle.Core.Serialization.Tests;

public class TestKit : ISpeckleKit
{
  public IEnumerable<Type> Types => typeof(RevitWall).Assembly.ExportedTypes;
  public IEnumerable<string> Converters { get; }
  public string Description { get; }
  public string Name { get; }
  public string Author { get; }
  public string WebsiteOrEmail { get; }
  public ISpeckleConverter LoadConverter(string app) => throw new NotImplementedException();
}
public class TestTransport : ITransport
{
  public string TransportName
  {
    get => "Test";
    set
    {
    }
  }

  public Dictionary<string, object> TransportContext { get; }
  public TimeSpan Elapsed { get; }
  public int SavedObjectCount { get; }
  public CancellationToken CancellationToken { get; set; }
  public Action<string, int>? OnProgressAction { get; set; }
  public Action<string, Exception>? OnErrorAction { get; set; }
  public void BeginWrite() => throw new NotImplementedException();

  public void EndWrite() => throw new NotImplementedException();

  public void SaveObject(string id, string serializedObject) => throw new NotImplementedException();

  public void SaveObject(string id, ITransport sourceTransport) => throw new NotImplementedException();

  public Task WriteComplete() => throw new NotImplementedException();

  public string? GetObject(string id) => "{}";

  public Task<string> CopyObjectAndChildren(string id, ITransport targetTransport, Action<int>? onTotalChildrenCountKnown = null) => throw new NotImplementedException();

  public Task<Dictionary<string, bool>> HasObjects(IReadOnlyList<string> objectIds) => throw new NotImplementedException();
}

[TestFixture]
[Description("For certain types, changing property from one type to another should be implicitly backwards compatible")]
public class SerializationTests
{
  [Test]
  [TestCase("RevitObject.json", "Objects.BuiltElements.Revit.Parameter")]
  public async Task Basic_TypeValidation(string fileName, string typeName)
  {
    var asm = Assembly.GetExecutingAssembly();
    var fullName = asm.GetManifestResourceNames().Single(x => x.EndsWith(fileName));
    await using var stream = asm.GetManifestResourceStream(fullName).NotNull();
    using var reader = new StreamReader(stream);
    var json = await reader.ReadToEndAsync();
    var deserializer = new BaseObjectDeserializerV2
    {
      ReadTransport = new TestTransport(),
      CancellationToken = default
    };
    var baseType = deserializer.Deserialize(json);
    typeName.Should().Be(baseType.speckle_type);
  }
}
