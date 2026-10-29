using System.Buffers.Binary;

namespace FreeSlate.Opc;

/// <summary>
/// AES (FIPS-197) and MD5 (RFC 1321) in managed code, for where the platform has none (the browser: .NET's WebAssembly
/// runtime hashes with SHA but throws PlatformNotSupportedException for AES and MD5): the decryption of password-protected
/// documents needs AES in ECB, CBC and CFB8 mode without padding (see OfficeCrypto) and MD5 for agile descriptors that name
/// it. Plain table-free code, which a document's package is decrypted with once.
/// </summary>
static class ManagedCrypto
{
    static readonly byte[] SBox = new byte[256], InverseSBox = new byte[256];

    static ManagedCrypto()
    {
        // The S-box: the multiplicative inverse in GF(2^8) through the affine transformation (FIPS-197 5.1.1), built from
        // 3 as a generator of the field's multiplicative group.
        byte p = 1, q = 1;
        do
        {
            p = (byte)(p ^ (p << 1) ^ ((p & 0x80) != 0 ? 0x1B : 0));
            q ^= (byte)(q << 1);
            q ^= (byte)(q << 2);
            q ^= (byte)(q << 4);
            if ((q & 0x80) != 0)
                q ^= 0x09;
            var x = (byte)(q ^ Rotate(q, 1) ^ Rotate(q, 2) ^ Rotate(q, 3) ^ Rotate(q, 4));
            SBox[p] = (byte)(x ^ 0x63);
        }
        while (p != 1);
        SBox[0] = 0x63;
        for (var i = 0; i < 256; i++)
            InverseSBox[SBox[i]] = (byte)i;

        static byte Rotate(byte value, int shift) => (byte)((value << shift) | (value >> (8 - shift)));
    }

    /// <summary>A product in GF(2^8) with the AES polynomial.</summary>
    static byte Multiply(byte a, byte b)
    {
        byte product = 0;
        while (b != 0)
        {
            if ((b & 1) != 0)
                product ^= a;
            a = (byte)((a << 1) ^ ((a & 0x80) != 0 ? 0x1B : 0));
            b >>= 1;
        }
        return product;
    }

    /// <summary>The round keys of a 128, 192 or 256 bit key (FIPS-197 5.2), 16 bytes for each round and the initial one.</summary>
    static byte[] Expand(byte[] key)
    {
        if (key.Length is not (16 or 24 or 32))
            throw new ArgumentException("An AES key has 16, 24 or 32 bytes.", nameof(key));
        var words = key.Length / 4;
        var rounds = words + 6;
        var expanded = new byte[16 * (rounds + 1)];
        key.CopyTo(expanded, 0);
        byte rcon = 1;
        Span<byte> t = stackalloc byte[4];
        for (var i = words; i < 4 * (rounds + 1); i++)
        {
            expanded.AsSpan(4 * (i - 1), 4).CopyTo(t);
            if (i % words == 0)
            {
                (t[0], t[1], t[2], t[3]) = ((byte)(SBox[t[1]] ^ rcon), SBox[t[2]], SBox[t[3]], SBox[t[0]]);
                rcon = Multiply(rcon, 2);
            }
            else if (words > 6 && i % words == 4)
            {
                for (var k = 0; k < 4; k++)
                    t[k] = SBox[t[k]];
            }
            for (var k = 0; k < 4; k++)
                expanded[4 * i + k] = (byte)(expanded[4 * (i - words) + k] ^ t[k]);
        }
        return expanded;
    }

    /// <summary>One block encrypted in place (FIPS-197 5.1).</summary>
    static void Encrypt(byte[] keys, Span<byte> state)
    {
        var rounds = keys.Length / 16 - 1;
        AddRoundKey(state, keys, 0);
        for (var round = 1; round <= rounds; round++)
        {
            for (var i = 0; i < 16; i++)
                state[i] = SBox[state[i]];
            ShiftRows(state, inverse: false);
            if (round < rounds)
                MixColumns(state, inverse: false);
            AddRoundKey(state, keys, round);
        }
    }

    /// <summary>One block decrypted in place (FIPS-197 5.3).</summary>
    static void Decrypt(byte[] keys, Span<byte> state)
    {
        var rounds = keys.Length / 16 - 1;
        AddRoundKey(state, keys, rounds);
        for (var round = rounds - 1; round >= 0; round--)
        {
            ShiftRows(state, inverse: true);
            for (var i = 0; i < 16; i++)
                state[i] = InverseSBox[state[i]];
            AddRoundKey(state, keys, round);
            if (round > 0)
                MixColumns(state, inverse: true);
        }
    }

    static void AddRoundKey(Span<byte> state, byte[] keys, int round)
    {
        for (var i = 0; i < 16; i++)
            state[i] ^= keys[16 * round + i];
    }

