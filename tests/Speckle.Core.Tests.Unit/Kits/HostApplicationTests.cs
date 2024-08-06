using NUnit.Framework;
using Shouldly;
using Speckle.Core.Kits;

namespace Speckle.Core.Tests.Unit.Kits;

public class HostApplicationTests
{
  private static List<HostAppVersion> _hostAppVersion = Enum.GetValues<HostAppVersion>().ToList();

  [Test]
  [TestCaseSource("_hostAppVersion")]
  public void HostAppVersionParsingTests(HostAppVersion appVersion)
  {
    appVersion.ToString().StartsWith("v").ShouldBeTrue();
    var version = HostApplications.GetVersion(appVersion);
    int.Parse(version).ShouldBePositive();
  }
}
