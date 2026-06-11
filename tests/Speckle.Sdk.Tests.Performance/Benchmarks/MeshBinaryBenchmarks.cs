using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;
using Speckle.Sdk.Serialisation;

namespace Speckle.Sdk.Tests.Performance.Benchmarks;

/// <summary>
/// Measures encode/decode throughput and on-the-wire size for the binary
/// mesh format against the legacy JSON Mesh representation. Run via
/// <c>dotnet run -c Release --project tests/Speckle.Sdk.Tests.Performance -- --filter "*MeshBinary*"</c>.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput)]
public class MeshBinaryBenchmarks
{
  // Three representative shape sizes:
  //   - Tiny: the wedge from the originating chat (12 tris)
  //   - Wall: typical building-element mesh (10k tris)
  //   - Terrain: large surface mesh (500k tris)
  [Params(12, 10_000, 500_000)]
  public int TriangleCount;

  private Mesh _mesh;
  private byte[] _binaryBytes;
  private string _legacyJson;

  [GlobalSetup]
  public void Setup()
  {
    _mesh = BuildSyntheticGridMesh(TriangleCount);
    _binaryBytes = MeshBinaryEncoder.EncodeToSmsh(
      _mesh.vertices.ToArray(),
      _mesh.faces.ToArray(),
      [], [], []
    );

    var serializer = new SpeckleObjectSerializer();
    _legacyJson = serializer.Serialize(_mesh);
  }

  [Benchmark(Baseline = true, Description = "Legacy JSON size")]
  public int LegacyJsonSize() => _legacyJson.Length;

  [Benchmark(Description = "SMSH bytes size")]
  public int BinarySize() => _binaryBytes.Length;

  [Benchmark(Description = "Encode legacy → SMSH bytes")]
  public byte[] EncodeToSmsh() =>
    MeshBinaryEncoder.EncodeToSmsh(
      _mesh.vertices.ToArray(),
      _mesh.faces.ToArray(),
      [], [], []
    );

  [Benchmark(Description = "Decode SMSH → DecodedMesh")]
  public DecodedMesh DecodeFromSmsh() => MeshBinaryDecoder.Decode(_binaryBytes);

  /// <summary>
  /// Builds a triangulated grid mesh with the requested triangle count. Each row
  /// of the grid contributes 2 triangles, so the grid is sqrt(N/2) cells per side.
  /// Vertex positions use realistic survey-coordinate offsets, mirroring the chat
  /// example so we exercise the same precision regime real Speckle data hits.
  /// </summary>
  private static Mesh BuildSyntheticGridMesh(int triangleCount)
  {
    int side = (int)Math.Ceiling(Math.Sqrt(triangleCount / 2.0));

    var vertices = new List<double>();
    var faces = new List<int>();

    const double originX = 5112.0;
    const double originY = 2948.0;
    const double cell = 0.05;

    for (int y = 0; y <= side; y++)
    {
      for (int x = 0; x <= side; x++)
      {
        vertices.Add(originX + x * cell);
        vertices.Add(originY + y * cell);
        vertices.Add(21.1 + 0.001 * (x + y));
      }
    }

    int emitted = 0;
    for (int y = 0; y < side && emitted < triangleCount; y++)
    {
      for (int x = 0; x < side && emitted < triangleCount; x++)
      {
        int i0 = y * (side + 1) + x;
        int i1 = i0 + 1;
        int i2 = i0 + (side + 1);
        int i3 = i2 + 1;
        faces.Add(3); faces.Add(i0); faces.Add(i1); faces.Add(i2);
        emitted++;
        if (emitted >= triangleCount) { break; }
        faces.Add(3); faces.Add(i1); faces.Add(i3); faces.Add(i2);
        emitted++;
      }
    }

    return new Mesh
    {
      vertices = vertices,
      faces = faces,
      units = Units.Meters,
    };
  }
}
