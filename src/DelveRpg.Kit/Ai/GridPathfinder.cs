using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Ai;

/// <summary>
/// Breadth-first grid navigation with a bounded horizon. The donor moves
/// monsters along node-graph paths and steers them through tile space
/// (NodeGraphPathfinding and DoomStylePathfinding); this port keeps only the
/// outcome a chase needs — a walkable route to the target — and simplifies
/// both to a grid BFS. See docs/gameplay-design.md.
/// </summary>
public static class GridPathfinder
{
    public static IReadOnlyList<(int X, int Y)> FindPath(
        DungeonLevel level,
        int fromX,
        int fromY,
        int toX,
        int toY,
        int maxSteps,
        float stepHeight = float.MaxValue)
    {
        if ((fromX == toX && fromY == toY) || !level.IsNavigable(toX, toY))
        {
            return Array.Empty<(int X, int Y)>();
        }

        var previous = new Dictionary<(int X, int Y), (int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((fromX, fromY));
        previous[(fromX, fromY)] = (-1, -1);
        int steps = 0;

        while (queue.Count > 0 && steps <= maxSteps)
        {
            steps++;
            (int x, int y) = queue.Dequeue();
            foreach ((int nextX, int nextY) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                // A path climbs no more than a step at a time.
                if (!level.IsNavigable(nextX, nextY) || previous.ContainsKey((nextX, nextY))
                    || level.StandHeight(nextX, nextY) > level.ClimbBase(x, y, level.StandHeight(x, y)) + stepHeight)
                {
                    continue;
                }

                previous[(nextX, nextY)] = (x, y);
                if (nextX == toX && nextY == toY)
                {
                    return Reconstruct(previous, (toX, toY));
                }

                queue.Enqueue((nextX, nextY));
            }
        }

        return Array.Empty<(int X, int Y)>();
    }

    private static IReadOnlyList<(int X, int Y)> Reconstruct(
        Dictionary<(int X, int Y), (int X, int Y)> previous,
        (int X, int Y) goal)
    {
        var path = new List<(int X, int Y)>();
        (int x, int y) = goal;
        while (previous[(x, y)] != (-1, -1))
        {
            path.Add((x, y));
            (x, y) = previous[(x, y)];
        }

        path.Reverse();
        return path;
    }
}
