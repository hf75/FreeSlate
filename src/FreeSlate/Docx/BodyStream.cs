using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using FreeSlate.Opc;

namespace FreeSlate.Docx;

/// <summary>
/// The main part's XML read bit by bit (see DocumentContext.LoadBody), as OpcPackage.ReadXml reads it whole, with its
/// whitespace and where each element was read (line info, which PartText finds elements by): the root and what stands
/// before the body at once, the body's children as they are asked for, then what follows the body. Each element is
/// made ready as DocumentContext.Load makes a part's: Strict's names made transitional, then <c>prepare</c>. The
/// elements are read by XLinq's own reader (its constructor from an XmlReader and its line info, reached through
/// UnsafeAccessor), so that they come out as a whole part's do.
/// </summary>
sealed class BodyStream : IDisposable
{
    const LoadOptions Options = LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo;

    readonly XmlReader reader;
    readonly IXmlLineInfo info;
    readonly Action<XElement> prepare;
    readonly XElement root;
    readonly bool strict;

    /// <summary>The part as read so far, and its body.</summary>
    public XDocument Document { get; } = new();
    public XElement Body { get; }

    /// <summary>Whether the body's end was read, and what follows it.</summary>
    bool done;

    BodyStream(byte[] bytes, Action<XElement> prepare)
    {
        this.prepare = prepare;
        // As XDocument.Load reads (see OpcPackage.ReadXml): whitespace kept, a DTD parsed but nothing resolved.
        reader = XmlReader.Create(new MemoryStream(bytes, false),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, MaxCharactersFromEntities = 10_000_000, XmlResolver = null });
        info = (IXmlLineInfo)reader;
        reader.MoveToContent();
        root = Start();
        Document.Add(root);
        strict = Strict.IsStrict(root);
        if (strict)
            Strict.Transition(root);
        if (reader.IsEmptyElement)
            throw new InvalidDataException("The part's root holds no body.");
        reader.Read();
        while (!(reader.NodeType == XmlNodeType.Element && reader.Depth == 1 && reader.LocalName == "body"))
        {
            if (reader.NodeType == XmlNodeType.EndElement || reader.EOF)
                throw new InvalidDataException("The part's root holds no body.");
            Add(root, Node());
        }
        Body = Start();
        root.Add(Body);
        if (strict)
            Strict.Transition(Body);
        var empty = reader.IsEmptyElement;
        reader.Read();
        if (empty)
            Finish();
    }

    /// <summary>The part read up to its body's first child, or null where XLinq's own way of reading cannot be reached.</summary>
    /// <exception cref="InvalidDataException">The part has no body.</exception>
    public static BodyStream? Open(byte[] bytes, Action<XElement> prepare)
    {
        try
        {
            // XLinq's internals answer before the part is read: where they are gone, the part is read whole.
            using (var probe = XmlReader.Create(new StringReader("<x/>")))
            {
                probe.MoveToContent();
                SetLineInfo(ReadElement(probe, Options), 1, 1);
            }
            return new BodyStream(bytes, prepare);
        }
        catch (MissingMemberException)
        {
            return null;
        }
    }

    /// <summary>Reads so many more children of the body (elements, the whitespace between them going with them); whether any are left.</summary>
    public bool Read(int children)
    {
        for (var read = 0; read < children && !done;)
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.EndElement:
                    reader.Read();
                    Finish();
                    break;
                case XmlNodeType.None:
                    Finish();
                    break;
                case XmlNodeType.Element:
                    Add(Body, Node());
                    read++;
                    break;
                default:
                    Add(Body, Node());
                    break;
            }
        }
        return !done;
    }

    /// <summary>What follows the body in the root, read whole.</summary>
    void Finish()
    {
        while (reader.NodeType != XmlNodeType.EndElement && !reader.EOF)
            Add(root, Node());
        done = true;
    }

    /// <summary>A node added to a container, an element made ready (see prepare) once it stands there.</summary>
    void Add(XContainer container, XNode? node)
    {
        if (node == null)
            return;
        container.Add(node);
        if (node is not XElement element)
            return;
        if (strict)
            Strict.Transition(element);
        prepare(element);
    }

    /// <summary>The node the reader stands on, read whole (an element with where it and its own were read), the reader after it; null for one left out.</summary>
    XNode? Node()
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                return ReadElement(reader, Options);
            case XmlNodeType.Text or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
            {
                var text = new XText(reader.Value);
                reader.Read();
                return text;
            }
            case XmlNodeType.CDATA:
            {
                var data = new XCData(reader.Value);
                reader.Read();
                return data;
            }
            case XmlNodeType.Comment or XmlNodeType.ProcessingInstruction:
                return XNode.ReadFrom(reader);
            default:
                reader.Read();
                return null;
        }
    }

    /// <summary>The element whose start tag the reader stands on, with its attributes and where it was read, without its content.</summary>
    XElement Start()
    {
        var element = new XElement(XNamespace.Get(reader.NamespaceURI) + reader.LocalName);
        SetLineInfo(element, info.LineNumber, info.LinePosition);
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                var name = reader.NamespaceURI == XNamespace.Xmlns.NamespaceName
                    ? reader.Prefix.Length == 0 ? XName.Get("xmlns") : XNamespace.Xmlns + reader.LocalName
                    : XNamespace.Get(reader.NamespaceURI) + reader.LocalName;
                element.Add(new XAttribute(name, reader.Value));
            }
            while (reader.MoveToNextAttribute());
            reader.MoveToElement();
        }
        return element;
    }

    /// <summary>
    /// The properties of the first section break after the body's children read so far, read from the part's text as a
    /// copy (for the first pages, before the part is read to there; see ModelReader.BeginBody); null where none follows.
    /// </summary>
    public XElement? SectionAhead(PartText text)
    {
        var from = Body.LastNode is XElement last && text.Start(last) is var start and >= 0 ? text.End(start) : text.Start(Body);
        if (from < 0)
            return null;
        // The prefixes WordprocessingML has in the part (Strict's too), from the root's declarations.
        var declarations = root.Attributes().Where(a => a.IsNamespaceDeclaration).ToList();
        var found = -1;
        foreach (var declaration in declarations.Where(d => d.Value is "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                     or "http://purl.oclc.org/ooxml/wordprocessingml/main"))
        {
            var tag = declaration.Name.Namespace == XNamespace.None ? "<sectPr" : $"<{declaration.Name.LocalName}:sectPr";
            for (var at = text.Text.IndexOf(tag, from, StringComparison.Ordinal); at >= 0; at = text.Text.IndexOf(tag, at + 1, StringComparison.Ordinal))
            {
                if (at + tag.Length < text.Text.Length && text.Text[at + tag.Length] is ' ' or '\t' or '\r' or '\n' or '/' or '>')
                {
                    found = found < 0 ? at : Math.Min(found, at);
                    break;
                }
            }
        }
        if (found < 0)
            return null;
        try
        {
            var wrapped = XElement.Parse($"<x {string.Join(" ", declarations)}>{text.Text[found..text.End(found)]}</x>");
            var properties = wrapped.Elements().First();
            if (strict)
                Strict.Transition(properties);
            return properties;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    public void Dispose() => reader.Dispose();

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    static extern XElement ReadElement(XmlReader reader, LoadOptions options);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SetLineInfo")]
    static extern void SetLineInfo(XObject node, int lineNumber, int linePosition);
}
