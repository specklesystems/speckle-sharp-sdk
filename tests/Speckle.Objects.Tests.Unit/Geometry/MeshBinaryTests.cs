using AwesomeAssertions;
using Speckle.Objects.Geometry;
using Speckle.Objects.Utils;
using Speckle.Sdk.Common;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class MeshBinaryTests
{
  /// <summary>
  /// The wedge mesh from the chat that motivated this work:
  /// 12 triangles, 36 face-corner vertices (with duplicates), 8 unique positions.
  /// MVP encoder does not dedup — the binary keeps all 36 entries.
  /// </summary>
  private static (double[] verts, int[] faces) WedgeMesh()
  {
    double[] verts =
    {
      5112.64229691833, 2954.8287229479, 21.1014995692531,
      5111.77929689795, 2954.8287229479, 21.1014995692531,
      5112.64229691833, 2954.8287229479, 22.339499604539,
      5112.64229691833, 2954.8287229479, 22.339499604539,
      5111.77929689795, 2954.8287229479, 21.1014995692531,
      5111.77929689795, 2954.8287229479, 22.339499604539,
      5111.77929689795, 2954.8287229479, 21.1014995692531,
      5111.77929689795, 2948.80322265035, 21.1014995692531,
      5111.77929689795, 2954.8287229479, 22.339499604539,
      5111.77929689795, 2954.8287229479, 22.339499604539,
      5111.77929689795, 2948.80322265035, 21.1014995692531,
      5111.77929689795, 2948.80322265035, 22.339499604539,
      5111.77929689795, 2948.80322265035, 21.1014995692531,
      5113.99229695708, 2948.80322265035, 21.1014995692531,
      5111.77929689795, 2948.80322265035, 22.339499604539,
      5111.77929689795, 2948.80322265035, 22.339499604539,
      5113.99229695708, 2948.80322265035, 21.1014995692531,
      5113.99229695708, 2948.80322265035, 22.339499604539,
      5113.99229695708, 2948.80322265035, 21.1014995692531,
      5112.64229691833, 2954.8287229479, 21.1014995692531,
      5113.99229695708, 2948.80322265035, 22.339499604539,
      5113.99229695708, 2948.80322265035, 22.339499604539,
      5112.64229691833, 2954.8287229479, 21.1014995692531,
      5112.64229691833, 2954.8287229479, 22.339499604539,
      5112.64229691833, 2954.8287229479, 21.1014995692531,
      5113.99229695708, 2948.80322265035, 21.1014995692531,
      5111.77929689795, 2954.8287229479, 21.1014995692531,
      5111.77929689795, 2954.8287229479, 21.1014995692531,
      5113.99229695708, 2948.80322265035, 21.1014995692531,
      5111.77929689795, 2948.80322265035, 21.1014995692531,
      5111.77929689795, 2954.8287229479, 22.339499604539,
      5111.77929689795, 2948.80322265035, 22.339499604539,
      5112.64229691833, 2954.8287229479, 22.339499604539,
      5112.64229691833, 2954.8287229479, 22.339499604539,
      5111.77929689795, 2948.80322265035, 22.339499604539,
      5113.99229695708, 2948.80322265035, 22.339499604539,
    };
    int[] faces =
    {
      3, 0, 1, 2,    3, 3, 4, 5,    3, 6, 7, 8,    3, 9, 10, 11,
      3, 12, 13, 14, 3, 15, 16, 17, 3, 18, 19, 20, 3, 21, 22, 23,
      3, 24, 25, 26, 3, 27, 28, 29, 3, 30, 31, 32, 3, 33, 34, 35,
    };
    return (verts, faces);
  }

  [Fact]
  public void EncodeToSmsh_WedgeMesh_HasExpectedHeaderAndSize()
  {
    var (verts, faces) = WedgeMesh();

    var bytes = MeshBinaryEncoder.EncodeToSmsh(verts, faces, [], [], []);

    // Unquantized layout, no optional sections:
    //   header                          = 16 bytes
    //   36 vertices × 3 × float64       = 864 bytes
    //   48 face ints × int32            = 192 bytes
    //   total                           = 1072 bytes
    bytes.Length.Should().Be(1072);

    var header = MeshBinaryDecoder.ReadHeader(bytes);
    header.Version.Should().Be(1);
    header.VertexCount.Should().Be(36u);
    header.FaceIntCount.Should().Be(48u);
    header.Flags.Should().NotHaveFlag(SmshFlags.Quantized);
    header.Flags.Should().NotHaveFlag(SmshFlags.HasNormals);
    header.Flags.Should().NotHaveFlag(SmshFlags.HasUvs);
    header.Flags.Should().NotHaveFlag(SmshFlags.HasColors);
  }

  [Fact]
  public void Decode_WedgeMesh_RoundTripsBitExact()
  {
    var (verts, faces) = WedgeMesh();

    var bytes = MeshBinaryEncoder.EncodeToSmsh(verts, faces, [], [], []);
    var decoded = MeshBinaryDecoder.Decode(bytes);

    // No quantization → exact round-trip.
    decoded.Vertices.Should().Equal(verts);
    decoded.Faces.Should().Equal(faces);
    decoded.Normals.Should().BeNull();
    decoded.TextureCoords.Should().BeNull();
    decoded.Colors.Should().BeNull();
  }

  [Fact]
  public void EncodeToSmsh_IsDeterministic()
  {
    // Same geometry encoded twice must produce byte-identical buffers — this is
    // what lets the transport layer's SHA256-keyed blob storage dedup across
    // instances and across sends.
    var (verts, faces) = WedgeMesh();
    var a = MeshBinaryEncoder.EncodeToSmsh(verts, faces, [], [], []);
    var b = MeshBinaryEncoder.EncodeToSmsh(verts, faces, [], [], []);

    a.Should().Equal(b);
  }

  [Fact]
  public void EncodeFromMesh_WritesBlobToTempDir()
  {
    var source = new Mesh
    {
      vertices = [.. WedgeMesh().verts],
      faces = [.. WedgeMesh().faces],
      units = Units.Meters,
    };
    var tempDir = Path.Combine(Path.GetTempPath(), $"smsh-test-{Guid.NewGuid():N}");

    try
    {
      var binary = MeshBinaryEncoder.EncodeFromMesh(source, tempDir);

      binary.units.Should().Be(Units.Meters);
      binary.encoding.Should().Be("smsh_v1");
      binary.geometryBlob.Should().NotBeNull();
      File.Exists(binary.geometryBlob.filePath).Should().BeTrue();

      var bytes = File.ReadAllBytes(binary.geometryBlob.filePath);
      bytes.Length.Should().Be(1072);
    }
    finally
    {
      if (Directory.Exists(tempDir))
      {
        Directory.Delete(tempDir, recursive: true);
      }
    }
  }

  [Fact]
  public void EncodeFromMesh_DefaultsToSpeckleTempDir()
  {
    var source = new Mesh
    {
      vertices = [.. WedgeMesh().verts],
      faces = [.. WedgeMesh().faces],
      units = Units.Meters,
    };
    var expectedDir = Path.Combine(Path.GetTempPath(), "Speckle");

    var binary = MeshBinaryEncoder.EncodeFromMesh(source);

    try
    {
      Path.GetDirectoryName(binary.geometryBlob.filePath).Should().Be(expectedDir);
    }
    finally
    {
      if (File.Exists(binary.geometryBlob.filePath))
      {
        File.Delete(binary.geometryBlob.filePath);
      }
    }
  }

  [Fact]
  public void DecodeToLegacyMesh_RoundTripsThroughBlob()
  {
    var (verts, faces) = WedgeMesh();
    var source = new Mesh
    {
      vertices = [.. verts],
      faces = [.. faces],
      units = Units.Meters,
    };
    var tempDir = Path.Combine(Path.GetTempPath(), $"smsh-test-{Guid.NewGuid():N}");

    try
    {
      var binary = MeshBinaryEncoder.EncodeFromMesh(source, tempDir);
      var roundtripped = MeshBinaryDecoder.DecodeToLegacyMesh(binary, tempDir);

      roundtripped.units.Should().Be(source.units);
      roundtripped.vertices.Should().Equal(source.vertices);
      roundtripped.faces.Should().Equal(source.faces);
    }
    finally
    {
      if (Directory.Exists(tempDir))
      {
        Directory.Delete(tempDir, recursive: true);
      }
    }
  }

  [Fact]
  public void EncodeDecode_WithNormals_RoundTrips()
  {
    double[] verts = { 0, 0, 0, 1, 0, 0, 0, 1, 0 };
    int[] faces = { 3, 0, 1, 2 };
    double[] normals = { 0, 0, 1, 0, 0, 1, 0, 0, 1 };

    var bytes = MeshBinaryEncoder.EncodeToSmsh(verts, faces, normals, [], []);
    var header = MeshBinaryDecoder.ReadHeader(bytes);
    header.Flags.Should().HaveFlag(SmshFlags.HasNormals);

    var decoded = MeshBinaryDecoder.Decode(bytes);
    decoded.Normals.Should().Equal(normals);
  }

  [Fact]
  public void EncodeDecode_WithUvsAndColors_RoundTrips()
  {
    double[] verts = { 0, 0, 0, 1, 0, 0, 0, 1, 0 };
    int[] faces = { 3, 0, 1, 2 };
    double[] uvs = { 0, 0, 1, 0, 0, 1 };
    int[] colors = { unchecked((int)0xFFFF0000), unchecked((int)0xFF00FF00), unchecked((int)0xFF0000FF) };

    var bytes = MeshBinaryEncoder.EncodeToSmsh(verts, faces, [], uvs, colors);
    var header = MeshBinaryDecoder.ReadHeader(bytes);
    header.Flags.Should().HaveFlag(SmshFlags.HasUvs);
    header.Flags.Should().HaveFlag(SmshFlags.HasColors);

    var decoded = MeshBinaryDecoder.Decode(bytes);
    decoded.TextureCoords.Should().Equal(uvs);
    decoded.Colors.Should().Equal(colors);
  }

  [Fact]
  public void Decode_RejectsQuantizedFlag()
  {
    // Forge a buffer with the (reserved) Quantized flag set — decoder must refuse.
    var bytes = MeshBinaryEncoder.EncodeToSmsh([0, 0, 0, 1, 0, 0, 0, 1, 0], [3, 0, 1, 2], [], [], []);
    bytes[6] = 0x01; // flip bit 0 (Quantized)

    Action act = () => MeshBinaryDecoder.Decode(bytes);
    act.Should().Throw<Speckle.Sdk.SpeckleException>().WithMessage("*Quantized*");
  }

  [Fact]
  public void ReadHeader_RejectsBadMagic()
  {
    var bytes = new byte[SmshFormat.HeaderSize];
    Action act = () => MeshBinaryDecoder.ReadHeader(bytes);
    act.Should().Throw<Speckle.Sdk.SpeckleException>().WithMessage("*magic mismatch*");
  }
}