    /// <summary>The rows of the state (column by column in the block) shifted left by their index, or back.</summary>
    static void ShiftRows(Span<byte> state, bool inverse)
    {
        Span<byte> copy = stackalloc byte[16];
        state.CopyTo(copy);
        for (var row = 1; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                var from = inverse ? (column - row + 4) % 4 : (column + row) % 4;
                state[4 * column + row] = copy[4 * from + row];
            }
        }
    }

    static void MixColumns(Span<byte> state, bool inverse)
    {
        var (a, b, c, d) = inverse ? ((byte)14, (byte)11, (byte)13, (byte)9) : ((byte)2, (byte)3, (byte)1, (byte)1);
        for (var column = 0; column < 4; column++)
        {
            var s = state.Slice(4 * column, 4);
            var (s0, s1, s2, s3) = (s[0], s[1], s[2], s[3]);
            s[0] = (byte)(Multiply(s0, a) ^ Multiply(s1, b) ^ Multiply(s2, c) ^ Multiply(s3, d));
            s[1] = (byte)(Multiply(s0, d) ^ Multiply(s1, a) ^ Multiply(s2, b) ^ Multiply(s3, c));
            s[2] = (byte)(Multiply(s0, c) ^ Multiply(s1, d) ^ Multiply(s2, a) ^ Multiply(s3, b));
            s[3] = (byte)(Multiply(s0, b) ^ Multiply(s1, c) ^ Multiply(s2, d) ^ Multiply(s3, a));
        }
    }

    /// <summary>Whole blocks decrypted in ECB mode, without padding.</summary>
    public static byte[] DecryptEcb(byte[] key, ReadOnlySpan<byte> data)
    {
        var keys = Expand(key);
        var plain = data[..(data.Length / 16 * 16)].ToArray();
        for (var offset = 0; offset < plain.Length; offset += 16)
            Decrypt(keys, plain.AsSpan(offset, 16));
        return plain;
    }

    /// <summary>Whole blocks decrypted in CBC mode, without padding.</summary>
    public static byte[] DecryptCbc(byte[] key, byte[] iv, ReadOnlySpan<byte> data)
    {
        var keys = Expand(key);
        var plain = data[..(data.Length / 16 * 16)].ToArray();
        Span<byte> previous = stackalloc byte[16], block = stackalloc byte[16];
        iv.AsSpan(0, 16).CopyTo(previous);
        for (var offset = 0; offset < plain.Length; offset += 16)
        {
            var cipher = plain.AsSpan(offset, 16);
            cipher.CopyTo(block);
            Decrypt(keys, cipher);
            for (var i = 0; i < 16; i++)
                cipher[i] ^= previous[i];
            block.CopyTo(previous);
        }
        return plain;
    }

    /// <summary>Data decrypted in CFB mode with a feedback of eight bits (CFB8), byte by byte.</summary>
    public static byte[] DecryptCfb8(byte[] key, byte[] iv, ReadOnlySpan<byte> data)
    {
        var keys = Expand(key);
        var plain = new byte[data.Length];
        Span<byte> register = stackalloc byte[16], output = stackalloc byte[16];
        iv.AsSpan(0, 16).CopyTo(register);
        for (var i = 0; i < data.Length; i++)
        {
            register.CopyTo(output);
            Encrypt(keys, output);
            plain[i] = (byte)(data[i] ^ output[0]);
            register[1..].CopyTo(register);
            register[15] = data[i];
        }
        return plain;
    }

    static readonly uint[] Sines = [.. Enumerable.Range(0, 64).Select(i => (uint)(long)Math.Floor(Math.Abs(Math.Sin(i + 1)) * 4294967296.0))];

    static readonly int[] Shifts = [7, 12, 17, 22, 5, 9, 14, 20, 4, 11, 16, 23, 6, 10, 15, 21];

    /// <summary>The MD5 digest of data (RFC 1321).</summary>
    public static byte[] Md5(ReadOnlySpan<byte> data)
    {
        var length = (data.Length + 8) / 64 * 64 + 64;
        var message = new byte[length];
        data.CopyTo(message);
        message[data.Length] = 0x80;
        BinaryPrimitives.WriteUInt64LittleEndian(message.AsSpan(length - 8), (ulong)data.Length * 8);
        uint a0 = 0x67452301, b0 = 0xefcdab89, c0 = 0x98badcfe, d0 = 0x10325476;
        Span<uint> m = stackalloc uint[16];
        for (var offset = 0; offset < length; offset += 64)
        {
            for (var i = 0; i < 16; i++)
                m[i] = BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(offset + 4 * i));
            var (a, b, c, d) = (a0, b0, c0, d0);
            for (var i = 0; i < 64; i++)
            {
                var (f, g) = (i / 16) switch
                {
                    0 => ((b & c) | (~b & d), i),
                    1 => ((d & b) | (~d & c), (5 * i + 1) % 16),
                    2 => (b ^ c ^ d, (3 * i + 5) % 16),
                    _ => (c ^ (b | ~d), 7 * i % 16),
                };
                f += a + Sines[i] + m[g];
                (a, d, c) = (d, c, b);
                b += uint.RotateLeft(f, Shifts[i / 16 * 4 + i % 4]);
            }
            (a0, b0, c0, d0) = (a0 + a, b0 + b, c0 + c, d0 + d);
        }
        var digest = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(digest, a0);
        BinaryPrimitives.WriteUInt32LittleEndian(digest.AsSpan(4), b0);
        BinaryPrimitives.WriteUInt32LittleEndian(digest.AsSpan(8), c0);
        BinaryPrimitives.WriteUInt32LittleEndian(digest.AsSpan(12), d0);
        return digest;
    }
}
