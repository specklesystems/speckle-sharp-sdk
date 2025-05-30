namespace Speckle.Sdk.Common;

// MD5 implementation in pure C# (public domain / no dependencies)
// Not for cryptographic purposes
// Using this instead of changing ID generation but avoiding built in MD5 for FIPS compliance

public static class Md5
{
  // Standard initial values
  private static readonly uint[] T =
  [
    0xd76aa478,
    0xe8c7b756,
    0x242070db,
    0xc1bdceee,
    0xf57c0faf,
    0x4787c62a,
    0xa8304613,
    0xfd469501,
    0x698098d8,
    0x8b44f7af,
    0xffff5bb1,
    0x895cd7be,
    0x6b901122,
    0xfd987193,
    0xa679438e,
    0x49b40821,
    0xf61e2562,
    0xc040b340,
    0x265e5a51,
    0xe9b6c7aa,
    0xd62f105d,
    0x02441453,
    0xd8a1e681,
    0xe7d3fbc8,
    0x21e1cde6,
    0xc33707d6,
    0xf4d50d87,
    0x455a14ed,
    0xa9e3e905,
    0xfcefa3f8,
    0x676f02d9,
    0x8d2a4c8a,
    0xfffa3942,
    0x8771f681,
    0x6d9d6122,
    0xfde5380c,
    0xa4beea44,
    0x4bdecfa9,
    0xf6bb4b60,
    0xbebfbc70,
    0x289b7ec6,
    0xeaa127fa,
    0xd4ef3085,
    0x04881d05,
    0xd9d4d039,
    0xe6db99e5,
    0x1fa27cf8,
    0xc4ac5665,
    0xf4292244,
    0x432aff97,
    0xab9423a7,
    0xfc93a039,
    0x655b59c3,
    0x8f0ccc92,
    0xffeff47d,
    0x85845dd1,
    0x6fa87e4f,
    0xfe2ce6e0,
    0xa3014314,
    0x4e0811a1,
    0xf7537e82,
    0xbd3af235,
    0x2ad7d2bb,
    0xeb86d391,
  ];

  public static byte[] ComputeHash(byte[] input)
  {
    // Pad input
    int origLenBits = input.Length * 8;
    int padLen = (56 - (input.Length + 1) % 64 + 64) % 64;
    byte[] padded = new byte[input.Length + 1 + padLen + 8];
    Array.Copy(input, padded, input.Length);
    padded[input.Length] = 0x80;
    BitConverter.GetBytes((long)origLenBits).CopyTo(padded, padded.Length - 8);

    // Initialize MD5 buffer
    uint a = 0x67452301;
    uint b = 0xefcdab89;
    uint c = 0x98badcfe;
    uint d = 0x10325476;

    for (int i = 0; i < padded.Length / 64; i++)
    {
      uint[] M = new uint[16];
      for (int j = 0; j < 16; j++)
      {
        M[j] = BitConverter.ToUInt32(padded, (i * 64) + j * 4);
      }

      uint AA = a,
        BB = b,
        CC = c,
        DD = d;
      for (int j = 0; j < 64; j++)
      {
        uint f,
          g;
        if (j < 16)
        {
          f = (b & c) | (~b & d);
          g = (uint)j;
        }
        else if (j < 32)
        {
          f = (d & b) | (~d & c);
          g = (uint)((5 * j + 1) % 16);
        }
        else if (j < 48)
        {
          f = b ^ c ^ d;
          g = (uint)((3 * j + 5) % 16);
        }
        else
        {
          f = c ^ (b | ~d);
          g = (uint)((7 * j) % 16);
        }

        uint temp = d;
        d = c;
        c = b;
        b += LeftRotate(a + f + T[j] + M[g], S(j));
        a = temp;
      }
      a += AA;
      b += BB;
      c += CC;
      d += DD;
    }

    byte[] output = new byte[16];
    Array.Copy(BitConverter.GetBytes(a), 0, output, 0, 4);
    Array.Copy(BitConverter.GetBytes(b), 0, output, 4, 4);
    Array.Copy(BitConverter.GetBytes(c), 0, output, 8, 4);
    Array.Copy(BitConverter.GetBytes(d), 0, output, 12, 4);
    return output;
  }

  private static int S(int i)
  {
    int[] s = { 7, 12, 17, 22, 5, 9, 14, 20, 4, 11, 16, 23, 6, 10, 15, 21 };
    return s[(i / 16) * 4 + (i % 4)];
  }

  private static uint LeftRotate(uint x, int c) => (x << c) | (x >> (32 - c));

  // Convenience method to get hex string
  public static string GetString(string input)
  {
    var hash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
  }
}
