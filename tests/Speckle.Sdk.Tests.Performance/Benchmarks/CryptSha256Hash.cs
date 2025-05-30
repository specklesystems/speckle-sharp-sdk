using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput)]
public class CryptSha256Hash
{
  private string testData;

  [GlobalSetup]
  public void Setup()
  {
    var random = new Random(420);
    testData = new string(Enumerable.Range(0, 10_000_000).Select(_ => (char)random.Next(32, 127)).ToArray());
  }

  [Benchmark]
  public string Sha256()
  {
    return Speckle.Sdk.Common.Sha256.GetString(testData);
  }

  [Benchmark]
  public string Sha256_Span()
  {
    return Speckle.Sdk.Common.Sha256.GetString(testData.AsSpan());
  }
}
