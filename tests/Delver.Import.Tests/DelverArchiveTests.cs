using System.Text;
using Delver.Import.Jar;
using Xunit;

namespace Delver.Import.Tests;

public class DelverArchiveTests
{
    private static readonly (string Path, string Content)[] Fixture =
    {
        ("data/monsters.dat", "{\"monsters\": {}}"),
        ("generator/Dungeon/section.dat", "{\"name\": \"Dungeon\"}"),
        ("generator/Dungeon/Halls/1.dat", "{width:17}"),
        ("generator/Dungeon/Halls/2.bin", "kryo"),
        ("levels/test/test1.bin", "kryo-level"),
        ("levels/test/test1.dat", "{entities:[]}"),
        ("textures/door.png", "png"),
        ("meshes/door_0.obj", "obj"),
        ("ui/skin.json", "{}"),
        ("ui/editor/HoloSkin/Holo.atlas", "atlas"),
        ("ui/pixel.fnt", "fnt"),
        ("sounds/a.mp3", "mp3"),
        ("shaders/tint.vert", "vert"),
        ("com/interrupt/Foo.class", "class"),
        ("natives/libfoo.so", "so"),
        ("META-INF/MANIFEST.MF", "Manifest-Version: 1.0"),
    };

    [Fact]
    public void Entries_AreClassifiedAndOrdered()
    {
        using MemoryStream zip = SyntheticArchive.CreateZip(Fixture.Reverse().ToArray());
        using DelverArchive archive = new(zip, leaveOpen: true);

        var paths = archive.Entries.Select(entry => entry.Path).ToList();
        Assert.Equal(paths.OrderBy(path => path, StringComparer.Ordinal).ToList(), paths);

        Assert.Equal(DelverEntryKind.Data, KindOf(archive, "data/monsters.dat"));
        Assert.Equal(DelverEntryKind.Data, KindOf(archive, "generator/Dungeon/section.dat"));
        Assert.Equal(DelverEntryKind.RoomTemplate, KindOf(archive, "generator/Dungeon/Halls/1.dat"));
        Assert.Equal(DelverEntryKind.RoomTemplate, KindOf(archive, "generator/Dungeon/Halls/2.bin"));
        Assert.Equal(DelverEntryKind.Level, KindOf(archive, "levels/test/test1.bin"));
        Assert.Equal(DelverEntryKind.Level, KindOf(archive, "levels/test/test1.dat"));
        Assert.Equal(DelverEntryKind.Texture, KindOf(archive, "textures/door.png"));
        Assert.Equal(DelverEntryKind.Mesh, KindOf(archive, "meshes/door_0.obj"));
        Assert.Equal(DelverEntryKind.Data, KindOf(archive, "ui/skin.json"));
        Assert.Equal(DelverEntryKind.Data, KindOf(archive, "ui/editor/HoloSkin/Holo.atlas"));
        Assert.Equal(DelverEntryKind.Font, KindOf(archive, "ui/pixel.fnt"));
        Assert.Equal(DelverEntryKind.Audio, KindOf(archive, "sounds/a.mp3"));
        Assert.Equal(DelverEntryKind.Shader, KindOf(archive, "shaders/tint.vert"));
        Assert.Equal(DelverEntryKind.Class, KindOf(archive, "com/interrupt/Foo.class"));
        Assert.Equal(DelverEntryKind.Native, KindOf(archive, "natives/libfoo.so"));
        Assert.Equal(DelverEntryKind.Other, KindOf(archive, "META-INF/MANIFEST.MF"));
    }

