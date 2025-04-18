using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Speckle.Sdk.Api;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;
using Speckle.Sdk.Transports;

namespace Speckle.Sdk.Tests.Unit.Api.Operations;

public class Closures
{
  private readonly IOperations _operations;

  public Closures()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(TableLegFixture).Assembly);
    var serviceProvider = TestServiceSetup.GetServiceProvider();
    _operations = serviceProvider.GetRequiredService<IOperations>();
  }

  [Fact(DisplayName = "Checks whether closures are generated correctly by the serialiser.")]
  public async Task CorrectDecompositionTracking()
  {
    var d5 = new Base();
    ((dynamic)d5).name = "depth five"; // end v

    var d4 = new Base();
    ((dynamic)d4).name = "depth four";
    ((dynamic)d4)["@detach"] = d5;

    var d3 = new Base();
    ((dynamic)d3).name = "depth three";
    ((dynamic)d3)["@detach"] = d4;

    var d2 = new Base();
    ((dynamic)d2).name = "depth two";
    ((dynamic)d2)["@detach"] = d3;
    ((dynamic)d2)["@joker"] = new object[] { d5 };

    var d1 = new Base();
    ((dynamic)d1).name = "depth one";
    ((dynamic)d1)["@detach"] = d2;
    ((dynamic)d1)["@joker"] = d5; // consequently, d5 depth in d1 should be 1

    var transport = new MemoryTransport();

    var sendResult = await _operations.Send(d1, transport, false);

    var test = await _operations.Receive(sendResult.rootObjId, localTransport: transport);

    test.id.NotNull();
    d1.GetId(true).Should().BeEquivalentTo((test.id));

    var d1_ = NotNullExtensions.NotNull(JsonConvert.DeserializeObject<dynamic>(transport.Objects[d1.GetId(true)]));
    var d2_ = NotNullExtensions.NotNull(JsonConvert.DeserializeObject<dynamic>(transport.Objects[d2.GetId(true)]));
    var d3_ = NotNullExtensions.NotNull(JsonConvert.DeserializeObject<dynamic>(transport.Objects[d3.GetId(true)]));
    JsonConvert.DeserializeObject<dynamic>(transport.Objects[d4.GetId(true)]);
    JsonConvert.DeserializeObject<dynamic>(transport.Objects[d5.GetId(true)]);

    var depthOf_d5_in_d1 = int.Parse((string)d1_.__closure[d5.GetId(true)]);
    depthOf_d5_in_d1.Should().Be(1);

    var depthOf_d4_in_d1 = int.Parse((string)d1_.__closure[d4.GetId(true)]);
    depthOf_d4_in_d1.Should().Be(3);

    var depthOf_d5_in_d3 = int.Parse((string)d3_.__closure[d5.GetId(true)]);
    depthOf_d5_in_d3.Should().Be(2);

    var depthOf_d4_in_d3 = int.Parse((string)d3_.__closure[d4.GetId(true)]);
    depthOf_d4_in_d3.Should().Be(1);

    var depthOf_d5_in_d2 = int.Parse((string)d2_.__closure[d5.GetId(true)]);
    depthOf_d5_in_d2.Should().Be(1);
  }

  [Fact]
  public void DescendantsCounting()
  {
    Base myBase = new();

    var myList = new List<object>();
    // These should be counted!
    for (int i = 0; i < 100; i++)
    {
      var smolBase = new Base();
      smolBase["test"] = i;
      myList.Add(smolBase);
    }

    // Primitives should not be counted!
    for (int i = 0; i < 10; i++)
    {
      myList.Add(i);
    }

    myList.Add("Hello");
    myList.Add(new { hai = "bai" });

    myBase["@detachTheList"] = myList;

    var dictionary = new Dictionary<string, object>();
    for (int i = 0; i < 10; i++)
    {
      var smolBase = new Base { applicationId = i.ToString() };
      dictionary[$"key {i}"] = smolBase;
    }

    dictionary["string value"] = "bol";
    dictionary["int value"] = 42;
    dictionary["THIS IS RECURSIVE SURPRISE"] = myBase;

    myBase["@detachTheDictionary"] = dictionary;

    myBase.GetTotalChildrenCount().Should().Be(112);

    var tableTest = new DiningTable();
    tableTest.GetTotalChildrenCount().Should().Be(10);

    // Explicitely test for recurisve references!
    var recursiveRef = new Base { applicationId = "random" };
    recursiveRef["@recursive"] = recursiveRef;

    recursiveRef.GetTotalChildrenCount().Should().Be(2);
  }
}
