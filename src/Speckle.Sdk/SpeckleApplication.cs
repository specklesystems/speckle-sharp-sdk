using Speckle.InterfaceGenerator;

namespace Speckle.Sdk;

[GenerateAutoInterface]
public class SpeckleApplication : ISpeckleApplication
{
  public required string HostApplication { get; init; }
  public required string HostApplicationVersion { get; init; }
  public required string Slug { get; init; }
  public required string SpeckleVersion { get; init; }

  public string ApplicationAndVersion => $"{HostApplication} {HostApplicationVersion}";
}
