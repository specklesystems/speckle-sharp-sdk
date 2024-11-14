using Shouldly;
using Speckle.Sdk.Common;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Speckle.Sdk.Tests.Unit.Models;

public class BaseTests
{
  [Before(Class)]
  public static void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(BaseTests).Assembly);
  }

  [Test]
  public void CanGetSetDynamicItemProp()
  {
    var @base = new Base();
    @base["Item"] = "Item";

 @base["Item"].ShouldBe("Item");
  }

  [Test]
  public void CanGetSetTypedItemProp()
  {
    var @base = new ObjectWithItemProp { Item = "baz" };

    @base["Item"].ShouldBe("baz");
    @base.Item.ShouldBe("baz");
  }

  [Test]
  public void CanValidatePropNames()
  {
    dynamic @base = new Base();

    // Word chars are OK
    @base["something"] = "B";

    // Only single leading @ allowed
    @base["@something"] = "A";
    Assert.Throws<InvalidPropNameException>(() =>
    {
      @base["@@@something"] = "Testing";
    });

    // Invalid chars:  ./
    Assert.Throws<InvalidPropNameException>(() =>
    {
      @base["some.thing"] = "Testing";
    });
    Assert.Throws<InvalidPropNameException>(() =>
    {
      @base["some/thing"] = "Testing";
    });

    // Trying to change a class member value will throw exceptions.
    //Assert.Throws<Exception>(() => { @base["speckle_type"] = "Testing"; });
    //Assert.Throws<Exception>(() => { @base["id"] = "Testing"; });
  }

  [Test]
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
    num.ShouldBe(MAX_NUM / 1000 * 2 + 1);
  }

  [Test]
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
    num.ShouldBe(actualNum);
  }
/*
  [Test]
  public void CanGetMemberNames()
  {
    var @base = new SampleObject();
    var dynamicProp = "dynamicProp";
    @base[dynamicProp] = 123;
    var names = @base.GetMembers().Keys;
    names.ShouldNotContain(nameof(@base.IgnoredSchemaProp));
    Assert.That(names, Has.No.Member(nameof(@base.ObsoleteSchemaProp)));
    Assert.That(names, Has.Member(dynamicProp));
    Assert.That(names, Has.Member(nameof(@base.attachedProp)));
  }

  [Test(Description = "Checks that only instance properties are returned, excluding obsolete and ignored.")]
  public void CanGetMembers_OnlyInstance()
  {
    var @base = new SampleObject();
    @base["dynamicProp"] = 123;

    var names = @base.GetMembers(DynamicBaseMemberType.Instance).Keys;
    Assert.That(names, Has.Member(nameof(@base.attachedProp)));
  }

  [Test(Description = "Checks that only dynamic properties are returned")]
  public void CanGetMembers_OnlyDynamic()
  {
    var @base = new SampleObject();
    var dynamicProp = "dynamicProp";
    @base[dynamicProp] = 123;

    var names = @base.GetMembers(DynamicBaseMemberType.Dynamic).Keys;
    Assert.That(names, Has.Member(dynamicProp));
    Assert.That(names, Has.Count.EqualTo(1));
  }

  [Test(Description = "Checks that all typed properties (including ignored ones) are returned")]
  public void CanGetMembers_OnlyInstance_IncludeIgnored()
  {
    var @base = new SampleObject();
    @base["dynamicProp"] = 123;

    var names = @base.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.SchemaIgnored).Keys;
    Assert.That(names, Has.Member(nameof(@base.IgnoredSchemaProp)));
    Assert.That(names, Has.Member(nameof(@base.attachedProp)));
  }

  [Test(Description = "Checks that all typed properties (including obsolete ones) are returned")]
  public void CanGetMembers_OnlyInstance_IncludeObsolete()
  {
    var @base = new SampleObject();
    @base["dynamicProp"] = 123;

    var names = @base.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Obsolete).Keys;
    Assert.That(names, Has.Member(nameof(@base.ObsoleteSchemaProp)));
    Assert.That(names, Has.Member(nameof(@base.attachedProp)));
  }

  [Test]
  public void CanGetDynamicMembers()
  {
    var @base = new SampleObject();
    var dynamicProp = "dynamicProp";
    @base[dynamicProp] = null;

    var names = @base.GetDynamicMemberNames();
    Assert.That(names, Has.Member(dynamicProp));
    Assert.That(@base[dynamicProp], Is.Null);
  }

  [Test]
  public void CanSetDynamicMembers()
  {
    var @base = new SampleObject();
    var key = "dynamicProp";
    var value = "something";
    // Can create a new dynamic member
    @base[key] = value;
    Assert.That(value, Is.EqualTo((string)@base[key].NotNull()));

    // Can overwrite existing
    value = "some other value";
    @base[key] = value;
    Assert.That(value, Is.EqualTo((string)@base[key].NotNull()));

    // Accepts null values
    @base[key] = null;
    Assert.That(@base[key], Is.Null);
  }

  [Test]
  public void CanShallowCopy()
  {
    var sample = new SampleObject();
    var copy = sample.ShallowCopy();

    var selectedMembers =
      DynamicBaseMemberType.Dynamic | DynamicBaseMemberType.Instance | DynamicBaseMemberType.SchemaIgnored;
    var sampleMembers = sample.GetMembers(selectedMembers);
    var copyMembers = copy.GetMembers(selectedMembers);

    Assert.That(copyMembers.Keys, Is.EquivalentTo(sampleMembers.Keys));
    Assert.That(copyMembers.Values, Is.EquivalentTo(sampleMembers.Values));
  }*/

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
