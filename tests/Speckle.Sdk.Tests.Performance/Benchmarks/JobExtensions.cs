using BenchmarkDotNet.Jobs;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

public static class JobExtensions
{
  public static Job WithConstants(this Job job, string constants) =>
    job.WithArguments([new MsBuildArgument("/p:DefineConstants=\"" + constants + "\"")]);

  public static Job WithSpeckle(this Job job, string nuget) =>
    job.WithNuGet("Speckle.Sdk", nuget).WithNuGet("Speckle.Objects", nuget).WithId(nuget);
}
