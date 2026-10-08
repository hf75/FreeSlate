using System.Buffers.Binary;
using System.Text;

namespace FreeSlate.Opc;

/// <summary>
/// A reader for OLE compound files ([MS-CFB]): the sector allocation table (with its DIFAT extension), the
/// directory and the mini stream, enough to read named streams such as those of an encrypted package.
/// </summary>
public sealed class CompoundFile
{
    const uint EndOfChain = 0xFFFFFFFE;
    const uint FreeSector = 0xFFFFFFFF;

    static ReadOnlySpan<byte> Signature => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    readonly byte[] data;
    readonly int sectorSize;
    readonly int miniSectorSize;
    readonly uint miniStreamCutoff;
    readonly List<uint> fat = [];
    readonly List<uint> miniFat = [];
    readonly byte[] miniStream;
    readonly List<(string Name, int Type, uint Start, long Size)> entries = [];

    /// <summary>Whether the bytes start like a compound file (an encrypted Office document, a .ppt …).</summary>
    public static bool IsCompoundFile(ReadOnlySpan<byte> bytes) => bytes.Length >= 512 && bytes[..8].SequenceEqual(Signature);

    public CompoundFile(byte[] data)
    {
        if (!IsCompoundFile(data))
            throw new InvalidDataException("Not a compound file.");
        this.data = data;
        sectorSize = 1 << U16(0x1E);
        miniSectorSize = 1 << U16(0x20);
        miniStreamCutoff = U32(0x38);
        if (sectorSize is not (512 or 4096) || miniSectorSize > sectorSize)
            throw new InvalidDataException("Unsupported compound file sector size.");

        // The FAT sectors: 109 in the header, more in the DIFAT chain.
        var fatSectors = new List<uint>();
        for (var i = 0; i < 109; i++)
            Add(fatSectors, U32(0x4C + i * 4));
        var difat = U32(0x44);
        var perSector = sectorSize / 4 - 1;
        for (var guard = 0; difat < EndOfChain - 3 && guard < 1 << 20; guard++)
        {
            var offset = Offset(difat);
            for (var i = 0; i < perSector; i++)
                Add(fatSectors, U32(offset + i * 4));
            difat = U32(offset + perSector * 4);
        }
        foreach (var sector in fatSectors)
        {
            var offset = Offset(sector);
            for (var i = 0; i < sectorSize / 4 && offset + i * 4 + 4 <= data.Length; i++)
                fat.Add(U32(offset + i * 4));
        }

        foreach (var sector in Chain(U32(0x3C)))
        {
            var offset = Offset(sector);
            for (var i = 0; i < sectorSize / 4; i++)
                miniFat.Add(U32(offset + i * 4));
        }

        var directory = ReadChain(U32(0x30), long.MaxValue);
        for (var offset = 0; offset + 128 <= directory.Length; offset += 128)
        {
            var nameLength = Math.Clamp((int)BinaryPrimitives.ReadUInt16LittleEndian(directory.AsSpan(offset + 0x40)), 2, 64);
            var name = Encoding.Unicode.GetString(directory, offset, nameLength - 2);
            var type = directory[offset + 0x42];
            var start = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(offset + 0x74));
            var size = (long)BinaryPrimitives.ReadUInt64LittleEndian(directory.AsSpan(offset + 0x78));
            if (sectorSize == 512)
                size &= 0xFFFFFFFF;   // version 3 files leave the high half undefined
            entries.Add((name, type, start, size));
        }
        miniStream = entries.Count > 0 && entries[0].Type == 5 ? ReadChain(entries[0].Start, entries[0].Size) : [];

        static void Add(List<uint> list, uint sector)
        {
            if (sector < EndOfChain - 3)
                list.Add(sector);
        }
    }

    /// <summary>The streams' names.</summary>
    public IEnumerable<string> Streams => entries.Where(e => e.Type == 2).Select(e => e.Name);

    /// <summary>The contents of the first stream with the given name, or null.</summary>
    public byte[]? Read(string name)
    {
        foreach (var (entryName, type, start, size) in entries)
        {
            if (type != 2 || !string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (size >= miniStreamCutoff)
                return ReadChain(start, size);
            var result = new byte[size];
            var written = 0;
            var sector = start;
            for (var guard = 0; written < size && sector < EndOfChain && guard <= miniFat.Count; guard++)
            {
                var offset = (long)sector * miniSectorSize;
                var count = (int)Math.Min(miniSectorSize, size - written);
                if (offset + count > miniStream.Length)
                    break;
                Array.Copy(miniStream, offset, result, written, count);
                written += count;
                sector = sector < miniFat.Count ? miniFat[(int)sector] : EndOfChain;
            }
            return result;
        }
        return null;
    }

    IEnumerable<uint> Chain(uint start)
    {
        var sector = start;
        for (var guard = 0; sector < EndOfChain - 3 && guard <= fat.Count; guard++)
        {
            yield return sector;
            sector = sector < fat.Count ? fat[(int)sector] : EndOfChain;
        }
    }

    byte[] ReadChain(uint start, long size)
    {
        using var buffer = new MemoryStream();
        foreach (var sector in Chain(start))
        {
            var offset = Offset(sector);
            var count = (int)Math.Min(sectorSize, Math.Min(size - buffer.Length, data.Length - offset));
            if (count <= 0)
                break;
            buffer.Write(data, offset, count);
        }
        return buffer.ToArray();
    }

    int Offset(uint sector) => (int)Math.Min((sector + 1L) * sectorSize, data.Length);

    ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));

    uint U32(int offset) => offset + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset)) : FreeSector;
}
