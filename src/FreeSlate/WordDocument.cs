using System.Globalization;
using FreeSlate.Docx;
using FreeSlate.Docx.Layout;
using FreeSlate.Fonts;
using FreeSlate.Opc;
using FreeSlate.Rendering;

namespace FreeSlate;

/// <summary>
/// A Word document opened to change its text and to save it (catalog: package.opc): its paragraphs by the ids the
/// reader gives them (Block.Id, which the places of its pages name, see PlacesItem), text replaced at places of a
/// paragraph's text (Docx.ParagraphText), a paragraph split in two or taken away, its pages as it reads now, and the
/// document saved as DOCX: the paragraphs it changed written anew in the XML they were read from, every other byte of
/// the package as it was (see Docx.TextEdits). After each change the document is read again, so the ids of the
/// paragraphs after a split or a removed one move on. Changes reach the body's paragraphs, in tables and text boxes too.
/// </summary>
public sealed class WordDocument : IDisposable
{
    OpcPackage package;
    readonly string? path;
    readonly string? password;
    readonly FontLocator? fonts;
    readonly CultureInfo culture;
    readonly DateTime? printed;
    DocumentContext context = null!;
    ModelReader reader = null!;
    List<Block> blocks = [];
    readonly Dictionary<int, ParagraphBlock> paragraphs = [];
    List<PageScene>? scenes;

    WordDocument(OpcPackage package, string? path, string? password, FontLocator? fonts, CultureInfo culture, DateTime? printed)
    {
        (this.package, this.path, this.password, this.fonts, this.culture, this.printed) = (package, path, password, fonts, culture, printed);
        Read();
    }

    /// <summary>
    /// Opens a Word document: a password-protected one with its password (it is saved without it); its fields of the
    /// date print at <paramref name="printed"/> (now when null), its numbers and dates in <paramref name="culture"/>.
    /// </summary>
    /// <exception cref="PasswordException">The document needs a password, or the password is wrong.</exception>
    public static WordDocument Open(string path, string? password = null, FontLocator? fonts = null, CultureInfo? culture = null, DateTime? printed = null) =>
        new(OpcPackage.Open(path, password), Path.GetFullPath(path), password, fonts, culture ?? CultureInfo.CurrentCulture, printed);

    /// <summary>The body's blocks as the document reads now.</summary>
    public IReadOnlyList<Block> Blocks => blocks;

    /// <summary>A paragraph of the body (of its tables and text boxes too) by its id, or null.</summary>
    public ParagraphBlock? Paragraph(int id) => paragraphs.GetValueOrDefault(id);

    /// <summary>The text of a paragraph (see ParagraphText).</summary>
    public string Text(int paragraph) => ParagraphText.Of(Find(paragraph));

    /// <summary>
    /// Replaces the text of a paragraph from a place, so many characters, by other text (without breaks): it takes the
    /// format of the text before it.
    /// </summary>
    /// <exception cref="NotSupportedException">The range holds what cannot be changed so (see TextEdits); nothing changes.</exception>
    public void Replace(int paragraph, int offset, int length, string text)
    {
        var (part, bytes) = TextEdits.Replace(context, Find(paragraph), offset, length, text);
        Apply(part, bytes);
    }

    /// <summary>Puts text into a paragraph at a place (see Replace).</summary>
    public void Insert(int paragraph, int offset, string text) => Replace(paragraph, offset, 0, text);

    /// <summary>Takes so many characters of a paragraph's text away from a place (see Replace).</summary>
    public void Delete(int paragraph, int offset, int length) => Replace(paragraph, offset, length, "");

    /// <summary>
    /// Splits a paragraph in two at a place, as Enter does; returns the id of the second, or of the paragraph the reader
    /// joins it into (the first, where its mark is hidden).
    /// </summary>
    /// <exception cref="NotSupportedException">The place is within what cannot be split (a field, a content control); nothing changes.</exception>
    public int Split(int paragraph, int offset)
    {
        var (part, bytes, second) = TextEdits.Split(context, Find(paragraph), offset);
        Apply(part, bytes);
        var text = new PartText(bytes);
        bool Second(System.Xml.Linq.XElement? element) => element != null && context.PartOf(element) == part && text.Start(element) == second;
        // Ids up to the split paragraph stay: where nothing of the second half shows on its own, the first holds it.
        return (paragraphs.Values.FirstOrDefault(p => Second(p.Element))
                ?? paragraphs.Values.FirstOrDefault(p => p.Content.Any(i => Second(i.Xml?.Ancestors(p.Element.Name).FirstOrDefault()))))?.Id
               ?? paragraph;
    }

