using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace FreeSlate.Editor.Controls;

/// <summary>What a window button shows.</summary>
public enum CaptionSymbol
{
    Minimize,
    Maximize,
    Restore,
    Close,
    FullScreen,
    ExitFullScreen,
}

/// <summary>
/// The symbol of a window button, drawn for the pixels of its screen: at any scaling its lines are whole device pixels wide and lie
/// exactly on them, and the diagonals of the close symbol run through pixel centres, so that nothing blurs.
/// </summary>
public sealed class CaptionGlyph : Control
{
    public static readonly StyledProperty<CaptionSymbol> SymbolProperty =
        AvaloniaProperty.Register<CaptionGlyph, CaptionSymbol>(nameof(Symbol));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<CaptionGlyph>();

    static CaptionGlyph() => AffectsRender<CaptionGlyph>(SymbolProperty, ForegroundProperty);

    public CaptionSymbol Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>A new measure can mean a new scaling, for which the pixels lie elsewhere.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        InvalidateVisual();
        return base.MeasureOverride(availableSize);
    }

    public override void Render(DrawingContext context) =>
        Draw(context, Symbol, Foreground, Bounds.Width, LayoutHelper.GetLayoutScale(this));

    /// <summary>Draws the symbol in a square of the given side at the given scaling, all measures counted in device pixels.</summary>
    public static void Draw(DrawingContext context, CaptionSymbol symbol, IBrush? brush, double side, double scale)
    {
        var pixel = 1 / scale;
        var size = (int)Math.Round(side * scale);
        var stroke = Math.Max(1, (int)Math.Floor(scale + 0.1));
        var half = stroke / 2.0;
        var pen = new Pen(brush, stroke * pixel);
        var corner = Math.Round(scale) * pixel;

        switch (symbol)
        {
            case CaptionSymbol.Minimize:
                context.FillRectangle(brush!, new Rect(0, (size - stroke) / 2 * pixel, size * pixel, stroke * pixel));
                break;
            case CaptionSymbol.Maximize:
                context.DrawRectangle(null, pen, new Rect(half * pixel, half * pixel, (size - stroke) * pixel, (size - stroke) * pixel), corner, corner);
                break;
            case CaptionSymbol.Restore:
                var front = (int)Math.Round(size * 0.8);
                var offset = size - front;
                context.DrawRectangle(null, pen, new Rect(half * pixel, (offset + half) * pixel, (front - stroke) * pixel, (front - stroke) * pixel), corner, corner);
                var back = new StreamGeometry();
                using (var line = back.Open())
                {
                    line.BeginFigure(new Point(offset * pixel, half * pixel), false);
                    line.LineTo(new Point((size - half) * pixel - corner, half * pixel));
                    line.ArcTo(new Point((size - half) * pixel, half * pixel + corner), new Size(corner, corner), 0, false, SweepDirection.Clockwise);
                    line.LineTo(new Point((size - half) * pixel, front * pixel));
                    line.EndFigure(false);
                }
                context.DrawGeometry(null, pen, back);
                break;
            case CaptionSymbol.Close:
                var near = half * pixel;
                var far = (size - half) * pixel;
                context.DrawLine(pen, new Point(near, near), new Point(far, far));
                context.DrawLine(pen, new Point(far, near), new Point(near, far));
                break;
            case CaptionSymbol.FullScreen:
            case CaptionSymbol.ExitFullScreen:
                // Four corners: at the square's corners and open inward to go to full screen, around its middle and open outward to leave it.
                var inward = symbol == CaptionSymbol.ExitFullScreen;
                var arm = Math.Max(2, (int)Math.Round(size * 0.3));
                var round = Math.Round(scale);
                var corners = new StreamGeometry();
                using (var line = corners.Open())
                    foreach (var (right, down) in new[] { (false, false), (true, false), (true, true), (false, true) })
                    {
                        var x = (right ? size - half : half) + (inward ? (right ? -arm : arm) : 0);
                        var y = (down ? size - half : half) + (inward ? (down ? -arm : arm) : 0);
                        var toX = right == inward ? 1 : -1;
                        var toY = down == inward ? 1 : -1;
                        line.BeginFigure(new Point((x + toX * arm) * pixel, y * pixel), false);
                        line.LineTo(new Point((x + toX * round) * pixel, y * pixel));
                        line.ArcTo(new Point(x * pixel, (y + toY * round) * pixel), new Size(corner, corner), 0, false,
                            toX * toY > 0 ? SweepDirection.CounterClockwise : SweepDirection.Clockwise);
                        line.LineTo(new Point(x * pixel, (y + toY * arm) * pixel));
                        line.EndFigure(false);
                    }
                context.DrawGeometry(null, pen, corners);
                break;
        }
    }
}
