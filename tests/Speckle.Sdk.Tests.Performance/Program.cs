// See https://aka.ms/new-console-template for more information

#if !MANUAL_RUN
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssemblies([typeof(Program).Assembly]).Run(args);
#else
using Speckle.Sdk.Tests.Performance.Benchmarks;

using var sut = new PipelineDeserialize();
await sut.Setup();
sut.Receive3_sync();
#endif
