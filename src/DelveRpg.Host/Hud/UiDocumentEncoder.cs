using System.Text;
using Rusty.Engine;

namespace DelveRpg.Host.Hud;

/// <summary>A small ordered document tree the HUD projection builds from.</summary>
public abstract record UiDocument
{
    public sealed record Obj(IReadOnlyList<(string Key, UiDocument Value)> Fields) : UiDocument;

    public sealed record Arr(IReadOnlyList<UiDocument> Items) : UiDocument;

    public sealed record Str(string Value) : UiDocument;

    public sealed record Num(double Value) : UiDocument;

    public sealed record Flag(bool Value) : UiDocument;

    public sealed record Nothing : UiDocument;

    public static UiDocument Object(params (string Key, UiDocument Value)[] fields) => new Obj(fields);

    public static UiDocument Array(params UiDocument[] items) => new Arr(items);
}

/// <summary>
/// Encodes one <see cref="UiDocument"/> into the Engine's structured UI value:
/// a node graph whose keys and text share one UTF-8 buffer.
/// </summary>
public static class UiDocumentEncoder
{
    public static UiValue Encode(UiDocument document)
    {
        var nodes = new List<StructuredValueNode>();
        var edges = new List<uint>();
        var utf8 = new MemoryStream();
        EncodeNode(document, null, nodes, edges, utf8);
        return new UiValue(
            nodes.ToArray(),
            edges.ToArray(),
            0,
            utf8.ToArray());
    }

    private static uint EncodeNode(
        UiDocument document,
        string? key,
        List<StructuredValueNode> nodes,
        List<uint> edges,
        MemoryStream utf8)
    {
        (uint keyOffset, uint keyLength) = Append(utf8, key);
        uint nodeIndex = (uint)nodes.Count;
        nodes.Add(default); // reserve; filled once children are known

        switch (document)
        {
            case UiDocument.Obj obj:
            {
                // Children encode depth-first; their edge runs belong to them.
                // This node's edge block is written as one contiguous run after
                // its whole subtree, because consumers read exactly
                // [firstEdge, firstEdge + childCount).
                var children = new List<uint>(obj.Fields.Count);
                foreach ((string fieldKey, UiDocument value) in obj.Fields)
                {
                    children.Add(EncodeNode(value, fieldKey, nodes, edges, utf8));
                }

                uint firstEdge = (uint)edges.Count;
                edges.AddRange(children);
                nodes[(int)nodeIndex] = new StructuredValueNode(
                    StructuredValueKind.Object, 0, 0, keyOffset, keyLength, 0, 0, firstEdge, (uint)obj.Fields.Count);
                break;
            }

            case UiDocument.Arr arr:
            {
                var children = new List<uint>(arr.Items.Count);
                foreach (UiDocument item in arr.Items)
                {
                    children.Add(EncodeNode(item, null, nodes, edges, utf8));
                }

                uint firstEdge = (uint)edges.Count;
                edges.AddRange(children);
                nodes[(int)nodeIndex] = new StructuredValueNode(
                    StructuredValueKind.Array, 0, 0, keyOffset, keyLength, 0, 0, firstEdge, (uint)arr.Items.Count);
                break;
            }

            case UiDocument.Str text:
            {
                (uint textOffset, uint textLength) = Append(utf8, text.Value);
                nodes[(int)nodeIndex] = new StructuredValueNode(
                    StructuredValueKind.String, 0, 0, keyOffset, keyLength, textOffset, textLength, 0, 0);
                break;
            }

            case UiDocument.Num number:
                nodes[(int)nodeIndex] = new StructuredValueNode(
                    StructuredValueKind.Number, 0, number.Value, keyOffset, keyLength, 0, 0, 0, 0);
                break;

            case UiDocument.Flag flag:
                nodes[(int)nodeIndex] = new StructuredValueNode(
                    StructuredValueKind.Bool, flag.Value ? 1u : 0u, 0, keyOffset, keyLength, 0, 0, 0, 0);
                break;

            default:
                nodes[(int)nodeIndex] = new StructuredValueNode(
                    StructuredValueKind.Null, 0, 0, keyOffset, keyLength, 0, 0, 0, 0);
                break;
        }

        return nodeIndex;
    }

    private static (uint Offset, uint Length) Append(MemoryStream utf8, string? text)
    {
        if (text is null)
        {
            return (0, 0);
        }

        uint offset = (uint)utf8.Length;
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        utf8.Write(bytes);
        return (offset, (uint)bytes.Length);
    }
}
