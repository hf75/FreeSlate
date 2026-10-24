using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Children of a large element (the body: tens of thousands of paragraphs) found and taken out without XLinq's walk: its
/// list of nodes is singly linked, so that XNode.Remove, PreviousNode and AddBeforeSelf walk from the parent's first
/// child to the node before (7 to 8 ms at page 4 000 of 5 000, measured with a 5 000-page document). Where the node
/// before is known, a child is taken out through XLinq's own fields, as XContainer.RemoveNode does after its walk (without
/// its change events, which nothing here listens to); where the runtime has no such fields, as XLinq does.
/// </summary>
static class Siblings
{
    /// <summary>Whether the runtime's XLinq lacks the fields: children are then taken out as XLinq does.</summary>
    static bool unavailable;

    /// <summary>
    /// The node just before a child (white space too), found from a node before it (from the parent's first node where
    /// null): null where the child is the first.
    /// </summary>
    public static XNode? NodeBefore(XContainer parent, XNode? from, XNode child)
    {
        var node = from ?? parent.FirstNode;
        if (node == null || ReferenceEquals(node, child))
            return null;
        while (!ReferenceEquals(node.NextNode, child))
            node = node.NextNode ?? throw new InvalidOperationException("The node is not a child of the parent after the node given.");
        return node;
    }

    /// <summary>Takes a child out of its parent, the node just before it being <paramref name="before"/> (null for the first child).</summary>
    public static void Remove(XNode child, XNode? before)
    {
        if (!unavailable && child.Parent is { } parent)
        {
            try
            {
                // The list is circular: the last child (the parent's content) comes before the first.
                var previous = before ?? ContentOf(parent) as XNode;
                if (previous != null && ReferenceEquals(NextOf(previous), child))
                {
                    if (ReferenceEquals(previous, child))
                        ContentOf(parent) = null;
                    else
                    {
                        if (ReferenceEquals(ContentOf(parent), child))
                            ContentOf(parent) = previous;
                        NextOf(previous) = NextOf(child);
                    }
                    ParentOf(child) = null;
                    NextOf(child) = null;
                    return;
                }
            }
            catch (MissingFieldException)
            {
                unavailable = true;
            }
        }
        child.Remove();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "next")]
    static extern ref XNode? NextOf(XNode node);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "parent")]
    static extern ref XContainer? ParentOf(XObject node);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "content")]
    static extern ref object? ContentOf(XContainer container);
}
