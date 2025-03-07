using FluentAssertions;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;


namespace Speckle.Sdk.Tests.Unit.Models;

public class BaseTests
{
  public BaseTests()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(BaseTests).Assembly);
  }

  [Fact]
  public void CanGetSetDynamicItemProp()
  {
    var @base = new Base();
    @base["Item"] = "Item";

    @base["Item"].Should().Be("Item");
  }

  [Fact]
  public void CanGetSetTypedItemProp()
  {
    var @base = new ObjectWithItemProp { Item = "baz" };

    @base["Item"].Should().Be("baz");
    @base.Item.Should().Be("baz");
  }

  [Fact(DisplayName = "Checks if validation is performed in property names")]
  public void CanValidatePropNames()
  {
    dynamic @base = new Base();

    // Word chars are OK
    @base["something"] = "B";

    // Only single leading @ allowed
    @base["@something"] = "A";
    FluentActions
      .Invoking(() =>
      {
        @base["@@@something"] = "Testing";
      })
      .Should()
      .Throw<InvalidPropNameException>();

    // Invalid chars:  ./
    FluentActions
      .Invoking(() =>
      {
        @base["some.thing"] = "Testing";
      })
      .Should()
      .Throw<InvalidPropNameException>();
    FluentActions
      .Invoking(() =>
      {
        @base["some/thing"] = "Testing";
      })
      .Should()
      .Throw<InvalidPropNameException>();
    // Trying to change a class member value will throw exceptions.
    //Assert.Throws<Exception>(() => { @base["speckle_type"] = "Testing"; });
    //Assert.Throws<Exception>(() => { @base["id"] = "Testing"; });
  }

  [Fact]
  public void CountDynamicChunkables()
  {
    const int MAX_NUM = 3000;
    var @base = new Base();
    var customChunk = new List<double>();
    var customChunkArr = new double[MAX_NUM];

    for (int i = 0; i < MAX_NUM; i++)
    {
      customChunk.Add(i / 2);
      customChunkArr[i] = i;
    }

    @base["@(1000)cc1"] = customChunk;
    @base["@(1000)cc2"] = customChunkArr;

    var num = @base.GetTotalChildrenCount();
    num.Should().Be(MAX_NUM / 1000 * 2 + 1);
  }

  [Fact]
  public void CountTypedChunkables()
  {
    const int MAX_NUM = 3000;
    var @base = new SampleObject();
    var customChunk = new List<double>();
    var customChunkArr = new double[MAX_NUM];

    for (int i = 0; i < MAX_NUM; i++)
    {
      customChunk.Add(i / 2);
      customChunkArr[i] = i;
    }

    @base.list = customChunk;
    @base.arr = customChunkArr;

    var num = @base.GetTotalChildrenCount();
    var actualNum = 1 + MAX_NUM / 300 + MAX_NUM / 1000;
    num.Should().Be(actualNum);
  }

  [Fact(DisplayName = "Checks that no ignored or obsolete properties are returned")]
  public void CanGetMemberNames()
  {
    var @base = new SampleObject();
    var dynamicProp = "dynamicProp";
    @base[dynamicProp] = 123;
    var names = @base.GetMembers().Keys;
    names.Should().NotContain(nameof(@base.IgnoredSchemaProp));
    names.Should().NotContain(nameof(@base.ObsoleteSchemaProp));
    names.Should().Contain(dynamicProp);
    names.Should().Contain(nameof(@base.attachedProp));
  }

  [Fact(DisplayName = "Checks that only instance properties are returned, excluding obsolete and ignored.")]
  public void CanGetMembers_OnlyInstance()
  {
    var @base = new SampleObject();
    @base["dynamicProp"] = 123;

    var names = @base.GetMembers(DynamicBaseMemberType.Instance).Keys;
    names.Should().Contain(nameof(@base.attachedProp));
  }

  [Fact(DisplayName = "Checks that only dynamic properties are returned")]
  public void CanGetMembers_OnlyDynamic()
  {
    var @base = new SampleObject();
    var dynamicProp = "dynamicProp";
    @base[dynamicProp] = 123;

    var names = @base.GetMembers(DynamicBaseMemberType.Dynamic).Keys;
    names.Should().Contain(dynamicProp);
    names.Count.Should().Be(1);
  }

  [Fact(DisplayName = "Checks that all typed properties (including ignored ones) are returned")]
  public void CanGetMembers_OnlyInstance_IncludeIgnored()
  {
    var @base = new SampleObject();
    @base["dynamicProp"] = 123;

    var names = @base.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.SchemaIgnored).Keys;
    names.Should().Contain(nameof(@base.IgnoredSchemaProp));
    names.Should().Contain(nameof(@base.attachedProp));
  }

  [Fact(DisplayName = "Checks that all typed properties (including obsolete ones) are returned")]
  public void CanGetMembers_OnlyInstance_IncludeObsolete()
  {
    var @base = new SampleObject();
    @base["dynamicProp"] = 123;

    var names = @base.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Obsolete).Keys;
    names.Should().Contain(nameof(@base.ObsoleteSchemaProp));
    names.Should().Contain(nameof(@base.attachedProp));
  }

  [Fact]
  public void CanGetDynamicMembers()
  {
    var @base = new SampleObject();
    var dynamicProp = "dynamicProp";
    @base[dynamicProp] = null;

    var names = @base.GetDynamicMemberNames();
    names.Should().Contain(dynamicProp);
    @base[dynamicProp].Should().BeNull();
  }

  [Fact]
  public void CanSetDynamicMembers()
  {
    var @base = new SampleObject();
    var key = "dynamicProp";
    var value = "something";
    // Can create a new dynamic member
    @base[key] = value;
    value.Should().Be((string)@base[key].NotNull());

    // Can overwrite existing
    value = "some other value";
    @base[key] = value;
    value.Should().Be((string)@base[key].NotNull());

    // Accepts null values
    @base[key] = null;
    @base[key].Should().BeNull();
  }

  [Fact]
  public void CanShallowCopy()
  {
    var sample = new SampleObject();
    var copy = sample.ShallowCopy();

    var selectedMembers =
      DynamicBaseMemberType.Dynamic | DynamicBaseMemberType.Instance | DynamicBaseMemberType.SchemaIgnored;
    var sampleMembers = sample.GetMembers(selectedMembers);
    var copyMembers = copy.GetMembers(selectedMembers);

    copyMembers.Keys.Should().BeEquivalentTo(sampleMembers.Keys);
    copyMembers.Values.Should().BeEquivalentTo(sampleMembers.Values);
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Models.BaseTests+SampleObject")]
  public class SampleObject : Base
  {
    [Chunkable, DetachProperty]
    public List<double> list { get; set; } = new();

    [Chunkable(300), DetachProperty]
    public double[] arr { get; set; }

    [DetachProperty]
    public SampleProp detachedProp { get; set; }

    public SampleProp attachedProp { get; set; }

    public string crazyProp { get; set; }

    [SchemaIgnore]
    public string IgnoredSchemaProp { get; set; }

    [Obsolete("Use attached prop")]
    public string ObsoleteSchemaProp { get; set; }
  }

  public class SampleProp
  {
    public string name { get; set; }
  }

  [SpeckleType("Speckle.Core.Tests.Unit.Models.BaseTests+ObjectWithItemProp")]
  public class ObjectWithItemProp : Base
  {
    public string Item { get; set; } = "Item";
  }
}
