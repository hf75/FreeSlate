using System.Buffers.Binary;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Fidelity.PowerPoint;

/// <summary>
/// Password-protected decks (catalog: package.encryption), both opened with the password in their .password file:
/// encrypted-agile is encrypted by PowerPoint itself (agile encryption: AES-256, SHA-512, 100 000 spins);
/// encrypted-standard is saved plainly and then encrypted here with standard encryption (AES-128, SHA-1, the
/// scheme of Office 2007), which PowerPoint no longer writes but still opens.
/// </summary>
[SupportedOSPlatform("windows")]
static class EncryptedDecks
{
    public const string Password = "FreeSlate";

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var title = slide.Shapes.AddTextbox(1, 60, 40, 840, 80);
        title.TextFrame.TextRange.Text = "A password-protected deck";
        title.TextFrame.TextRange.Font.Size = 40;
        var shape = slide.Shapes.AddShape(9, 120, 180, 300, 220);
        shape.Fill.ForeColor.RGB = 0x3C9BE6;
        var star = slide.Shapes.AddShape(92, 540, 180, 260, 240);
        star.Fill.ForeColor.RGB = 0x2BB0F2;
        var second = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var body = second.Shapes.AddTextbox(1, 60, 60, 840, 400);
        body.TextFrame.TextRange.Text = "Second slide\rwith several paragraphs\rof text";
        body.TextFrame.TextRange.Font.Size = 32;
    }

    /// <summary>
    /// Lets PowerPoint encrypt the deck when it saves it (agile encryption); it also removes the author's name, which
    /// cannot be replaced in the encrypted file afterwards.
    /// </summary>
    public static void BuildAgile(dynamic deck)
    {
        Build(deck);
        deck.RemovePersonalInformation = PowerPointSession.MsoTrue;
        deck.Password = Password;
    }

    /// <summary>Replaces the saved deck by a compound file with its standard-encrypted package.</summary>
    public static void EncryptStandard(string path)
    {
        var package = File.ReadAllBytes(path);
        // Fixed salt and verifier: the test file stays the same from run to run.
        var salt = Enumerable.Range(0, 16).Select(i => (byte)(0x10 + i * 7)).ToArray();
        var verifier = Enumerable.Range(0, 16).Select(i => (byte)(0xA0 ^ i * 13)).ToArray();
        var key = StandardKey(salt, Password, 128);

        using var aes = Aes.Create();
        aes.Key = key;
        var info = new MemoryStream();
        var writer = new BinaryWriter(info);
        writer.Write((ushort)4);
        writer.Write((ushort)2);
        writer.Write(0x24u);   // fCryptoAPI | fAES
        var csp = Encoding.Unicode.GetBytes("Microsoft Enhanced RSA and AES Cryptographic Provider\0");
        writer.Write(32u + (uint)csp.Length);
        writer.Write(0x24u);   // header flags
        writer.Write(0u);      // size extra
        writer.Write(0x660Eu); // AES-128
        writer.Write(0x8004u); // SHA-1
        writer.Write(128u);
        writer.Write(0x18u);   // PROV_RSA_AES
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(csp);
        writer.Write(16u);
        writer.Write(salt);
        writer.Write(aes.EncryptEcb(verifier, PaddingMode.None));
        writer.Write(20u);
        var hash = new byte[32];
        SHA1.HashData(verifier).CopyTo(hash, 0);
        writer.Write(aes.EncryptEcb(hash, PaddingMode.None));
        writer.Flush();

        var padded = new byte[(package.Length + 15) / 16 * 16];
        package.CopyTo(padded, 0);
        var encrypted = new byte[8 + padded.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(encrypted, (ulong)package.Length);
        aes.EncryptEcb(padded, PaddingMode.None).CopyTo(encrypted, 8);

        File.WriteAllBytes(path, CompoundFileWriter.Write([new("EncryptionInfo", info.ToArray()), new("EncryptedPackage", encrypted)]));
    }

    /// <summary>The standard encryption key ([MS-OFFCRYPTO] 2.3.4.7): 50 000 SHA-1 spins, block 0, then the 0x36/0x5C pads.</summary>
    static byte[] StandardKey(byte[] salt, string password, int keyBits)
    {
        var h = SHA1.HashData([.. salt, .. Encoding.Unicode.GetBytes(password)]);
        var round = new byte[24];
        for (var i = 0; i < 50000; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(round, (uint)i);
            h.CopyTo(round, 4);
            h = SHA1.HashData(round);
        }
        h = SHA1.HashData([.. h, 0, 0, 0, 0]);
        byte[] Pad(byte value)
        {
            var buffer = Enumerable.Repeat(value, 64).ToArray();
            for (var i = 0; i < h.Length; i++)
                buffer[i] ^= h[i];
            return SHA1.HashData(buffer);
        }
        return Pad(0x36).Concat(Pad(0x5C)).Take(keyBits / 8).ToArray();
    }
}

/// <summary>
/// A minimal writer of OLE compound files ([MS-CFB], version 3, 512-byte sectors) with streams in the root storage:
/// small streams go to the mini stream, the directory is a balanced red-black tree.
/// </summary>
static class CompoundFileWriter
{
    public sealed record Stream(string Name, byte[] Data);

    const int Sector = 512, MiniSector = 64, Cutoff = 4096;
    const uint Free = 0xFFFFFFFF, End = 0xFFFFFFFE, FatSector = 0xFFFFFFFD, NoStream = 0xFFFFFFFF;

