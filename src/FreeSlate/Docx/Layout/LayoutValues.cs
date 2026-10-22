using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Linq;

namespace FreeSlate.Docx.Layout;

/// <summary>
/// Compares states of a layout value by value (see Paginator.SameFlow): the engine's own data by its public fields
/// and properties, what a layout makes anew each time included; blocks by their ids (Block.Id, which their copies keep)
/// and what they hold; XML by its content; fonts, packages and the layout's services by reference; closures not at all.
/// </summary>
static class LayoutValues
{
    static readonly ConcurrentDictionary<Type, (FieldInfo[] Fields, PropertyInfo[] Properties)> members = new();

    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> hold the same values.</summary>
    public static bool Same(object? a, object? b) => Same(a, b, new HashSet<(object, object)>(Pairs.Instance), 0);

    /// <param name="depth">How deep the values go: deeper than any layout's data, they count as different.</param>
    static bool Same(object? a, object? b, HashSet<(object, object)> seen, int depth)
    {
        if (ReferenceEquals(a, b))
            return true;
        // Closures are not compared, whether there are any or not.
        if (a is Delegate || b is Delegate)
            return a is null or Delegate && b is null or Delegate;
        if (a == null || b == null)
            return false;
        switch (a)
        {
            case string:
                return a.Equals(b);
            case Block x:
                return b is Block block && x.Id == block.Id && SameBlock(x, block);
            case XNode x:
                return b is XNode node && XNode.DeepEquals(x, node);
        }
        var type = a.GetType();
        if (type != b.GetType())
            return false;
        if (type.IsPrimitive || type.IsEnum || a is decimal)
            return a.Equals(b);
        if (depth > 64)
            return false;
        // A pair met again on the way down is the same unless something else differs.
        if (!type.IsValueType && !seen.Add((a, b)))
            return true;
        if (a is IEnumerable left)
        {
            var right = ((IEnumerable)b).GetEnumerator();
            foreach (var item in left)
            {
                if (!right.MoveNext() || !Same(item, right.Current, seen, depth + 1))
                    return false;
            }
            return !right.MoveNext();
        }
        if (!type.IsValueType && !Own(type))
            return false;
        var (fields, properties) = members.GetOrAdd(type, t => (t.GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => !f.FieldType.IsByRefLike).ToArray(),
            t.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !p.PropertyType.IsByRefLike).ToArray()));
        foreach (var field in fields)
        {
            if (!Same(field.GetValue(a), field.GetValue(b), seen, depth + 1))
                return false;
        }
        foreach (var property in properties)
        {
            if (!Same(property.GetValue(a), property.GetValue(b), seen, depth + 1))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Whether two blocks of the same id are the same: paragraphs of the same element, formats and inlines (copies the
    /// layout made of one, without its leading break, are the same where they hold the same inlines), tables of the same
    /// model; an edited block is another.
    /// </summary>
    static bool SameBlock(Block x, Block y) => (x, y) switch
    {
        (ParagraphBlock p, ParagraphBlock q) => ReferenceEquals(p.Element, q.Element) && Equals(p.Format, q.Format) && Equals(p.MarkFormat, q.MarkFormat)
            && ReferenceEquals(p.EndsSection, q.EndsSection) && Equals(p.Label, q.Label) && p.Content.Count == q.Content.Count
            && p.Content.Zip(q.Content).All(c => ReferenceEquals(c.First, c.Second)),
        (TableBlock t, TableBlock u) => ReferenceEquals(t.Table, u.Table),
        _ => false,
    };

    /// <summary>The engine's own data: its types outside fonts and packages, but for the layout's services.</summary>
    static bool Own(Type type) =>
        type.Namespace is { } space && space.StartsWith("FreeSlate", StringComparison.Ordinal)
        && !space.StartsWith("FreeSlate.Fonts", StringComparison.Ordinal) && !space.StartsWith("FreeSlate.Opc", StringComparison.Ordinal)
        && type != typeof(Paginator) && type != typeof(ParagraphLayout) && type != typeof(TableLayout) && type != typeof(FieldEvaluator)
        && type != typeof(RunStyles) && type != typeof(DocumentContext) && type != typeof(ModelReader);

    /// <summary>Pairs of objects by reference.</summary>
    sealed class Pairs : IEqualityComparer<(object, object)>
    {
        public static readonly Pairs Instance = new();

        public bool Equals((object, object) x, (object, object) y) => ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);

        public int GetHashCode((object, object) pair) =>
            HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Item1), System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Item2));
    }
}
