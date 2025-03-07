using Speckle.InterfaceGenerator;

namespace Speckle.Sdk;

[GenerateAutoInterface]
public class SpeckleApplication : ISpeckleApplication
{
  public string HostApplication { get; init; }
  public string HostApplicationVersion { get; init; }
  public string Slug { get; init; }
  public string SpeckleVersion { get; init; }

  public string ApplicationAndVersion => $"{HostApplication} {HostApplicationVersion}";
}
