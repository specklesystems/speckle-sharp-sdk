// See https://aka.ms/new-console-template for more information

using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssemblies([typeof(Program).Assembly]).Run(args);
