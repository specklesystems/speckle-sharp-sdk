// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;
using Speckle.Sdk.Tests.Performance.Benchmarks;

var test = new GeneralSerializerTest();
await test.Setup();
var x = test.SpeckleObjectSerializer2Test();

Console.WriteLine("Done!");
// BenchmarkSwitcher.FromAssemblies([typeof(Program).Assembly]).Run(args);
