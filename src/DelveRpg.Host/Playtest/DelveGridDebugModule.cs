using System.Text;
using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Rusty.Engine.Debugging;

namespace DelveRpg.Host.Playtest;

/// <summary>
/// <c>delve.grid</c>: the true floor around the player as text rows, for
/// tools that need to route (the HUD map shows only what was explored).
/// Read-only; it never advances time.
/// </summary>
public sealed class DelveGridDebugModule(Func<RunSession?> session) : IDebugCommandModule
{
    private const int Radius = 16;

    [DebugCommand("delve.grid", Description = "The floor within 16 tiles of the player: # wall, . floor, + door, L locked door, ~ water, < > stairs, ^ spikes, _ pressure plate, o pot, @ player, m monster, i item. First line: origin x,y of the top-left cell.")]
    public DebugCommandResult Grid()
    {
        if (session() is not RunSession run)
        {
            return DebugCommandResult.Failure(DebugCommandStatus.ModuleUnavailable, "No run in progress.");
        }

        int px = run.Player.Body.TileX;
        int py = run.Player.Body.TileY;
        int left = Math.Max(0, px - Radius);
        int top = Math.Max(0, py - Radius);
        int right = Math.Min(run.Level.Width - 1, px + Radius);
        int bottom = Math.Min(run.Level.Height - 1, py + Radius);
        var marks = new Dictionary<(int, int), char>();
        foreach (SpikeTrap spikes in run.Spikes) marks[(spikes.TileX, spikes.TileY)] = '^';
        foreach (TouchTrigger trigger in run.Triggers.Where(t => t.IsPlate)) marks[(trigger.TileX, trigger.TileY)] = '_';
        foreach (Pot pot in run.Pots) marks[(pot.TileX, pot.TileY)] = 'o';
        foreach (GroundItem item in run.GroundItems) marks[(item.X, item.Y)] = 'i';
        foreach (var monster in run.Monsters) marks[(monster.Body.TileX, monster.Body.TileY)] = 'm';
        marks[(px, py)] = '@';

        var text = new StringBuilder($"{left},{top}\n");
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                text.Append(marks.TryGetValue((x, y), out char mark) ? mark : run.Level.At(x, y).Kind switch
                {
                    TileKind.Wall => '#',
                    TileKind.DoorClosed or TileKind.DoorOpen => '+',
                    TileKind.DoorLocked => 'L',
                    TileKind.Water => '~',
                    TileKind.StairsDown => '>',
                    TileKind.StairsUp => '<',
                    _ => '.',
                });
            }

            text.Append('\n');
        }

        return DebugCommandResult.Success(text.ToString());
    }
}
