﻿using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

namespace Speckle.Sdk.Tests.Performance;

[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RegressionTestConfigAttribute : Attribute, IConfigSource
{
  public IConfig Config { get; private set; }

  public RegressionTestConfigAttribute(
    int launchCount = 1,
    int warmupCount = 0,
    int iterationCount = 10,
    RunStrategy strategy = RunStrategy.Monitoring,
    bool includeHead = true,
    params string[] nugetVersions
  )
  {
    List<Job> jobs = new();

    if (includeHead)
    {
      jobs.Add(
        new Job("Head")
          .WithStrategy(strategy)
          .WithLaunchCount(launchCount)
          .WithWarmupCount(warmupCount)
          .WithIterationCount(iterationCount)
      );
    }

    bool isBaseline = true;
    foreach (var version in nugetVersions)
    {
      jobs.Add(
        new Job(version)
          .WithStrategy(strategy)
          .WithLaunchCount(launchCount)
          .WithWarmupCount(warmupCount)
          .WithIterationCount(iterationCount)
          .WithNuGet("Speckle.Objects", version)
          .WithBaseline(isBaseline)
      );

      isBaseline = false;
    }

    Config = ManualConfig.CreateEmpty().AddJob(jobs.ToArray());
  }
}
