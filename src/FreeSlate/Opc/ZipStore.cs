using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace FreeSlate.Opc;

/// <summary>An entry of a ZIP file's central directory (PKWARE's APPNOTE.TXT 4.3.12): how and where its data is stored.</summary>
sealed record ZipRecord(byte[] Name, ushort Flags, ushort Method, ushort Time, ushort Date, uint Crc, uint Compressed, uint Size, uint Offset);

/// <summary>
/// The entries of a ZIP file as they are stored, to be copied without unpacking them (APPNOTE.TXT 4.3): the central
/// directory read through its end record, the data found after each local header, a ZIP file written entry by entry
/// with its central directory. ZIP64 files and those near its limits (4 GB, 65 535 entries) are left to ZipArchive.
/// </summary>
static class ZipStore
{
    const uint LocalHeader = 0x04034B50, DirectoryHeader = 0x02014B50, DirectoryEnd = 0x06054B50;

    /// <summary>The entries of the central directory in their order, or null where it is not a plain ZIP file this reads.</summary>
    public static List<ZipRecord>? Records(Stream zip)
    {
        if (!zip.CanSeek || zip.Length < 22 || zip.Length > uint.MaxValue)
            return null;
        // The end record lies within the last 22 bytes and a comment of up to 65 535.
        var tail = (int)Math.Min(zip.Length, 22 + 0xFFFF);
        var end = Read(zip, zip.Length - tail, tail);
        var at = -1;
        for (var i = tail - 22; i >= 0 && at < 0; i--)
            if (BinaryPrimitives.ReadUInt32LittleEndian(end.AsSpan(i)) == DirectoryEnd && i + 22 + BinaryPrimitives.ReadUInt16LittleEndian(end.AsSpan(i + 20)) == tail)
                at = i;
        if (at < 0)
            return null;
        var span = end.AsSpan(at);
        var (disk, count, size, offset) = (BinaryPrimitives.ReadUInt16LittleEndian(span[4..]), BinaryPrimitives.ReadUInt16LittleEndian(span[10..]),
            BinaryPrimitives.ReadUInt32LittleEndian(span[12..]), BinaryPrimitives.ReadUInt32LittleEndian(span[16..]));
        if (disk != 0 || count == 0xFFFF || size == uint.MaxValue || offset == uint.MaxValue || (long)offset + size > zip.Length)
            return null;
        var directory = Read(zip, offset, (int)size);
        var records = new List<ZipRecord>(count);
        var p = 0;
        for (var k = 0; k < count; k++)
        {
            if (p + 46 > directory.Length || BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(p)) != DirectoryHeader)
                return null;
            var h = directory.AsSpan(p);
            var (nameLength, extraLength, commentLength) = (BinaryPrimitives.ReadUInt16LittleEndian(h[28..]), BinaryPrimitives.ReadUInt16LittleEndian(h[30..]),
                BinaryPrimitives.ReadUInt16LittleEndian(h[32..]));
            var record = new ZipRecord(h.Slice(46, nameLength).ToArray(), BinaryPrimitives.ReadUInt16LittleEndian(h[8..]),
                BinaryPrimitives.ReadUInt16LittleEndian(h[10..]), BinaryPrimitives.ReadUInt16LittleEndian(h[12..]), BinaryPrimitives.ReadUInt16LittleEndian(h[14..]),
                BinaryPrimitives.ReadUInt32LittleEndian(h[16..]), BinaryPrimitives.ReadUInt32LittleEndian(h[20..]), BinaryPrimitives.ReadUInt32LittleEndian(h[24..]),
                BinaryPrimitives.ReadUInt32LittleEndian(h[42..]));
            // Sizes and offsets of ZIP64 entries stand in an extra field; encrypted entries are not copied.
            if (record.Compressed == uint.MaxValue || record.Size == uint.MaxValue || record.Offset == uint.MaxValue || (record.Flags & 1) != 0)
                return null;
            records.Add(record);
            p += 46 + nameLength + extraLength + commentLength;
        }
        return records;
    }

    static byte[] Read(Stream zip, long at, int count)
    {
        var bytes = new byte[count];
        zip.Position = at;
        zip.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>Writes a ZIP file entry by entry, then its central directory (Finish).</summary>
    public sealed class Writer(Stream output)
    {
        readonly List<ZipRecord> written = [];
        long position;

        /// <summary>
        /// An entry with its data as <paramref name="zip"/> stores it, not unpacked: a local header of its own (without
        /// a data descriptor, the sizes known), then the stored bytes found after the source's local header.
        /// </summary>
        public void Copy(Stream zip, ZipRecord record)
        {
            var local = Read(zip, record.Offset, 30);
            if (BinaryPrimitives.ReadUInt32LittleEndian(local) != LocalHeader)
                throw new InvalidDataException("A ZIP entry's local header is missing.");
            zip.Position = record.Offset + 30L + BinaryPrimitives.ReadUInt16LittleEndian(local.AsSpan(26)) + BinaryPrimitives.ReadUInt16LittleEndian(local.AsSpan(28));
            Header(record with { Flags = (ushort)(record.Flags & ~8) });
            var buffer = new byte[81920];
            for (var left = (long)record.Compressed; left > 0;)
            {
                var n = zip.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
                if (n == 0)
                    throw new EndOfStreamException("A ZIP entry's data ends early.");
                output.Write(buffer, 0, n);
                left -= n;
            }
            position += record.Compressed;
        }

        /// <summary>An entry of <paramref name="bytes"/>, deflated (stored where deflating does not make them smaller).</summary>
        public void Add(byte[] name, ushort flags, ushort time, ushort date, byte[] bytes)
        {
            using var packed = new MemoryStream();
            using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
                deflate.Write(bytes);
            var deflated = packed.Length < bytes.Length;
            var data = deflated ? packed.GetBuffer().AsSpan(0, (int)packed.Length) : bytes;
            Header(new ZipRecord(name, (ushort)(flags & ~8), (ushort)(deflated ? 8 : 0), time, date, Crc32(bytes), (uint)data.Length, (uint)bytes.Length, 0));
            output.Write(data);
            position += data.Length;
        }

        void Header(ZipRecord record)
        {
            Span<byte> h = stackalloc byte[30];
            BinaryPrimitives.WriteUInt32LittleEndian(h, LocalHeader);
            Fields(h[4..], record);
            output.Write(h);
            output.Write(record.Name);
            written.Add(record with { Offset = (uint)position });
            position += 30 + record.Name.Length;
        }

        /// <summary>The fields a local header and a central directory header share, from "version needed" on.</summary>
        static void Fields(Span<byte> h, ZipRecord record)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(h, 20);
            BinaryPrimitives.WriteUInt16LittleEndian(h[2..], record.Flags);
            BinaryPrimitives.WriteUInt16LittleEndian(h[4..], record.Method);
            BinaryPrimitives.WriteUInt16LittleEndian(h[6..], record.Time);
            BinaryPrimitives.WriteUInt16LittleEndian(h[8..], record.Date);
            BinaryPrimitives.WriteUInt32LittleEndian(h[10..], record.Crc);
            BinaryPrimitives.WriteUInt32LittleEndian(h[14..], record.Compressed);
            BinaryPrimitives.WriteUInt32LittleEndian(h[18..], record.Size);
            BinaryPrimitives.WriteUInt16LittleEndian(h[22..], (ushort)record.Name.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(h[24..], 0);
        }

        /// <summary>The central directory of the entries written and its end record.</summary>
        public void Finish()
        {
            var start = position;
            Span<byte> h = stackalloc byte[46];
            foreach (var record in written)
            {
                h.Clear();
                BinaryPrimitives.WriteUInt32LittleEndian(h, DirectoryHeader);
                BinaryPrimitives.WriteUInt16LittleEndian(h[4..], 20);
                Fields(h[6..], record);
                BinaryPrimitives.WriteUInt32LittleEndian(h[42..], record.Offset);
                output.Write(h);
                output.Write(record.Name);
                position += 46 + record.Name.Length;
            }
            Span<byte> end = stackalloc byte[22];
            end.Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(end, DirectoryEnd);
            BinaryPrimitives.WriteUInt16LittleEndian(end[8..], (ushort)written.Count);
            BinaryPrimitives.WriteUInt16LittleEndian(end[10..], (ushort)written.Count);
            BinaryPrimitives.WriteUInt32LittleEndian(end[12..], (uint)(position - start));
            BinaryPrimitives.WriteUInt32LittleEndian(end[16..], (uint)start);
            output.Write(end);
        }
    }

    /// <summary>An entry name's bytes and flags: UTF-8, flagged as such (bit 11) where it is not ASCII.</summary>
    public static (byte[] Name, ushort Flags) Name(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name);
        return (bytes, (ushort)(bytes.Length == name.Length ? 0 : 0x800));
    }

    /// <summary>A time as ZIP keeps it, in MS-DOS's format (2-second steps, 1980 to 2107).</summary>
    public static (ushort Time, ushort Date) DosTime(DateTime at)
    {
        if (at.Year is < 1980 or > 2107)
            at = new DateTime(1980, 1, 1);
        return ((ushort)(at.Hour << 11 | at.Minute << 5 | at.Second / 2), (ushort)((at.Year - 1980) << 9 | at.Month << 5 | at.Day));
    }

    static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++)
            c = (c & 1) != 0 ? 0xEDB88320 ^ c >> 1 : c >> 1;
        return c;
    }).ToArray();

    /// <summary>The CRC-32 ZIP checks entries with (ISO 3309, the polynomial 0x04C11DB7 reflected).</summary>
    static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var c = uint.MaxValue;
        foreach (var b in bytes)
            c = CrcTable[(c ^ b) & 0xFF] ^ c >> 8;
        return ~c;
    }
}
