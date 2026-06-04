// See https://aka.ms/new-console-template for more information

#if !MANUAL_RUN
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssemblies([typeof(Program).Assembly]).Run(args);
#else
using Speckle.Sdk.Tests.Performance.Benchmarks;

using var sut = new PipelineDeserialize();
sut.MaxDegreeOfParallelism = 32;
await sut.Setup();
await sut.Receive3();
#endif