    public static byte[] Write(IReadOnlyList<Stream> streams)
    {
        // Mini stream: the small streams in 64-byte sectors.
        var mini = new MemoryStream();
        var miniFat = new List<uint>();
        var starts = new uint[streams.Count];
        for (var i = 0; i < streams.Count; i++)
        {
            if (streams[i].Data.Length >= Cutoff)
                continue;
            var count = Math.Max(1, (streams[i].Data.Length + MiniSector - 1) / MiniSector);
            starts[i] = (uint)miniFat.Count;
            for (var k = 0; k < count; k++)
                miniFat.Add(k == count - 1 ? End : (uint)(miniFat.Count + 1));
            mini.Write(streams[i].Data);
            mini.Write(new byte[count * MiniSector - streams[i].Data.Length]);
        }

        static int Sectors(long bytes) => (int)((bytes + Sector - 1) / Sector);
        var directorySectors = Sectors((streams.Count + 1) * 128L);
        var miniFatSectors = Sectors(miniFat.Count * 4L);
        var miniStreamSectors = Sectors(mini.Length);
        var large = streams.Select(s => s.Data.Length >= Cutoff ? Sectors(s.Data.Length) : 0).ToArray();
        var others = directorySectors + miniFatSectors + miniStreamSectors + large.Sum();
        var fatSectors = 1;
        while (fatSectors * (Sector / 4) < fatSectors + others)
            fatSectors++;
        if (fatSectors > 109)
            throw new NotSupportedException("The file is too large for a header-only DIFAT.");

        var fat = new List<uint>();
        for (var i = 0; i < fatSectors; i++)
            fat.Add(FatSector);
        uint Chain(int count)
        {
            if (count == 0)
                return End;
            var start = (uint)fat.Count;
            for (var k = 0; k < count; k++)
                fat.Add(k == count - 1 ? End : (uint)(fat.Count + 1));
            return start;
        }
        var directoryStart = Chain(directorySectors);
        var miniFatStart = Chain(miniFatSectors);
        var miniStreamStart = Chain(miniStreamSectors);
        for (var i = 0; i < streams.Count; i++)
        {
            if (large[i] > 0)
                starts[i] = Chain(large[i]);
        }
        while (fat.Count % (Sector / 4) != 0 || fat.Count < fatSectors * (Sector / 4))
            fat.Add(Free);

        // Directory: the root and its streams as a balanced binary tree in [MS-CFB] name order.
        var order = Enumerable.Range(0, streams.Count)
            .OrderBy(i => streams[i].Name.Length).ThenBy(i => streams[i].Name.ToUpperInvariant(), StringComparer.Ordinal).ToList();
        var left = new uint[streams.Count];
        var right = new uint[streams.Count];
        var red = new bool[streams.Count];
        var depth = new int[streams.Count];
        uint Tree(int from, int to, int level)
        {
            if (from > to)
                return NoStream;
            var middle = (from + to) / 2;
            var node = order[middle];
            depth[node] = level;
            left[node] = Tree(from, middle - 1, level + 1);
            right[node] = Tree(middle + 1, to, level + 1);
            return (uint)node + 1;
        }
        var top = Tree(0, streams.Count - 1, 0);
        var deepest = streams.Count > 0 ? depth.Max() : 0;
        for (var i = 0; i < streams.Count; i++)
            red[i] = deepest > 0 && depth[i] == deepest;

        var directory = new byte[directorySectors * Sector];
        void Entry(int index, string name, byte type, bool isRed, uint l, uint r, uint child, uint start, long size)
        {
            var span = directory.AsSpan(index * 128, 128);
            var bytes = Encoding.Unicode.GetBytes(name);
            bytes.CopyTo(span);
            BinaryPrimitives.WriteUInt16LittleEndian(span[0x40..], (ushort)(bytes.Length + 2));
            span[0x42] = type;
            span[0x43] = isRed ? (byte)0 : (byte)1;
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x44..], l);
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x48..], r);
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x4C..], child);
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x74..], start);
            BinaryPrimitives.WriteUInt64LittleEndian(span[0x78..], (ulong)size);
        }
        Entry(0, "Root Entry", 5, false, NoStream, NoStream, top, mini.Length > 0 ? miniStreamStart : End, mini.Length);
        for (var i = 0; i < streams.Count; i++)
            Entry(i + 1, streams[i].Name, 2, red[i], left[i], right[i], NoStream, starts[i], streams[i].Data.Length);
        for (var i = streams.Count + 1; i < directorySectors * 4; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(i * 128 + 0x44), NoStream);
            BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(i * 128 + 0x48), NoStream);
            BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(i * 128 + 0x4C), NoStream);
        }

        var output = new MemoryStream();
        var header = new byte[Sector];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x18), 0x3E);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x1A), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x1C), 0xFFFE);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x1E), 9);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x20), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x2C), (uint)fatSectors);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x30), directoryStart);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x38), Cutoff);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x3C), miniFatSectors > 0 ? miniFatStart : End);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x40), (uint)miniFatSectors);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x44), End);
        for (var i = 0; i < 109; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x4C + i * 4), i < fatSectors ? (uint)i : Free);
        output.Write(header);

        foreach (var entry in fat.Take(fatSectors * (Sector / 4)))
            output.Write(BitConverter.GetBytes(entry));
        output.Write(directory);
        var miniFatBytes = new byte[miniFatSectors * Sector];
        for (var i = 0; i < miniFatBytes.Length / 4; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(miniFatBytes.AsSpan(i * 4), i < miniFat.Count ? miniFat[i] : Free);
        output.Write(miniFatBytes);
        output.Write(mini.ToArray());
        output.Write(new byte[miniStreamSectors * Sector - mini.Length]);
        for (var i = 0; i < streams.Count; i++)
        {
            if (large[i] == 0)
                continue;
            output.Write(streams[i].Data);
            output.Write(new byte[large[i] * Sector - streams[i].Data.Length]);
        }
        return output.ToArray();
    }
}
