using System.Text;
using DelveRpg.Host.Hud;
using Rusty.Engine;

namespace DelveRpg.Host.Tests;

/// <summary>
/// The UI value crosses into the Engine's structured-value decoder, which
/// reads each node's children as the contiguous edge run
/// [firstEdge, firstEdge + childCount). These checks pin that shape on real
/// encoded documents — a shape bug here silently drops whole HUD sections.
/// </summary>
public sealed class UiDocumentEncoderTests
{
    private static UiDocument Sample() => UiDocument.Object(
        ("phase", new UiDocument.Str("run")),
        ("hp", new UiDocument.Num(12)),
        ("messages", UiDocument.Array(new UiDocument.Str("hi"), new UiDocument.Str("there"))),
        ("hotbar", UiDocument.Array(
            UiDocument.Object(
                ("index", new UiDocument.Num(0)),
                ("name", new UiDocument.Str("sword")),
                ("wielded", new UiDocument.Flag(true))),
            UiDocument.Object(
                ("index", new UiDocument.Num(1)),
                ("name", new UiDocument.Nothing())))),
        ("stats", UiDocument.Object(("attack", new UiDocument.Num(4)))),
        ("minimap", UiDocument.Object(
            ("size", new UiDocument.Num(3)),
            ("cells", new UiDocument.Str("@.#")),
            ("rows", UiDocument.Array(new UiDocument.Str("@.#"))))));

    private static void AssertShape(UiValue value)
    {
        StructuredValueNode[] nodes = value.Nodes.ToArray();
        uint[] edges = value.Edges.ToArray();
        byte[] utf8 = value.Utf8.ToArray();

        for (int i = 0; i < nodes.Length; i++)
        {
            StructuredValueNode node = nodes[i];

            // Every node's children live in one contiguous edge run.
            long end = (long)node.FirstEdge + node.ChildCount;
            Assert.True(end <= edges.Length, $"node {i} child run exceeds the edge array");

            for (uint offset = node.FirstEdge; offset < end; offset++)
            {
                uint child = edges[offset];
                Assert.True(child < nodes.Length, $"node {i} edge points outside the arena");
            }

            // Keys and text stay inside the shared buffer.
            Assert.True((long)node.KeyOffset + node.KeyLen <= utf8.Length, $"node {i} key out of bounds");
            Assert.True((long)node.TextOffset + node.TextLen <= utf8.Length, $"node {i} text out of bounds");

            // The root's children (the document's fields) are exactly the
            // keyed nodes: a run that only exists in order reads correctly.
            if (i == value.Root)
            {
                Assert.Equal(6u, node.ChildCount);
                for (uint offset = node.FirstEdge; offset < end; offset++)
                {
                    StructuredValueNode child = nodes[edges[offset]];
                    Assert.True(child.KeyLen > 0, "root field lost its key");
                }
            }
        }
    }

    [Fact]
    public void Encoded_documents_keep_child_edge_runs_contiguous()
    {
        UiValue value = UiDocumentEncoder.Encode(Sample());
        AssertShape(value);
    }

    [Fact]
    public void Root_field_keys_survive_with_nested_children_present()
    {
        UiValue value = UiDocumentEncoder.Encode(Sample());
        StructuredValueNode[] nodes = value.Nodes.ToArray();
        uint[] edges = value.Edges.ToArray();
        byte[] utf8 = value.Utf8.ToArray();
        var rootKeys = new List<string>();
        StructuredValueNode root = nodes[value.Root];
        for (uint offset = root.FirstEdge; offset < root.FirstEdge + root.ChildCount; offset++)
        {
            StructuredValueNode child = nodes[edges[offset]];
            rootKeys.Add(Encoding.UTF8.GetString(utf8.AsSpan((int)child.KeyOffset, (int)child.KeyLen)));
        }

        // With nested objects/arrays in the document, every root field must
        // still land at the root — the bug this guards against hoisted nested
        // keys and dropped whole sections.
        Assert.Equal(["phase", "hp", "messages", "hotbar", "stats", "minimap"], rootKeys);
    }

    [Fact]
    public void Nested_objects_decode_their_own_keys()
    {
        UiValue value = UiDocumentEncoder.Encode(Sample());
        StructuredValueNode[] nodes = value.Nodes.ToArray();
        uint[] edges = value.Edges.ToArray();
        byte[] utf8 = value.Utf8.ToArray();
        StructuredValueNode root = nodes[value.Root];
        uint minimapIndex = edges[root.FirstEdge + 5];
        StructuredValueNode minimap = nodes[minimapIndex];
        Assert.Equal(3u, minimap.ChildCount);

        var keys = new List<string>();
        for (uint offset = minimap.FirstEdge; offset < minimap.FirstEdge + minimap.ChildCount; offset++)
        {
            StructuredValueNode child = nodes[edges[offset]];
            keys.Add(Encoding.UTF8.GetString(utf8.AsSpan((int)child.KeyOffset, (int)child.KeyLen)));
        }

        Assert.Equal(["size", "cells", "rows"], keys);
    }
}
