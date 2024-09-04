using System.Diagnostics;
using NUnit.Framework;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;
using Speckle.Sdk.Tests.Unit.Host;

namespace Speckle.Sdk.Tests.Unit.Models;

[TestFixture]
[TestOf(typeof(Base))]
public class Hashing
{
  [SetUp]
  public void Setup()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(DiningTable).Assembly);
  }

  [Test(Description = "Checks that hashing (as represented by object ids) actually works.")]
  public async Task HashChangeCheck()
  {
    var table = new DiningTable();
    var secondTable = new DiningTable();

    Assert.That(await secondTable.GetIdAsync(), Is.EqualTo(await table.GetIdAsync()));

    ((dynamic)secondTable).testProp = "wonderful";

    Assert.That(await secondTable.GetIdAsync(), Is.Not.EqualTo(await table.GetIdAsync()));
  }

  [Test(
    Description = "Tests the convention that dynamic properties that have key names prepended with '__' are ignored."
  )]
  public async Task IgnoredDynamicPropertiesCheck()
  {
    var table = new DiningTable();
    var originalHash = await table.GetIdAsync();

    ((dynamic)table).__testProp = "wonderful";

    Assert.That(await table.GetIdAsync(), Is.EqualTo(originalHash));
  }

  [Test(Description = "Rather stupid test as results vary wildly even on one machine.")]
  public async Task HashingPerformance()
  {
    var polyline = new Polyline();

    for (int i = 0; i < 1000; i++)
    {
      polyline.Points.Add(new Point { X = i * 2, Y = i % 2 });
    }

    var stopWatch = new Stopwatch();
    stopWatch.Start();

    // Warm-up: first hashing always takes longer due to json serialisation init
    _ = await polyline.GetIdAsync();
    var stopWatchStep = stopWatch.ElapsedMilliseconds;
    _ = await polyline.GetIdAsync();

    var diff1 = stopWatch.ElapsedMilliseconds - stopWatchStep;
    Assert.That(diff1, Is.LessThan(300), $"Hashing shouldn't take that long ({diff1} ms) for the test object used.");
    Console.WriteLine($"Big obj hash duration: {diff1} ms");

    var pt = new Point
    {
      X = 10,
      Y = 12,
      Z = 30
    };
    stopWatchStep = stopWatch.ElapsedMilliseconds;
    _ = await pt.GetIdAsync();

    var diff2 = stopWatch.ElapsedMilliseconds - stopWatchStep;
    Assert.That(diff2, Is.LessThan(10), $"Hashing shouldn't take that long  ({diff2} ms)for the point object used.");
    Console.WriteLine($"Small obj hash duration: {diff2} ms");
  }

  [Test(Description = "The hash of a decomposed object is different that that of a non-decomposed object.")]
  public async Task DecompositionHashes()
  {
    var table = new DiningTable();
    ((dynamic)table)["@decomposeMePlease"] = new Point();

    var hash1 = await table.GetIdAsync();
    var hash2 = await table.GetIdAsync(true);

    Assert.That(hash2, Is.Not.EqualTo(hash1));
  }
}
