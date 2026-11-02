using Avalonia.Controls;

namespace FreeSlate.Editor.Controls;

/// <summary>The words beneath a window button, which the title bar places and shows. It fades by its theme, with the class "shown".</summary>
public sealed class CaptionTip : Border
{
    readonly TextBlock text = new();

    public CaptionTip() => Child = text;

    public string? Text
    {
        get => text.Text;
        set => text.Text = value;
    }

    public bool IsShown
    {
        get => Classes.Contains("shown");
        set => Classes.Set("shown", value);
    }
}
