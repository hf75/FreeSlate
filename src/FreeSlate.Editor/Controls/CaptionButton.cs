using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace FreeSlate.Editor.Controls;

/// <summary>What a window button does itself, as far as the frame does not connect it by the name of its part.</summary>
public enum CaptionJob
{
    None,
    ToggleFullScreen,
    Close,
}

/// <summary>A button of the window's title bar. What it does comes from the window, by the name of its part in the frame's template, or from its job.</summary>
public sealed class CaptionButton : Button
{
    public static readonly StyledProperty<CaptionJob> JobProperty =
        AvaloniaProperty.Register<CaptionButton, CaptionJob>(nameof(Job));

    public static readonly StyledProperty<CaptionSymbol> SymbolProperty =
        AvaloniaProperty.Register<CaptionButton, CaptionSymbol>(nameof(Symbol));

    public static readonly StyledProperty<string?> TipProperty =
        AvaloniaProperty.Register<CaptionButton, string?>(nameof(Tip));

    public static readonly StyledProperty<bool> ShowsTipProperty =
        AvaloniaProperty.Register<CaptionButton, bool>(nameof(ShowsTip), true);

    public CaptionJob Job
    {
        get => GetValue(JobProperty);
        set => SetValue(JobProperty, value);
    }

    public CaptionSymbol Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>What the button does, in words: its tip, and its name for a screen reader.</summary>
    public string? Tip
    {
        get => GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    /// <summary>Whether the tip shows beneath the button, or only a screen reader gets it.</summary>
    public bool ShowsTip
    {
        get => GetValue(ShowsTipProperty);
        set => SetValue(ShowsTipProperty, value);
    }

    CaptionBar? Bar => this.FindAncestorOfType<CaptionBar>();

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Bar?.PointerOn(this);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Bar?.PointerOff();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Bar?.Pressed();
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (Job == CaptionJob.ToggleFullScreen)
            (Bar?.Window as EditorWindow)?.ToggleFullScreen();
        else if (Job == CaptionJob.Close)
            Bar?.Window?.Close();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    /// <summary>Avalonia names the buttons of a frame in English; a screen reader gets the tip, in the user's language.</summary>
    sealed class Peer(CaptionButton owner) : ButtonAutomationPeer(owner)
    {
        protected override string? GetNameCore() => ((CaptionButton)Owner).Tip ?? base.GetNameCore();
    }
}
