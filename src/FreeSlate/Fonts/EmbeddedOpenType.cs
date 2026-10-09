using System.Buffers.Binary;
using System.Text;

namespace FreeSlate.Fonts;

/// <summary>
/// Embedded OpenType ([EOT], W3C Member Submission of 5 March 2008), the format of the fonts PowerPoint embeds in a
/// presentation (ppt/fonts/*.fntdata, catalog: text.embeddedFonts): a little-endian header naming the font, then the
/// font data, XORed with 0x50 when flagged and compressed with MicroType Express when flagged. PowerPoint writes
/// version 0x00020002 with the subset and compressed flags and without XOR (measured).
/// </summary>
public static class EmbeddedOpenType
{
    const uint Compressed = 0x4, XorEncrypted = 0x10000000;
    const int NamesStart = 82;

    /// <summary>The TrueType font an EOT file holds and the family name its header gives.</summary>
    /// <exception cref="InvalidDataException">The data is no EOT font or is damaged.</exception>
    public static (string Family, byte[] Font) Read(byte[] eot)
    {
        try
        {
            if (eot.Length < NamesStart || BinaryPrimitives.ReadUInt16LittleEndian(eot.AsSpan(34)) != 0x504C)
                throw new InvalidDataException("Not an Embedded OpenType font (no magic number).");
            var dataSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(eot.AsSpan(4));
            var version = BinaryPrimitives.ReadUInt32LittleEndian(eot.AsSpan(8));
            var flags = BinaryPrimitives.ReadUInt32LittleEndian(eot.AsSpan(12));

            // Family, style, version and full name, each a UTF-16 string after its size, with padding between them.
            var position = NamesStart;
            var family = Encoding.Unicode.GetString(eot.AsSpan(position + 2, Size16(eot, position))).TrimEnd('\0');
            position += 2 + Size16(eot, position);
            for (var name = 0; name < 3; name++)
            {
                position += 2;
                position += 2 + Size16(eot, position);
            }
            if (version >= 0x00020001)
            {
                position += 2;
                position += 2 + Size16(eot, position);   // the root string (the URLs that may use the font)
            }
            if (version >= 0x00020002)
            {
                position += 4 + 4 + 2;   // root string checksum, EUDC code page, padding
                position += 2 + Size16(eot, position);   // signature
                position += 4;   // EUDC flags
                position += 4 + (int)BinaryPrimitives.ReadUInt32LittleEndian(eot.AsSpan(position));   // EUDC font
            }

            var data = eot.AsSpan(position, dataSize).ToArray();
            if ((flags & XorEncrypted) != 0)
            {
                for (var i = 0; i < data.Length; i++)
                    data[i] ^= 0x50;
            }
            return (family, (flags & Compressed) != 0 ? MicroTypeExpress.Decompress(data) : data);
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException)
        {
            throw new InvalidDataException("Damaged Embedded OpenType font.", e);
        }
    }

    static int Size16(byte[] eot, int position) => BinaryPrimitives.ReadUInt16LittleEndian(eot.AsSpan(position));
}