    /// <summary>Takes a paragraph away with all its content; the section it ends goes on into the next one.</summary>
    /// <exception cref="NotSupportedException">It is the last of its body, cell or text box; nothing changes.</exception>
    public void Remove(int paragraph)
    {
        var (part, bytes) = TextEdits.Remove(context, Find(paragraph));
        Apply(part, bytes);
    }

    /// <summary>The pages of the document as it reads now, laid out and drawn as DocxConverter does (see PageScene).</summary>
    public IReadOnlyList<PageScene> Scenes()
    {
        if (scenes != null)
            return scenes;
        var styles = new RunStyles(context);
        var layout = new ParagraphLayout(context, styles);
        // Notes are read after the body: their numbers come from the order of their references.
        var footnotes = reader.Notes(endnotes: false);
        var endnoteParts = reader.Notes(endnotes: true);
        var endnotes = endnoteParts.Where(n => n.Key is not ("separator" or "continuationSeparator" or "continuationNotice")).OrderBy(n => reader.EndnoteNumber(n.Key))
            .Where(n => reader.EndnoteNumber(n.Key) > 0).Select(n => (reader.EndnoteSection(n.Key), n.Value)).ToList();
        var paginator = new Paginator(context, layout, reader) { EndnoteContinuation = endnoteParts.GetValueOrDefault("continuationSeparator") };
        var pages = paginator.Paginate(blocks, reader.LastSection(), footnotes, endnotes, endnoteParts.GetValueOrDefault("separator"));
        var renderer = new PageRenderer(context, new Drawings(context, culture), new TableLayout(context, layout)) { Settings = paginator.Settings };
        var papers = new List<(double Width, double Height)>();
        scenes = [];
        foreach (var page in pages)
        {
            var paper = papers.FindIndex(p => Math.Abs(p.Width - page.PaperWidth) < 0.01 && Math.Abs(p.Height - page.PaperHeight) < 0.01);
            if (paper < 0)
            {
                papers.Add((page.PaperWidth, page.PaperHeight));
                paper = papers.Count - 1;
            }
            scenes.Add(renderer.Scene(page, $"paper{paper}"));
        }
        return scenes;
    }

    /// <summary>Writes the document as DOCX (see OpcPackage.Save).</summary>
    public void Save(Stream output) => package.Save(output);

    /// <summary>Saves the document as a DOCX file, over the one it was opened from too.</summary>
    public void Save(string target)
    {
        target = Path.GetFullPath(target);
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"~{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        using (var stream = File.Create(temporary))
            package.Save(stream);
        if (string.Equals(target, path, StringComparison.OrdinalIgnoreCase))
        {
            // The package reads from the file it replaces: it is opened again from the saved one.
            package.Dispose();
            File.Move(temporary, target, overwrite: true);
            package = OpcPackage.Open(target, password);
            Read();
        }
        else
            File.Move(temporary, target, overwrite: true);
    }

    public void Dispose() => package.Dispose();

    ParagraphBlock Find(int id) =>
        paragraphs.GetValueOrDefault(id) ?? throw new ArgumentException($"The document's body has no paragraph {id}.", nameof(id));

    void Apply(string part, byte[] bytes)
    {
        package.Replace(part, bytes);
        Read();
    }

    /// <summary>Reads the document as it is now: its body's blocks and its paragraphs by id.</summary>
    void Read()
    {
        context = new DocumentContext(package, fonts) { Culture = culture, Printed = printed };
        reader = new ModelReader(context);
        blocks = reader.Body();
        paragraphs.Clear();
        Collect(blocks);
        scenes = null;
    }

    void Collect(IEnumerable<Block> list)
    {
        foreach (var block in list)
        {
            if (block is ParagraphBlock paragraph)
            {
                paragraphs.TryAdd(paragraph.Id, paragraph);
                foreach (var inline in paragraph.Content)
                {
                    var (inner, children) = inline switch
                    {
                        DrawingInline drawing => (drawing.Text, drawing.ChildText),
                        AnchorInline anchor => (anchor.Text, anchor.ChildText),
                        _ => (null, null),
                    };
                    if (inner != null)
                        Collect(inner);
                    foreach (var (_, text) in children ?? [])
                        Collect(text);
                }
            }
            else if (block is TableBlock table)
            {
                foreach (var row in table.Table.Rows)
                    foreach (var cell in row.Cells)
                        Collect(cell.Blocks);
            }
        }
    }
}
