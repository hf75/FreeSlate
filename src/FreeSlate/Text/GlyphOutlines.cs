using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace FreeSlate.Text;

/// <summary>
/// Glyph outlines through HarfBuzz's draw API (hb_font_draw_glyph), which reads TrueType, CFF and CFF2 outlines
/// alike. HarfBuzzSharp does not wrap it, so the native functions are called directly.
/// </summary>
static class GlyphOutlines
{
    const string Library = "libHarfBuzzSharp";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void PointFunc(IntPtr funcs, IntPtr data, IntPtr state, float x, float y, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void QuadraticFunc(IntPtr funcs, IntPtr data, IntPtr state, float cx, float cy, float x, float y, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void CubicFunc(IntPtr funcs, IntPtr data, IntPtr state, float c1x, float c1y, float c2x, float c2y, float x, float y, IntPtr user);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void CloseFunc(IntPtr funcs, IntPtr data, IntPtr state, IntPtr user);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr hb_draw_funcs_create();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_draw_funcs_set_move_to_func(IntPtr funcs, PointFunc func, IntPtr user, IntPtr destroy);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_draw_funcs_set_line_to_func(IntPtr funcs, PointFunc func, IntPtr user, IntPtr destroy);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_draw_funcs_set_quadratic_to_func(IntPtr funcs, QuadraticFunc func, IntPtr user, IntPtr destroy);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_draw_funcs_set_cubic_to_func(IntPtr funcs, CubicFunc func, IntPtr user, IntPtr destroy);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_draw_funcs_set_close_path_func(IntPtr funcs, CloseFunc func, IntPtr user, IntPtr destroy);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_draw_funcs_make_immutable(IntPtr funcs);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    static extern void hb_font_draw_glyph(IntPtr font, uint glyph, IntPtr funcs, IntPtr data);

    // The delegates must stay alive as long as the native draw functions may call them, i.e. forever.
    static readonly PointFunc MoveTo = (_, data, _, x, y, _) => Builder(data).Command('M', x, y);
    static readonly PointFunc LineTo = (_, data, _, x, y, _) => Builder(data).Command('L', x, y);
    static readonly QuadraticFunc QuadraticTo = (_, data, _, cx, cy, x, y, _) => Builder(data).Command('Q', cx, cy, x, y);
    static readonly CubicFunc CubicTo = (_, data, _, c1x, c1y, c2x, c2y, x, y, _) => Builder(data).Command('C', c1x, c1y, c2x, c2y, x, y);
    static readonly CloseFunc ClosePath = (_, data, _, _) => Builder(data).Close();

    static readonly Lazy<IntPtr> Funcs = new(() =>
    {
        var funcs = hb_draw_funcs_create();
        hb_draw_funcs_set_move_to_func(funcs, MoveTo, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_set_line_to_func(funcs, LineTo, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_set_quadratic_to_func(funcs, QuadraticTo, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_set_cubic_to_func(funcs, CubicTo, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_set_close_path_func(funcs, ClosePath, IntPtr.Zero, IntPtr.Zero);
        hb_draw_funcs_make_immutable(funcs);
        return funcs;
    });

    static PathBuilder Builder(IntPtr data) => (PathBuilder)GCHandle.FromIntPtr(data).Target!;

    /// <summary>The outline as SVG path data in font units with y pointing down (y negated); "" for empty glyphs.</summary>
    public static string Draw(HarfBuzzSharp.Font font, uint glyph)
    {
        var builder = new PathBuilder();
        var handle = GCHandle.Alloc(builder);
        try
        {
            hb_font_draw_glyph(font.Handle, glyph, Funcs.Value, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return builder.ToString();
    }

    sealed class PathBuilder
    {
        readonly StringBuilder sb = new();
        char last;

        public void Command(char command, params float[] values)
        {
            if (command != last || command == 'M')
                sb.Append(command);
            else
                sb.Append(' ');
            last = command;
            for (var i = 0; i < values.Length; i++)
            {
                var v = i % 2 == 1 ? -values[i] : values[i];
                if (i > 0 && v >= 0) sb.Append(' ');
                sb.Append(Format(v));
            }
        }

        public void Close()
        {
            sb.Append('Z');
            last = 'Z';
        }

        static string Format(double v)
        {
            var rounded = Math.Round(v, 1);
            return Math.Abs(rounded - Math.Round(rounded)) < 1e-9
                ? ((long)Math.Round(rounded)).ToString(CultureInfo.InvariantCulture)
                : rounded.ToString("0.#", CultureInfo.InvariantCulture);
        }

        public override string ToString() => sb.ToString();
    }
}
