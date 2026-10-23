using System.Runtime.InteropServices;

namespace FreeSlate.Text;

/// <summary>
/// Fonts cut down to the glyphs a document draws, by HarfBuzz's subsetter in the native library HarfBuzzSharp loads
/// (which HarfBuzzSharp itself does not wrap): every glyph keeps its id, those not drawn are left empty, the characters
/// given keep their mappings to the glyphs, and hinting and the font's names stay. A face of a collection (.ttc) comes
/// out as a font of its own.
/// </summary>
public static class FontSubset
{
    const string Library = "libHarfBuzzSharp";

    /// <summary>HB_SUBSET_FLAGS_RETAIN_GIDS and HB_SUBSET_FLAGS_NOTDEF_OUTLINE.</summary>
    const uint RetainGids = 0x2, NotdefOutline = 0x40;

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr hb_subset_input_create_or_fail();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_subset_input_destroy(IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr hb_subset_input_glyph_set(IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr hb_subset_input_unicode_set(IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_subset_input_set_flags(IntPtr input, uint flags);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr hb_subset_or_fail(IntPtr face, IntPtr input);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_set_add(IntPtr set, uint codepoint);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr hb_face_reference_blob(IntPtr face);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_face_destroy(IntPtr face);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr hb_blob_get_data(IntPtr blob, out uint length);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_blob_destroy(IntPtr blob);

    /// <summary>
    /// The font of <paramref name="data"/> (face <paramref name="index"/> of a collection) with only
    /// <paramref name="glyphs"/> (and .notdef), the <paramref name="characters"/> mapped to theirs; null where HarfBuzz
    /// cannot cut it down (a format it does not subset).
    /// </summary>
    public static byte[]? Subset(byte[] data, int index, IEnumerable<uint> glyphs, IEnumerable<uint> characters)
    {
        // In native memory of its own, which the blob frees with itself (see FontData.NativeBlob).
        var memory = Marshal.AllocHGlobal(Math.Max(1, data.Length));
        Marshal.Copy(data, 0, memory, data.Length);
        using var blob = new HarfBuzzSharp.Blob(memory, data.Length, HarfBuzzSharp.MemoryMode.ReadOnly, () => Marshal.FreeHGlobal(memory));
        using var face = new HarfBuzzSharp.Face(blob, index);
        var input = hb_subset_input_create_or_fail();
        if (input == IntPtr.Zero)
            return null;
        try
        {
            var glyphSet = hb_subset_input_glyph_set(input);
            hb_set_add(glyphSet, 0);
            foreach (var glyph in glyphs)
                hb_set_add(glyphSet, glyph);
            var characterSet = hb_subset_input_unicode_set(input);
            foreach (var character in characters)
                hb_set_add(characterSet, character);
            hb_subset_input_set_flags(input, RetainGids | NotdefOutline);
            var subset = hb_subset_or_fail(face.Handle, input);
            if (subset == IntPtr.Zero)
                return null;
            try
            {
                var result = hb_face_reference_blob(subset);
                try
                {
                    var bytes = hb_blob_get_data(result, out var length);
                    if (bytes == IntPtr.Zero || length == 0)
                        return null;
                    var font = new byte[length];
                    Marshal.Copy(bytes, font, 0, (int)length);
                    return font;
                }
                finally
                {
                    hb_blob_destroy(result);
                }
            }
            finally
            {
                hb_face_destroy(subset);
            }
        }
        finally
        {
            hb_subset_input_destroy(input);
        }
    }
}
