using NUnit.Framework;
using Speckle.Objects.Structural.GSA.Geometry;
using Speckle.Sdk.Host;
using Speckle.Sdk.Models;

namespace Objects.Tests.Unit;

public class ObjectBaseValidityTests
{
  [Test]
  public void TestThatTypeWithoutAttributeFails()
  {
    TypeLoader.Reset();
    TypeLoader.Initialize(typeof(Base).Assembly, typeof(GSAAssembly).Assembly);
  }
}
