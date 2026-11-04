using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeSlate.Editing;
using FreeSlate.Editor.Localization;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor;

/// <summary>What the editor shows: the content of the window, or the whole page in a browser.</summary>
public sealed partial class EditorView : Panel
{
    readonly EditSession editing;
    readonly Dictionary<Tone, RadioButton> tones = [];

    /// <summary>Whether the tone's buttons are being set to the document's, not chosen.</summary>
    bool following;

    public EditorView()
    {
        InitializeComponent();

        foreach (var language in UiLanguages.All)
        {
            var choice = new RadioButton { Content = language.Name, GroupName = "language", IsChecked = language == UiLanguages.Current };
            choice.IsCheckedChanged += (_, _) =>
            {
                if (choice.IsChecked == true)
                {
                    UiLanguages.Choose(language);
                    Sheets.Focus();
                }
            };
            Languages.Children.Add(choice);
        }

        foreach (var tone in Tones.All)
        {
            var choice = new RadioButton { GroupName = "tone" };
            choice.Bind(ContentControl.ContentProperty, this.GetResourceObservable(tone.NameKey));
            choice.IsCheckedChanged += (_, _) =>
            {
                if (choice.IsChecked == true && !following)
                    Take(tone);
            };
            tones[tone] = choice;
            ToneChoice.Children.Add(choice);
        }
        ShowMeanings.IsCheckedChanged += (_, _) =>
        {
            Sheets.ShowsMeanings = ShowMeanings.IsChecked == true;
            Sheets.Focus();
        };
        Sheets.MeaningName = style => Meanings.Of(style) is { } meaning ? Meanings.Name(meaning) : null;
        Sheets.Changed += FollowTone;
        KeyDown += GiveMeaning;
        AddHandler(KeyDownEvent, Structure, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        Sheets.IsEmptyChanged += () => Classes.Set("written", !Sheets.IsEmpty);
        // Before the first frame, so that the sheet comes with the window; and the keys go to it from the start.
        var (styles, numbering) = Tones.All[0].Definitions();
        var document = Environment.GetCommandLineArgs().Contains("--sample")
            ? Sample.Document(Tones.All[0])
            : WordDocument.New("Inter", 10.5, EngineFonts.Locator, styles: styles, numbering: numbering, hyphenate: true);
        editing = new EditSession(document);
        Sheets.Open(editing);
        Lever.Attach(Sheets, editing);
        Join(Ring);
        // The card of a picture's description: a press on the sheets keeps what is written there; the strip of its looks closes as it was.
        // Closed, the keys go back to the sheets.
        Card.Closed += () => Sheets.Focus();
        Strip.Closed += () => Sheets.Focus();
        Sheets.AddHandler(PointerPressedEvent, (_, _) =>
        {
            Card.Close(kept: true);
            Strip.Close();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        FollowTone();
        Loaded += (_, _) =>
        {
            Sheets.Focus();
            // In a window: its bar of full screen, which the line above the sheets comes and goes with there.
            if (this.FindAncestorOfType<Window>()?.GetVisualDescendants().OfType<Controls.CaptionBar>().FirstOrDefault(b => b.ForFullScreen) is { } bar)
            {
                void Follow()
                {
                    Classes.Set("fullScreen", bar.IsVisible);
                    Classes.Set("revealed", bar.IsVisible && !bar.IsResting);
                }
                bar.Changed += Follow;
                Follow();
            }
        };
    }

    /// <summary>Where the pointer was last over the editor, in its own coordinates: where the ring comes for the keyboard.</summary>
    Avalonia.Point? pointer;

    /// <summary>
    /// The gesture ring's fields, sharing the circle from the top clockwise in an order that stays (the hand learns it): the meanings, and
    /// last, up left, taking the meaning away; on a picture, where it stands among the text (see Placement): in the line up, beside the
    /// text right and left, on its own down, in front of the text and behind it between them, a place on the page down right (a ring of
    /// the eight ways, top, top right …), and up left the picture itself (a ring: its caption, cropping it, its description, its colors,
    /// turning it (a ring: straight again, mirrored either way, a quarter turn either way, slanted, the size it took when it was put in,
    /// tilting it in space by hand), another picture in its frame, removing it, a hairline round it); on a picture cropped by hand, its
    /// whole, proportions and the circle; on one tilted by hand, Word's 3-D presets; at a spot with nothing to act on, what goes in there,
    /// the cursor there: a picture from a file up, one from the clipboard down. And its ways to the sheets. Space held on a selection
    /// brings it for the keyboard.
    /// </summary>
    void Join(Controls.GestureRing ring)
    {
        // The change a field pointed at made for a preview, as a step of its own; null where none did.
        Action? previewed = null;
        void Undo()
        {
            if (previewed != null)
                Sheets.Edit(() => editing.TakeBack());
            previewed = null;
            Sheets.SelectionHidden = false;
        }
        // A field does what its preview showed: that preview stays, the step it is; another one is taken back first.
        void Give(Action change)
        {
            if (previewed == change)
            {
                previewed = null;
                Sheets.SelectionHidden = false;
                return;
            }
            Undo();
            Sheets.Edit(change);
        }
        // The fields that show what they do while they are pointed at, and their changes.
        var previews = new Dictionary<Controls.GestureRing.Slot, Action>();
        Controls.GestureRing.Slot Field(string nameKey, string sign, Action change, bool slanted = false, string? icon = null)
        {
            var slot = new Controls.GestureRing.Slot(nameKey, sign, () => Give(change), Slanted: slanted, Icon: icon);
            previews[slot] = change;
            return slot;
        }
        Controls.GestureRing.Slot Meaning(string style, string sign, bool slanted = false) =>
            Field(Meanings.Of(style)!.NameKey, sign, () => editing.Format(new TextFormatting { Style = style }), slanted);
        Controls.GestureRing.Slot Placed(Placement placement) =>
            Field(placement.NameKey, "", () => editing.Format(placement.Format), icon: placement.Icon);
        ring.Slots =
        [
            Meaning("Important", "!"),
            Meaning("Emphasized", "i", slanted: true),
            Meaning("Term", "Aa"),
            Meaning("Quotation", "“"),
            Meaning("OpenQuestion", "?"),
            Field("RingRemove", "×", () => editing.Format(new TextFormatting { Style = "" })),
        ];
        // Turning the picture, down in the ring of the picture itself, each a preview: straight up (neither turned, mirrored nor tilted),
        // mirrored left for right up right, a quarter turn right on the right, slanted down right (Word's isometric view, the look of
        // shearing it), the size it took when it was put in down, mirrored top for bottom down left, a quarter turn left on the left, and
        // up left tilting it in space by hand (see DocumentView.BeginTilt).
        IReadOnlyList<Controls.GestureRing.Slot> turning =
        [
            Field("TurnStraight", "", () => editing.StraightenPicture(), icon: Placement.StraightIcon),
            Field("FlipAcross", "", () => editing.FlipPicture(across: true), icon: Placement.FlipAcrossIcon),
            Field("TurnRight", "", () => editing.TurnPicture(90), icon: Placement.TurnRightIcon),
            Field("TurnAskew", "", () => editing.TiltPicture(new PictureTilt("isometricRightUp")), icon: Placement.AskewIcon),
            Field("PictureNaturalSize", "", () => editing.RestoreSize(), icon: Placement.NaturalSizeIcon),
            Field("FlipDown", "", () => editing.FlipPicture(across: false), icon: Placement.FlipDownIcon),
            Field("TurnLeft", "", () => editing.TurnPicture(-90), icon: Placement.TurnLeftIcon),
            new("TiltByHand", "", () => Sheets.BeginTilt(), Icon: Placement.TiltByHandIcon),
        ];
        // What is done with the picture itself, up left: a caption below it, the cursor then in it (or taking it away, a preview as the
        // others), cropping it by hand, its description (a card under it), its colors (a strip under it), turning it (a ring of its own),
        // another picture in its frame, removing it, a hairline round it (or taking it away).
        IReadOnlyList<Controls.GestureRing.Slot> Itself(bool captioned, bool framed) =>
        [
            captioned
                ? Field("PictureCaptionRemove", "", () => editing.RemoveCaption(), icon: Placement.CaptionRemoveIcon)
                : new("PictureCaption", "", () => Sheets.Edit(() => editing.AddCaption()), Icon: Placement.CaptionIcon),
            new("PictureCrop", "", () => Sheets.BeginCrop(), Icon: Placement.CropIcon),
            new("PictureDescribe", "", Describe, Icon: Placement.DescriptionIcon),
            new("PictureRecolor", "", Recolor, Icon: Placement.RecolorIcon),
            new("PictureTurn", "", Under: turning, Icon: Placement.TurnIcon),
            new("PictureReplace", "", () => _ = Sheets.ReplacePicture(), Icon: Placement.ReplaceIcon),
            new("PictureRemove", "×", () => Sheets.Edit(() => editing.Delete())),
            framed
                ? Field("PictureFrameRemove", "", () => editing.Format(new DrawingFormatting { Outline = 0 }), icon: Placement.FrameRemoveIcon)
                : Field("PictureFrame", "", () => editing.Format(new DrawingFormatting { Outline = Placement.FrameWidth, OutlineColor = Placement.FrameColor }), icon: Placement.FrameIcon),
        ];
        IReadOnlyList<Controls.GestureRing.Slot> Picture(bool captioned, bool framed) =>
        [
            Placed(Placement.Inline),
            Placed(Placement.Front),
            Placed(Placement.Right),
            new("PicturePlaces", "", Under: [.. Placement.Places.Select(Placed)], Icon: Placement.PlacesIcon),
            Placed(Placement.Own),
            Placed(Placement.Behind),
            Placed(Placement.Left),
            new("PictureMore", "", Under: Itself(captioned, framed), Icon: Placement.PictureIcon),
        ];
        var pictureRings = new[] { false, true }.SelectMany(captioned => new[] { false, true }.Select(framed => (captioned, framed)))
            .ToDictionary(k => k, k => Picture(k.captioned, k.framed));
        // While a picture is cropped by hand: the whole picture up, the proportions round the ring (turned as the picture stands, upright
        // or lying), and up left the circle, or back to the rectangle.
        IReadOnlyList<Controls.GestureRing.Slot> Cropped(bool upright, bool round) =>
        [
            Field("CropWhole", "", () => editing.CropPicture(0.0), icon: Placement.PictureIcon),
            Field("CropSquare", "1:1", () => editing.CropPicture(1.0)),
            upright ? Field("CropThreeFour", "3:4", () => editing.CropPicture(3 / 4.0)) : Field("CropFourThree", "4:3", () => editing.CropPicture(4 / 3.0)),
            upright ? Field("CropTwoThree", "2:3", () => editing.CropPicture(2 / 3.0)) : Field("CropThreeTwo", "3:2", () => editing.CropPicture(3 / 2.0)),
            upright ? Field("CropTall", "9:16", () => editing.CropPicture(9 / 16.0)) : Field("CropWide", "16:9", () => editing.CropPicture(16 / 9.0)),
            round
                ? Field("CropRectangle", "", () => editing.RoundPicture(false), icon: Placement.RectangleIcon)
                : Field("CropCircle", "", () => editing.RoundPicture(true), icon: Placement.CircleIcon),
        ];
        var cropRings = new[] { false, true }.SelectMany(upright => new[] { false, true }.Select(round => (upright, round)))
            .ToDictionary(k => k, k => Cropped(k.upright, k.round));
        // While a picture is tilted by hand: Word's 3-D Rotation presets, each in its direction as it shows the picture: from above up,
        // slanted up right, facing right on the right, slanted down right, from below down, lying down left, facing left on the left, flat
        // again up left.
        Controls.GestureRing.Slot Tilt(string nameKey, string camera) =>
            Field(nameKey, "", () => editing.TiltPicture(new PictureTilt(camera)), icon: Placement.TiltIcon(camera));
        IReadOnlyList<Controls.GestureRing.Slot> tilted =
        [
            Tilt("TiltAbove", "perspectiveAbove"),
            Tilt("TiltRising", "isometricRightUp"),
            Tilt("TiltRight", "perspectiveRight"),
            Tilt("TiltFalling", "isometricRightDown"),
            Tilt("TiltBelow", "perspectiveBelow"),
            Tilt("TiltLying", "perspectiveRelaxed"),
            Tilt("TiltLeft", "perspectiveLeft"),
            Field("TiltFlat", "", () => editing.TiltPicture(PictureTilt.Flat), icon: Placement.RectangleIcon),
        ];
        // At a spot with nothing to act on (no picture, selection or word there): what goes in there, the cursor there.
        IReadOnlyList<Controls.GestureRing.Slot> insert =
        [
            new("InsertPictureFile", "", () => _ = Sheets.PickPictures(), Icon: Placement.PictureIcon),
            new("InsertPictureClipboard", "", () => _ = Sheets.PasteClipboardPictures(), Icon: Placement.ClipboardIcon),
        ];
        var inserting = false;
        ring.Choose = () =>
        {
            var empty = inserting;
            inserting = false;
            if (empty)
                return insert;
            if (Sheets.Tilting)
                return tilted;
            if (Sheets.Cropping && editing.PictureShown is { Crop: { } crop, Width: { } width, Height: { } height })
                return cropRings[(width / crop.Wide < height / crop.High, editing.PictureShown.Shape == "ellipse")];
            return Sheets.DrawingSelected ? pictureRings[(editing.Caption != null, editing.PictureShown?.Outline > 0)] : null;
        };
        ring.Preview = slot =>
        {
            var change = slot != null && previews.TryGetValue(slot, out var given) ? given : null;
            if (change == previewed)
                return;
            Undo();
            if (change == null)
                return;
            // The text as it would read, without the selection over it; a change refused, or one that changes nothing, is no step.
            var before = editing.Document.Changes;
            Sheets.Edit(change);
            if (editing.Document.Changes == before)
                return;
            previewed = change;
            Sheets.SelectionHidden = true;
        };
        Avalonia.Point OnSheets(Avalonia.Point point) => ring.TranslatePoint(point, Sheets) ?? point;
        ring.Selection = () => Sheets.TranslatePoint(default, ring) is { } shift
            ? Sheets.SelectionAreas().Select(area => area.Translate((Vector)shift)).ToList()
            : [];
        // A picture under the pointer first: it lies over the text, or is the text's; where nothing is, what goes in there.
        ring.Target = point =>
        {
            var at = OnSheets(point);
            if (Sheets.SelectDrawingAt(at) || Sheets.OnSelection(at) || Sheets.SelectWordAt(at))
                return true;
            return inserting = Sheets.PlaceCursorAt(at);
        };
        ring.OnSelection = point => Sheets.OnSelection(OnSheets(point));
        ring.Press = (point, by, dragging) => Sheets.Press(OnSheets(point), by, KeyModifiers.None, dragging);
        ring.Attach(Sheets);
        AddHandler(PointerMovedEvent, (_, e) =>
        {
            pointer = e.GetPosition(ring);
            ring.Follow(pointer.Value);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerExitedEvent, (_, _) => pointer = null, Avalonia.Interactivity.RoutingStrategies.Direct, handledEventsToo: true);
        AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Space && ring.IsBusy && !ring.SpaceUp())
                Sheets.Edit(() => editing.Type(" "));
            else if (ring.KeyChanged(e.Key, down: false))
                e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        // While Space waits for the ring or brings it, it types nothing; let go early, it types its space then (see above).
        AddHandler(TextInputEvent, (_, e) =>
        {
            if (ring.IsBusy && e.Text == " ")
                e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// The description of the picture selected written on a card under it (see DescriptionCard): kept as one step where it changed;
    /// the keys go back to the sheets as the card closes.
    /// </summary>
    void Describe()
    {
        if (editing.Drawing is not { } drawn || Sheets.SelectionAreas() is not [var box])
            return;
        var (description, decorative) = (drawn.Description ?? "", drawn.Decorative == true);
        Card.Open(box, description, decorative, (text, isDecorative) =>
        {
            if (text != description || isDecorative != decorative)
                Sheets.Edit(() => editing.Format(new DrawingFormatting { Description = text, Decorative = isDecorative }));
        });
    }

    /// <summary>
    /// The colors of the picture selected chosen on a strip under it (see RecolorStrip): the picture small in each look, the one the pointer
    /// or the keys are on showing on the picture itself (a step taken back as they go on), the one chosen kept as one step.
    /// </summary>
    void Recolor()
    {
        if (editing.PictureShown is not { } shown || editing.PictureBytes is not { } bytes || Sheets.SelectionAreas() is not [var box])
            return;
        var current = shown.Recolor ?? "none";
        var part = shown.Crop ?? PictureCrop.None;
        var looks = Looks.All.Select(l => (l.Recolor, l.NameKey, Looks.Shown(bytes, part, l.Recolor, 168, 112))).ToList();
        // The look shown on the picture for a preview, a step of its own; null where none is.
        string? previewed = null;
        void Show(string? look)
        {
            if (look == previewed)
                return;
            if (previewed != null)
                Sheets.Edit(() => editing.TakeBack());
            previewed = null;
            if (look == null || look == current)
                return;
            var before = editing.Document.Changes;
            Sheets.Edit(() => editing.Format(new DrawingFormatting { Recolor = look }));
            if (editing.Document.Changes != before)
                previewed = look;
        }
        Strip.Open(box, looks, current, Show, look =>
        {
            // The preview of the look chosen is its step; another one goes first.
            if (look != previewed)
                Show(look);
            previewed = null;
        });
    }

    /// <summary>Gives the document a tone, one step to undo: what is in view fades into its new look.</summary>
    void Take(Tone tone)
    {
        if (Tones.Of(editing.Document) == tone)
            return;
        var (styles, numbering) = tone.Definitions();
        Sheets.Restyle(() => editing.Handle(() =>
        {
            editing.Document.DefineStyles(styles, numbering);
            return null;
        }));
        Sheets.Focus();
    }

    /// <summary>
    /// The structure by the keys, while the sheets have them: Tab moves the paragraphs of the selection, or the cursor's, a level to the
    /// right, Shift+Tab a level to the left (see Levels); Enter in an empty point or subpoint moves it a level to the left instead of
    /// making another one. Enter after a heading or a margin note makes text, as their styles say (w:next).
    /// </summary>
    void Structure(object? sender, KeyEventArgs e)
    {
        // The ring's keys first: the arrows and Escape while it is up, Space held on a selection.
        if (Ring.KeyChanged(e.Key, down: true))
        {
            e.Handled = true;
            return;
        }
        if (!Sheets.IsFocused)
            return;
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None && editing.Selecting)
        {
            var at = pointer ?? (Sheets.CaretBounds is { Height: > 0 } bar ? Sheets.TranslatePoint(bar.Center, Ring) : null) ?? new Avalonia.Point(Bounds.Width / 2, Bounds.Height / 2);
            if (Ring.SpaceDown(at))
                e.Handled = true;
            return;
        }
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var others = e.KeyModifiers & ~KeyModifiers.Shift;
        if (e.Key == Key.Tab && others == KeyModifiers.None)
        {
            e.Handled = true;
            Shift(shift ? -1 : 1);
        }
        else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && !editing.Selecting
            && editing.Document.Text(editing.Caret.Paragraph).Length == 0 && Levels.Of(editing.Document.ParagraphStyle(editing.Caret.Paragraph)).Step is 1 or 2)
        {
            e.Handled = true;
            Shift(-1);
        }
    }

    /// <summary>Moves the paragraphs of the selection, or the cursor's, so many levels, those that can go so far: one step to undo.</summary>
    void Shift(int steps)
    {
        var document = editing.Document;
        var (start, end) = editing.Selecting ? editing.Selection : (editing.Caret, editing.Caret);
        var moved = new List<(int Paragraph, Level To)>();
        for (int? at = start.Paragraph; at is { } paragraph; at = paragraph == end.Paragraph ? null : document.Next(paragraph))
        {
            if (Levels.Beside(Levels.Of(document.ParagraphStyle(paragraph)), steps) is { } to)
                moved.Add((paragraph, to));
        }
        if (moved.Count == 0)
            return;
        Sheets.Edit(() => editing.Handle(() =>
        {
            foreach (var (paragraph, to) in moved)
                document.Format(paragraph, new ParagraphFormatting { Style = to.Style });
            return null;
        }));
    }

    /// <summary>The tone's buttons show the document's tone, also after undoing or doing again.</summary>
    void FollowTone()
    {
        if (Tones.Of(editing.Document) is not { } tone || tones[tone].IsChecked == true)
            return;
        following = true;
        tones[tone].IsChecked = true;
        following = false;
    }

    /// <summary>
    /// Ctrl+1 to Ctrl+5 give the selection, or the word at the cursor, a meaning, Ctrl+0 takes it away: a way to give meanings until the
    /// gesture ring comes.
    /// </summary>
    void GiveMeaning(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0 || (e.KeyModifiers & (KeyModifiers.Shift | KeyModifiers.Alt)) != 0)
            return;
        var index = e.Key - Key.D0;
        if (e.Key is >= Key.NumPad0 and <= Key.NumPad9)
            index = e.Key - Key.NumPad0;
        if (index < 0 || index > Meanings.All.Count)
            return;
        e.Handled = true;
        var style = index == 0 ? "" : Meanings.All[index - 1].Style;
        Sheets.Edit(() => editing.Format(new TextFormatting { Style = style }));
    }
}
