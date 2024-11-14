using Shouldly;
using Speckle.Sdk.Host;

namespace Speckle.Sdk.Tests.Unit.Host;

public class HostApplicationTests
{
  public static IEnumerable<HostAppVersion> HostAppVersionData() =>  Enum.GetValues<HostAppVersion>();

  [Test]
  [MethodDataSource(nameof(HostAppVersionData))]
  public void HostAppVersionParsingTests(HostAppVersion appVersion)
  {
    appVersion.ToString().StartsWith('v').ShouldBeTrue();
    var version = HostApplications.GetVersion(appVersion);
    int.Parse(version).ShouldBePositive();
  }
}
