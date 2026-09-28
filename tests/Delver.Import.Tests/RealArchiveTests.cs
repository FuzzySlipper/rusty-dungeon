using Delver.Import.Jar;
using Xunit;

namespace Delver.Import.Tests;

/// <summary>
/// Loose checks against the real donor archive. The suite must pass on
/// machines without the game files, so the test exits early when the archive
/// is absent instead of being skipped dynamically.
/// </summary>
public class RealArchiveTests
{
    [Fact]
    public void RealArchive_ClassifiesEntries()
    {
        string jarPath = Environment.GetEnvironmentVariable("DELVER_GAME_JAR")
            ?? "/home/agent/research/delver-game/delver.jar";
        if (!File.Exists(jarPath))
        {
            return;
        }

        using DelverArchive archive = DelverArchive.Open(jarPath);

        Assert.NotEmpty(archive.Entries);
        var kinds = archive.Entries.Select(entry => entry.Kind).Distinct().ToList();
        Assert.NotEmpty(kinds);
        Assert.Contains(DelverEntryKind.Data, kinds);
        Assert.Contains(DelverEntryKind.Class, kinds);
        Assert.True(archive.Entries.Count(entry => entry.Kind is DelverEntryKind.Level or DelverEntryKind.RoomTemplate) > 0);
        Assert.Contains(archive.Entries, entry => entry.Path == "data/monsters.dat" && entry.Kind == DelverEntryKind.Data);
    }
}
