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
        Pad.Closed += () => Sheets.Focus();
        Sheets.AddHandler(PointerPressedEvent, (_, _) =>
        {
            Card.Close(kept: true);
            Strip.Close();
            Pad.Close();
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
        Controls.GestureRing.Slot Field(string nameKey, string sign, Action change, bool slanted = false, string? icon = null, Avalonia.Media.Color? swatch = null)
        {
            var slot = new Controls.GestureRing.Slot(nameKey, sign, () => Give(change), Slanted: slanted, Icon: icon, Swatch: swatch);
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
        // A new table, in the tone's look, its size stepped in a split ring: its columns on the right, one more up and one fewer down, its
        // rows on the left alike. It shows at the cursor as it would go in from the moment that ring comes, growing and shrinking with
        // each step; the way back in the ring's middle is split as the ring is: on its left (a tick) the table goes in, on its right (a
        // cross) it goes away, either back at the ring before. Enter puts it in and closes the ring; a click beside the ring, Escape and
        // Backspace take it away. Chosen blindly, a step puts in a table of the size it makes.
        var size = (Columns: Tables.Columns, Rows: Tables.Rows);
        var tableShown = false;
        Controls.GestureRing.Slot? newTable = null;
        // The table as large as the steps have it, a step of its own in place of the one that showed it before (none where it was refused).
        void ShowTable() => Sheets.Edit(() =>
        {
            if (tableShown)
                editing.TakeBack();
            tableShown = false;
            var steps = editing.Steps;
            editing.InsertTable(size.Columns, size.Rows, Tables.Table.Style, Tables.Text.Style);
            tableShown = editing.Steps > steps;
        });
        void Step(int columns, int rows)
        {
            size = (Math.Clamp(size.Columns + columns, 1, Tables.MostColumns), Math.Clamp(size.Rows + rows, 1, Tables.MostRows));
            if (ReferenceEquals(ring.Holder, newTable))
            {
                ShowTable();
                return;
            }
            Sheets.Edit(() => editing.InsertTable(size.Columns, size.Rows, Tables.Table.Style, Tables.Text.Style));
            size = (Tables.Columns, Tables.Rows);
        }
        string Counted(string one, string more, int count) =>
            string.Format(System.Globalization.CultureInfo.CurrentCulture, this.TryFindResource(count == 1 ? one : more, out var text) && text is string format ? format : "{0}", count);
        IReadOnlyList<Controls.GestureRing.Slot> tableSize =
        [
            new("TableColumnMore", "", () => Step(1, 0), Icon: Placement.ColumnMoreIcon, Steps: true),
            new("TableColumnFewer", "", () => Step(-1, 0), Icon: Placement.ColumnFewerIcon, Steps: true),
            new("TableRowFewer", "", () => Step(0, -1), Icon: Placement.RowFewerIcon, Steps: true),
            new("TableRowMore", "", () => Step(0, 1), Icon: Placement.RowMoreIcon, Steps: true),
        ];
        newTable = new("InsertTable", "", Under: tableSize, Icon: Placement.TableIcon, Split: true, Settles: true,
            Opened: () =>
            {
                (size, tableShown) = ((Tables.Columns, Tables.Rows), false);
                ShowTable();
            },
            Left: kept =>
            {
                if (!kept && tableShown)
                    Sheets.Edit(() => editing.TakeBack());
                tableShown = false;
            },
            Status: () => Counted("TableSizeColumn", "TableSizeColumns", size.Columns) + " \u00B7 " + Counted("TableSizeRow", "TableSizeRows", size.Rows));
        // At a spot with nothing to act on (no picture, selection or word there): what goes in there, the cursor there; a picture from a
        // file up, a table down right, a picture from the clipboard down left.
        IReadOnlyList<Controls.GestureRing.Slot> insert =
        [
            new("InsertPictureFile", "", () => _ = Sheets.PickPictures(), Icon: Placement.PictureIcon),
            newTable,
            new("InsertPictureClipboard", "", () => _ = Sheets.PasteClipboardPictures(), Icon: Placement.ClipboardIcon),
        ];
        // In a table's cell, at a spot with nothing to act on: its row up, its column right, shaping the table by hand down, the table
        // left; each a ring of its own laid out as the table is (above up, below down, beside left and right), each field a preview.
        IReadOnlyList<Controls.GestureRing.Slot> tableRow =
        [
            Field("TableRowAbove", "", () => editing.InsertRow(above: true), icon: Placement.RowAboveIcon),
            Field("TableRowMerge", "", () => editing.MergeRow(), icon: Placement.RowMergeIcon),
            Field("TableRowBelow", "", () => editing.InsertRow(above: false), icon: Placement.RowBelowIcon),
            Field("TableRowRemove", "\u00D7", () => editing.RemoveRow()),
        ];
        IReadOnlyList<Controls.GestureRing.Slot> tableSort =
        [
            Field("TableSortUp", "A\u2013Z", () => editing.SortRows(descending: false)),
            Field("TableSortDown", "Z\u2013A", () => editing.SortRows(descending: true)),
        ];
        IReadOnlyList<Controls.GestureRing.Slot> tableColumn =
        [
            new("TableSort", "A\u2013Z", Under: tableSort),
            Field("TableColumnRight", "", () => editing.InsertColumn(left: false), icon: Placement.ColumnRightIcon),
            Field("TableColumnRemove", "\u00D7", () => editing.RemoveColumn()),
            Field("TableColumnLeft", "", () => editing.InsertColumn(left: true), icon: Placement.ColumnLeftIcon),
        ];
        // A table's look: the tone's own up, then the kinds round the ring (a grid, lines between the rows, a book's rules, a frame), its
        // head down; each a preview.
        Controls.GestureRing.Slot Look(TableKind kind) => Field("TableLook" + kind.Style[5..], "", () => editing.StyleTable(kind.Style), icon: kind.Icon);
        IReadOnlyList<Controls.GestureRing.Slot> TableLooks(bool head) =>
        [
            Field("TableLookTone", "", () => editing.StyleTable(Tables.Table.Style), icon: Placement.LookToneIcon),
            Look(Tables.Kinds[0]),
            Look(Tables.Kinds[1]),
            head ? Field("TableHeadRemove", "", () => editing.SetHead(false), icon: Placement.HeadRemoveIcon)
                : Field("TableHead", "", () => editing.SetHead(true), icon: Placement.HeadIcon),
            Look(Tables.Kinds[2]),
            Look(Tables.Kinds[3]),
        ];
        // Where the table stands: in the text between its edges, in the middle up, at the right down right, across the text again down, at the
        // left down left; or floating beside the text, which goes round it, at the right up right and at the left up left (see
        // EditSession.FloatTable; its handle then moves it anywhere); each a preview.
        IReadOnlyList<Controls.GestureRing.Slot> tableAlign =
        [
            Field("TableAlignCenter", "", () => editing.AlignTable("center"), icon: Placement.AlignCenterIcon),
            Field("TableFloatRight", "", () => editing.FloatTable("right"), icon: Placement.FloatRightIcon),
            Field("TableAlignRight", "", () => editing.AlignTable("right"), icon: Placement.AlignRightIcon),
            Field("TableWiden", "", () => editing.WidenTable(), icon: Placement.WidenIcon),
            Field("TableAlignLeft", "", () => editing.AlignTable(null), icon: Placement.AlignLeftIcon),
            Field("TableFloatLeft", "", () => editing.FloatTable(null), icon: Placement.FloatLeftIcon),
        ];
        IReadOnlyList<Controls.GestureRing.Slot> TableWhole(bool head) =>
        [
            Field("TableTranspose", "", () => editing.TransposeTable(), icon: Placement.TransposeIcon),
            Field("TableDistribute", "", () => editing.DistributeColumns(), icon: Placement.DistributeIcon),
            new("TableAlign", "", Under: tableAlign, Icon: Placement.AlignCenterIcon),
            new("TableLook", "", Under: TableLooks(head), Icon: Placement.LookIcon),
            Field("TableRemove", "\u00D7", () => editing.RemoveTable()),
        ];
        // The colours of cells: none up, the colours round the ring as swatches; each a preview on the cells selected or the cursor's.
        IReadOnlyList<Controls.GestureRing.Slot> fills =
        [
            Field("FillNone", "", () => editing.ShadeCells(null), icon: Placement.FillNoneIcon),
            .. Tables.Fills.Select(f => Field(f.NameKey, "", () => editing.ShadeCells(f.Fill), swatch: f.Color)),
        ];
        // The lines of cells: none, fine, strong, double, dotted, dashed (see Tables.Lines), or as the table's look gives them; each a
        // preview on the lines named of the cells selected or of the cursor's.
        IReadOnlyList<Controls.GestureRing.Slot> CellLooks(CellLines which) =>
        [
            Field("LineHide", "", () => editing.SetCellLines(which, CellBorder.None), icon: Placement.LineHideIcon),
            .. Tables.Lines.Select(l => Field(l.NameKey, "", () => editing.SetCellLines(which, l.Border(Tones.Of(editing.Document))), icon: l.Icon)),
            Field("LineAsTable", "", () => editing.SetCellLines(which, null), icon: Placement.LookToneIcon),
        ];
        // Which lines of cells selected, each in its direction (the inside ones across down left, upright up left), then how they look.
        IReadOnlyList<Controls.GestureRing.Slot> cellsLines =
        [
            new("CellLinesTop", "", Under: CellLooks(CellLines.Top), Icon: Placement.CellsTopIcon),
            new("CellLinesOutside", "", Under: CellLooks(CellLines.Outside), Icon: Placement.CellsOutsideIcon),
            new("CellLinesRight", "", Under: CellLooks(CellLines.Right), Icon: Placement.CellsRightIcon),
            new("CellLinesAll", "", Under: CellLooks(CellLines.All), Icon: Placement.CellsAllIcon),
            new("CellLinesBottom", "", Under: CellLooks(CellLines.Bottom), Icon: Placement.CellsBottomIcon),
            new("CellLinesAcross", "", Under: CellLooks(CellLines.Across), Icon: Placement.CellsAcrossIcon),
            new("CellLinesLeft", "", Under: CellLooks(CellLines.Left), Icon: Placement.CellsLeftIcon),
            new("CellLinesUpright", "", Under: CellLooks(CellLines.Upright), Icon: Placement.CellsUprightIcon),
        ];
        // A cell split, laid out as the parts lie: in two rows up, in two columns right, in three rows down, in three columns left.
        IReadOnlyList<Controls.GestureRing.Slot> cellSplit =
        [
            Field("CellSplitRows2", "", () => editing.SplitCell(across: true, 2), icon: Placement.SplitRows2Icon),
            Field("CellSplitColumns2", "", () => editing.SplitCell(across: false, 2), icon: Placement.SplitColumns2Icon),
            Field("CellSplitRows3", "", () => editing.SplitCell(across: true, 3), icon: Placement.SplitRows3Icon),
            Field("CellSplitColumns3", "", () => editing.SplitCell(across: false, 3), icon: Placement.SplitColumns3Icon),
        ];
        // What goes into a cell: a picture from a file up, one from the clipboard down (a table in a table not).
        IReadOnlyList<Controls.GestureRing.Slot> cellInsert =
        [
            new("InsertPictureFile", "", () => _ = Sheets.PickPictures(), Icon: Placement.PictureIcon),
            new("InsertPictureClipboard", "", () => _ = Sheets.PasteClipboardPictures(), Icon: Placement.ClipboardIcon),
        ];
        // Shaping the table: by hand up (the pen and the eraser, see DocumentView.BeginShape), and the cell split evenly, in columns on the
        // right, in rows on the left.
        IReadOnlyList<Controls.GestureRing.Slot> shaping =
        [
            new("TableShapeByHand", "", () =>
            {
                if (editing.Table is { } table)
                    Sheets.BeginShape(table.Table);
            }, Icon: Placement.PenIcon),
            .. cellSplit.Where(s => s.NameKey.Contains("Columns", StringComparison.Ordinal)),
            .. cellSplit.Where(s => s.NameKey.Contains("Rows", StringComparison.Ordinal)).Reverse(),
        ];
        // Where the text stands in the cells, chosen on a pad under them (see PlaceText).
        Controls.GestureRing.Slot placing = new("CellPlace", "", PlaceText, Icon: Placement.CellPlaceFieldIcon);
        // In a table's cell, at a spot with nothing to act on: its row up, its column right, shaping the table down (by hand, or the cell
        // split), the table left (each laid out as the table is: above up, below down, beside left and right); between them its colour,
        // what goes in it, its lines (its frame's look) and where its text stands.
        var cellRings = new[] { false, true }.ToDictionary(head => head, head => (IReadOnlyList<Controls.GestureRing.Slot>)
        [
            new("TableRow", "", Under: tableRow, Icon: Placement.RowIcon),
            new("CellFill", "", Under: fills, Icon: Placement.FillIcon),
            new("TableColumn", "", Under: tableColumn, Icon: Placement.ColumnIcon),
            new("CellInsert", "", Under: cellInsert, Icon: Placement.CellInsertIcon),
            new("TableShape", "", Under: shaping, Icon: Placement.PenIcon),
            new("CellLines", "", Under: CellLooks(CellLines.Outside), Icon: Placement.CellLinesIcon),
            new("TableWhole", "", Under: TableWhole(head), Icon: Placement.TableIcon),
            placing,
        ]);
        // Cells selected together: their rows up and their columns right as for a cell, merging them down right, their text's meaning down,
        // their lines (which, then how) down left, the table left, and where their text stands up left; their colour up right. What they
        // hold Delete takes away, as in Word.
        var cellsRings = new[] { false, true }.ToDictionary(head => head, head => (IReadOnlyList<Controls.GestureRing.Slot>)
        [
            new("TableRow", "", Under: tableRow, Icon: Placement.RowIcon),
            new("CellFill", "", Under: fills, Icon: Placement.FillIcon),
            new("TableColumn", "", Under: tableColumn, Icon: Placement.ColumnIcon),
            Field("CellsMerge", "", () => editing.MergeCells(), icon: Placement.MergeIcon),
            new("CellsMeaning", "!", Under: ring.Slots),
            new("CellLines", "", Under: cellsLines, Icon: Placement.CellLinesIcon),
            new("TableWhole", "", Under: TableWhole(head), Icon: Placement.TableIcon),
            placing,
        ]);
        // A line of a table under the pointer (see DocumentView.TargetLineAt), how it looks: hidden up, fine, strong, double; down its
        // stretch under the pointer alone (the same in a ring of its own); dotted, dashed, and as the table's look gives it. The whole line
        // straight through the table, each a preview; a hidden one (a dashed guide) shows again as any of them.
        void Lined(bool piece, CellBorder? border)
        {
            if (Sheets.TargetedLine is { } t)
                editing.SetTableLine(t.Table, t.Upright, t.Line, piece ? t.From : t.WholeFrom, piece ? t.To : t.WholeTo, border);
        }
        IReadOnlyList<Controls.GestureRing.Slot> LineLooks(bool piece)
        {
            var lines = Tables.Lines.Select(l => Field(l.NameKey, "", () => Lined(piece, l.Border(Tones.Of(editing.Document))), icon: l.Icon)).ToList();
            return
            [
                Field("LineHide", "", () => Lined(piece, CellBorder.None), icon: Placement.LineHideIcon),
                .. lines.Take(3),
                .. piece ? [] : new[] { new Controls.GestureRing.Slot("LinePiece", "", Under: LineLooks(true), Icon: Placement.LinePieceIcon) },
                .. lines.Skip(3),
                Field("LineAsTable", "", () => Lined(piece, null), icon: Placement.LookToneIcon),
            ];
        }
        var lineRing = LineLooks(false);
        var (inserting, lined) = (false, false);
        ring.Choose = () =>
        {
            var (empty, line) = (inserting, lined);
            (inserting, lined) = (false, false);
            if (!line)
                Sheets.ForgetLine();
            else if (Sheets.TargetedLine != null)
                return lineRing;
            if (empty)
                return editing.Table is { } table ? cellRings[table.HeadRow || table.HeadColumn] : insert;
            if (editing.CellSelection != null && editing.Table is { } selected)
                return cellsRings[selected.HeadRow || selected.HeadColumn];
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
        ring.Selection = () =>
        {
            // A line's stretch under the pointer alone in its own ring, else the whole line.
            Sheets.TargetPiece = ring.Holder?.NameKey == "LinePiece";
            return Sheets.TranslatePoint(default, ring) is { } shift ? Sheets.SelectionAreas().Select(area => area.Translate((Vector)shift)).ToList() : [];
        };
        ring.Turned = () => Sheets.SelectionTurned() is var (frame, turn) && Sheets.TranslatePoint(default, ring) is { } moved
            ? (frame.Translate((Vector)moved), turn)
            : null;
        // A line of a table under the pointer first (they are thin), then a picture: it lies over the text, or is the text's; where
        // nothing is, what goes in there.
        ring.Target = point =>
        {
            var at = OnSheets(point);
            if (Sheets.TargetLineAt(at))
                return lined = true;
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

    /// <summary>
    /// Where the text stands in the cells acted on (those selected, else the cursor's) and the way it runs, chosen on a pad under them (see
    /// PlacePad): the choice the pointer or the keys are on showing in the cells (a step taken back as they go on), the one chosen kept as one
    /// step; a way kept, the pad shows the places running so.
    /// </summary>
    void PlaceText()
    {
        if (editing.CellPlace is not { } place || Sheets.CellsActedArea() is not { } box)
            return;
        var way = editing.CellDirection ?? "across";
        // The choice shown in the cells for a preview, a step of its own; null where none is. The cells' own place and way show no preview.
        Controls.PlacePad.Choice? previewed = null;
        void Show(Controls.PlacePad.Choice? choice)
        {
            if (choice == previewed)
                return;
            if (previewed != null)
                Sheets.Edit(() => editing.TakeBack());
            previewed = null;
            if (choice is not { } shown || shown.Place == place || shown.Way == way)
                return;
            var before = editing.Document.Changes;
            Sheets.Edit(() =>
            {
                if (shown.Place is var (vertical, horizontal))
                    editing.PlaceText(vertical, horizontal);
                else
                    editing.TurnText(shown.Way == "across" ? null : shown.Way);
            });
            if (editing.Document.Changes != before)
                previewed = shown;
        }
        Pad.Open(box, place, way, Placement.CellPlaceIcon, Placement.CellWayIcon, Show, choice =>
        {
            // The preview of the choice is its step; another one goes first.
            if (choice != previewed)
                Show(choice);
            previewed = null;
            if (choice.Way != null && editing.CellPlace is { } now)
            {
                (place, way) = (now, editing.CellDirection ?? "across");
                Pad.Turned(place, way);
            }
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
    /// right, Shift+Tab a level to the left (see Levels); in a table Tab goes to the next cell instead, Shift+Tab to the one before, as in
    /// Word (see EditSession.NextCell). Enter in an empty point or subpoint moves it a level to the left instead of making another one.
    /// Enter after a heading or a margin note makes text, as their styles say (w:next).
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
            if (editing.Table != null)
                Sheets.Edit(() => editing.NextCell(back: shift));
            else
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
