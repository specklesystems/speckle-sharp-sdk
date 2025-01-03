using System.Diagnostics;
using Xunit;
using Shouldly;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;

namespace Speckle.Sdk.Tests.Unit.Models;

 // Removed [TestFixture] and [TestOf] annotations as they are NUnit specific
public class Hashing
{

  public Hashing()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DiningTable).Assembly);
  }

 [Fact(DisplayName = "Checks that hashing (as represented by object IDs) actually works.")]

  public void HashChangeCheck_Test()
  {
    var table = new DiningTable();
    var secondTable = new DiningTable();

 secondTable.GetId().ShouldBe(table.GetId(), "Object IDs of identical objects should match.");

    ((dynamic)secondTable).testProp = "wonderful";

 secondTable.GetId().ShouldNotBe(table.GetId(), "Changing a property should alter the object ID.");
  }

 [Fact(DisplayName = "Verifies that dynamic properties with '__' prefix are ignored during hashing.")]

  public void IgnoredDynamicPropertiesCheck_Test()
  {
    var table = new DiningTable();
    var originalHash = table.GetId();

    ((dynamic)table).__testProp = "wonderful";

 table.GetId().ShouldBe(originalHash, "Hashing of table should not change when '__' prefixed properties are added.");
  }

 [Fact(DisplayName = "Performance test: Hash computation time for large and small objects.")]

  public void HashingPerformance_Test()
  {
    var polyline = new Polyline();

    for (int i = 0; i < 1000; i++)
    {
      polyline.Points.Add(new Point { X = i * 2, Y = i % 2 });
    }

    var stopWatch = new Stopwatch();
    stopWatch.Start();

    // Warm-up: first hashing always takes longer due to json serialisation init
    _ = polyline.GetId();
    var stopWatchStep = stopWatch.ElapsedMilliseconds;
    _ = polyline.GetId();

    var diff1 = stopWatch.ElapsedMilliseconds - stopWatchStep;
 diff1.ShouldBeLessThan(300, 
   $"Hashing shouldn't take that long ({diff1} ms) for the test object used.");
    Console.WriteLine($"Big obj hash duration: {diff1} ms");

    var pt = new Point
    {
      X = 10,
      Y = 12,
      Z = 30,
    };
    stopWatchStep = stopWatch.ElapsedMilliseconds;
    _ = pt.GetId();

    var diff2 = stopWatch.ElapsedMilliseconds - stopWatchStep;
 diff2.ShouldBeLessThan(10, 
   $"Hashing shouldn't take that long ({diff2} ms) for the point object used.");
    Console.WriteLine($"Small obj hash duration: {diff2} ms");
  }

 [Fact(DisplayName = "Verifies that decomposed and non-decomposed objects have different hashes.")]

  public void DecompositionHashes_Test()
  {
    var table = new DiningTable();
    ((dynamic)table)["@decomposeMePlease"] = new Point();

    var hash1 = table.GetId();
    var hash2 = table.GetId(true);

 hash2.ShouldNotBe(hash1, "Hash values should differ for decomposed and non-decomposed objects.");
  }
}