    [Fact]
    public void ExtractTo_WritesSelectedKindsPreservingInnerPaths()
    {
        SyntheticArchive.WithTempDirectory(directory =>
        {
            using MemoryStream zip = SyntheticArchive.CreateZip(Fixture);
            using DelverArchive archive = new(zip, leaveOpen: true);
            IReadOnlyList<string> extracted = archive.ExtractTo(directory, ReferenceKinds);

            Assert.Equal("{\"monsters\": {}}", File.ReadAllText(Path.Combine(directory, "data", "monsters.dat")));
            Assert.Equal("{width:17}", File.ReadAllText(Path.Combine(directory, "generator", "Dungeon", "Halls", "1.dat")));
            Assert.True(File.Exists(Path.Combine(directory, "levels", "test", "test1.bin")));
            Assert.True(File.Exists(Path.Combine(directory, "textures", "door.png")));
            Assert.True(File.Exists(Path.Combine(directory, "ui", "pixel.fnt")));

            Assert.False(File.Exists(Path.Combine(directory, "com", "interrupt", "Foo.class")));
            Assert.False(File.Exists(Path.Combine(directory, "natives", "libfoo.so")));
            Assert.False(File.Exists(Path.Combine(directory, "sounds", "a.mp3")));
            Assert.False(File.Exists(Path.Combine(directory, "shaders", "tint.vert")));
            Assert.False(File.Exists(Path.Combine(directory, "META-INF", "MANIFEST.MF")));
            Assert.Equal(extracted.Count, extracted.Distinct().Count());
        });
    }

    [Fact]
    public void ExtractTo_OverwritesCleanly()
    {
        SyntheticArchive.WithTempDirectory(directory =>
        {
            Directory.CreateDirectory(Path.Combine(directory, "data"));
            File.WriteAllText(Path.Combine(directory, "data", "monsters.dat"), "stale garbage");

            using MemoryStream zip = SyntheticArchive.CreateZip(Fixture);
            using DelverArchive archive = new(zip, leaveOpen: true);
            archive.ExtractTo(directory, ReferenceKinds);

            Assert.Equal("{\"monsters\": {}}", File.ReadAllText(Path.Combine(directory, "data", "monsters.dat")));
        });
    }

    [Fact]
    public void ExtractTo_IsDeterministicAcrossRuns()
    {
        SyntheticArchive.WithTempDirectory(directory =>
        {
            string first = Path.Combine(directory, "first");
            string second = Path.Combine(directory, "second");
            using MemoryStream zipOne = SyntheticArchive.CreateZip(Fixture);
            using MemoryStream zipTwo = SyntheticArchive.CreateZip(Fixture.Reverse().ToArray());
            using DelverArchive archiveOne = new(zipOne, leaveOpen: true);
            using DelverArchive archiveTwo = new(zipTwo, leaveOpen: true);

            IReadOnlyList<string> extractedOne = archiveOne.ExtractTo(first, ReferenceKinds);
            IReadOnlyList<string> extractedTwo = archiveTwo.ExtractTo(second, ReferenceKinds);

            Assert.Equal(extractedOne, extractedTwo);
            foreach (string relative in extractedOne)
            {
                string suffix = relative.Replace('/', Path.DirectorySeparatorChar);
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(first, suffix)),
                    File.ReadAllBytes(Path.Combine(second, suffix)));
            }
        });
    }

    [Fact]
    public void ReadEntryText_DecodesUtf8Content()
    {
        using MemoryStream zip = SyntheticArchive.CreateZip(("data/strings.dat", "{\"greeting\": \"déjà\"}"));
        using DelverArchive archive = new(zip, leaveOpen: true);

        Assert.Equal("{\"greeting\": \"déjà\"}", archive.ReadEntryText("data/strings.dat"));
    }

    private static readonly HashSet<DelverEntryKind> ReferenceKinds = new()
    {
        DelverEntryKind.Data,
        DelverEntryKind.Level,
        DelverEntryKind.RoomTemplate,
        DelverEntryKind.Texture,
        DelverEntryKind.Mesh,
        DelverEntryKind.Font,
    };

    private static DelverEntryKind KindOf(DelverArchive archive, string path) =>
        archive.Entries.Single(entry => entry.Path == path).Kind;
}
