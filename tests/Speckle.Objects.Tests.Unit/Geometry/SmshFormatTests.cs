using AwesomeAssertions;
using Speckle.Objects.Utils;

namespace Speckle.Objects.Tests.Unit.Geometry;

public class SmshFormatTests
{
  [Fact]
  public void MagicBytes_AreLittleEndianAscii_SMSH()
  {
    // 0x48534D53 == bytes 53 4D 53 48 == ASCII "SMSH" little-endian.
    SmshFormat.Magic.Should().Be(0x48534D53u);
  }

  [Fact]
  public void HeaderSize_AccountsForMagicVersionFlagsAndCounts()
  {
    // 4 (magic) + 2 (version) + 2 (flags) + 4 (vertex_count) + 4 (face_int_count) = 16.
    SmshFormat.HeaderSize.Should().Be(16);
  }

  [Fact]
  public void EncodingName_IsSmshV1()
  {
    SmshFormat.EncodingName.Should().Be("smsh_v1");
  }

  [Fact]
  public void Flags_AreDistinctSingleBits()
  {
    // Bit positions are part of the wire format — locked.
    ((int)SmshFlags.Quantized).Should().Be(1);   // reserved for future
    ((int)SmshFlags.HasNormals).Should().Be(2);
    ((int)SmshFlags.HasUvs).Should().Be(4);
    ((int)SmshFlags.HasColors).Should().Be(8);
  }
}
