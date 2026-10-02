using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Ai;

/// <summary>
/// One monster's decisions per tick. The donor's monster is flag-driven —
/// idle and wandering until it notices the player, chase along a path, flee
/// below the health threshold — and so is this. Noticing is the donor's
/// light-based rule: the player must be in sight and closer than a radius
/// that grows with how lit they stand (<see cref="RunSession"/> computes it),
/// or the monster must already have been hurt by them
/// ([donor] entities/Monster.java:527-566).
/// </summary>
public static class MonsterBrain
{
    private const int PathRefreshTicks = 30;
    private const int PathHorizon = 64;
    private const float KeepDistanceTiles = 3f;

    /// <summary>Monsters stop caring about a player out of sight this far off ([donor] Monster.java:456, :527).</summary>
    public const float SightTiles = 17f;

    /// <summary>[donor] Monster.java:617-700 wander: a new step every 100 ticks or on arrival.</summary>
    private const int WanderStepTicks = 100;

    /// <summary>[donor] Monster.java wander: a 3-in-20 chance on one roll in five to rest.</summary>
    private const int WanderRestTicks = 220;

    /// <summary>[donor] Monster.java:881-895 getSpeed: 60% speed while not alerted.</summary>
    private const float WanderSpeedFraction = 0.6f;

    private static readonly (int X, int Y)[] Steps = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Tick(
        MonsterState monster,
        DungeonLevel level,
        PlayerState player,
        GameTuning tuning,
        float speedMultiplier,
        float noticeRadius,
        IReadOnlyList<MonsterState> others,
        IRandomSource random)
    {
        ActorState body = monster.Body;
        ActorState target = player.Body;

        float fleeFraction = tuning.FleeHpFraction;
        if (body.Hp <= body.MaxHp * fleeFraction && body.Hp > 0)
        {
            monster.BrainState = MonsterBrainState.Fleeing;
        }
        else if (monster.BrainState == MonsterBrainState.Fleeing)
        {
            monster.BrainState = MonsterBrainState.Chasing;
        }

        float distance = Distance(body, target);
        bool inSight = distance <= SightTiles
            && LineOfSight.CanSee(level, body.TileX, body.TileY, target.TileX, target.TileY);
        bool notices = inSight && (distance < noticeRadius || monster.WasHit);

        if (monster.BrainState == MonsterBrainState.Idle && notices)
        {
            monster.BrainState = MonsterBrainState.Chasing;
            monster.PathRefreshRemaining = 0;
        }

        switch (monster.BrainState)
        {
            case MonsterBrainState.Idle:
                Wander(monster, level, speedMultiplier, others, target, tuning, random);
                return;
            case MonsterBrainState.Fleeing:
                StepTowards(monster, level, Away(body, target), FleeSpeed * speedMultiplier, others, target, tuning);
                return;
            case MonsterBrainState.Chasing:
                if (!inSight && distance > SightTiles)
                {
                    monster.BrainState = MonsterBrainState.Idle;
                    return;
                }

                // A keep-distance monster backs off inside three tiles; one
                // that does not chase holds where it was alerted and fights
                // from there ([donor] entities/Monster.java:549-552 keepDistance,
                // chasetarget).
                if (monster.Archetype.KeepsDistance && distance < KeepDistanceTiles)
                {
                    StepTowards(monster, level, Away(body, target), FleeSpeed * speedMultiplier, others, target, tuning);
                    return;
                }

                if (!monster.Archetype.ChasesTarget)
                {
                    return;
                }

                StepAlongPath(monster, level, target, tuning, speedMultiplier, others);
                return;
        }
    }

    private const float FleeSpeed = 0.035f;

    private static float ChaseSpeed(MonsterState monster, float speedMultiplier) =>
        0.03f * speedMultiplier * monster.Archetype.Stats.Speed / 4f;

    private static void StepAlongPath(
        MonsterState monster,
        DungeonLevel level,
        ActorState target,
        GameTuning tuning,
        float speedMultiplier,
        IReadOnlyList<MonsterState> others)
    {
        // Bodies do not overlap (the donor's entity collision); a monster
        // already beside its target holds and fights from there.
        if (Distance(monster.Body, target) <= tuning.ActorSeparationTiles)
        {
            return;
        }

        monster.PathRefreshRemaining--;
        if (monster.PathRefreshRemaining <= 0 || monster.PathIndex >= monster.Path.Count)
        {
            monster.Path = GridPathfinder.FindPath(
                level, monster.Body.TileX, monster.Body.TileY, target.TileX, target.TileY, PathHorizon);
            monster.PathIndex = 0;
            monster.PathRefreshRemaining = PathRefreshTicks;
        }

        // Cut corners the body clears: head for the furthest of the next few
        // path tiles it has a clear run to, so a chase runs diagonals and
        // rounds corners instead of stepping tile centre to tile centre
        // (the donor steers node to node; [donor] game/pathfinding/NodeGraphPathfinding.java).
        while (monster.PathIndex + 1 < monster.Path.Count
            && level.At(monster.Path[monster.PathIndex].X, monster.Path[monster.PathIndex].Y).Kind != TileKind.DoorClosed
            && ClearRun(level, monster.Body.X, monster.Body.Y, monster.Path[monster.PathIndex + 1]))
        {
            monster.PathIndex++;
        }

        if (monster.PathIndex >= monster.Path.Count)
        {
            return;
        }

        (int nextX, int nextY) = monster.Path[monster.PathIndex];
        if (level.At(nextX, nextY).Kind == TileKind.DoorClosed)
        {
            // A chase goes through doors; the donor's monsters open them too.
            level.TryOpenDoor(nextX, nextY);
        }

        if (MoveTowards(monster, nextX + 0.5f, nextY + 0.5f, ChaseSpeed(monster, speedMultiplier), level, others, target, tuning))
        {
            monster.PathIndex++;
        }
    }

    /// <summary>
    /// The donor's idle wander ([donor] Monster.java:612-700): step to a
    /// random open neighbouring tile, not straight back where it came from
    /// unless that is the only way, at 60% speed; pick anew on arrival or
    /// every 100 ticks, and now and then stand a while.
    /// </summary>
    private static void Wander(
        MonsterState monster,
        DungeonLevel level,
        float speedMultiplier,
        IReadOnlyList<MonsterState> others,
        ActorState player,
        GameTuning tuning,
        IRandomSource random)
    {
        if (monster.Archetype.Ambushes)
        {
            return;
        }

        if (monster.WanderTicksRemaining > 0)
        {
            monster.WanderTicksRemaining--;
        }

        if (monster.WanderTarget is not { } goal || monster.WanderTicksRemaining == 0)
        {
            PickWanderStep(monster, level, random);
            return;
        }

        if (monster.WanderResting)
        {
            return;
        }

        float speed = ChaseSpeed(monster, speedMultiplier) * WanderSpeedFraction;
        if (MoveTowards(monster, goal.X + 0.5f, goal.Y + 0.5f, speed, level, others, player, tuning))
        {
            PickWanderStep(monster, level, random);
        }
    }

    private static void PickWanderStep(MonsterState monster, DungeonLevel level, IRandomSource random)
    {
        monster.WanderTicksRemaining = WanderStepTicks;
        int roll = random.Next(0, 5);
        if (roll == 4 && random.Next(0, 20) < 3)
        {
            monster.WanderResting = true;
            monster.WanderTicksRemaining = WanderRestTicks;
            return;
        }

        monster.WanderResting = false;
        int x = monster.Body.TileX;
        int y = monster.Body.TileY;
        (int X, int Y)? back = null;
        var open = new List<(int X, int Y)>();
        foreach ((int dx, int dy) in Steps)
        {
            if (!level.IsWalkable(x + dx, y + dy))
            {
                continue;
            }

            if (monster.WanderFrom is { } from && from.X == x + dx && from.Y == y + dy)
            {
                back = (x + dx, y + dy);
                continue;
            }

            open.Add((x + dx, y + dy));
        }

        if (open.Count == 0 && back is { } only)
        {
            open.Add(only);
        }

        if (open.Count == 0)
        {
            monster.WanderTarget = null;
            return;
        }

        monster.WanderFrom = (x, y);
        monster.WanderTarget = open[roll % open.Count];
    }

    private static void StepTowards(
        MonsterState monster,
        DungeonLevel level,
        (int X, int Y) tile,
        float speed,
        IReadOnlyList<MonsterState> others,
        ActorState player,
        GameTuning tuning)
    {
        MoveTowards(monster, tile.X + 0.5f, tile.Y + 0.5f, speed, level, others, player, tuning);
    }

    /// <summary>
    /// Move one body towards a point; returns true when the point is reached.
    /// A step that would crowd another monster is refused and shoves that
    /// monster aside instead, as the donor's encroaching monster pushes the
    /// one in its way ([donor] Monster.java:1266-1297).
    /// </summary>
    private static bool MoveTowards(
        MonsterState monster,
        float targetX,
        float targetY,
        float speed,
        DungeonLevel level,
        IReadOnlyList<MonsterState> others,
        ActorState player,
        GameTuning tuning)
    {
        ActorState body = monster.Body;
        float dx = targetX - body.X;
        float dy = targetY - body.Y;
        float distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance <= 0.0001f)
        {
            return true;
        }

        float step = Math.Min(speed, distance);
        float nextX = body.X + (dx / distance * step);
        float nextY = body.Y + (dy / distance * step);
        if (Blocker(monster, nextX, nextY, others, tuning) is MonsterState blocker)
        {
            float pushX = blocker.Body.X - body.X;
            float pushY = blocker.Body.Y - body.Y;
            float length = MathF.Max(0.0001f, MathF.Sqrt((pushX * pushX) + (pushY * pushY)));
            blocker.VelocityX += pushX / length * MonsterPush * step;
            blocker.VelocityY += pushY / length * MonsterPush * step;
            return false;
        }

        if (level.IsWalkable((int)MathF.Floor(nextX), (int)MathF.Floor(body.Y)))
        {
            body.X = nextX;
        }

        if (level.IsWalkable((int)MathF.Floor(body.X), (int)MathF.Floor(nextY)))
        {
            body.Y = nextY;
        }

        return distance <= speed;
    }

    /// <summary>[donor] Monster.java:1266-1297: the pushed monster gets a fifth of the pusher's speed.</summary>
    private const float MonsterPush = 0.2f;

    /// <summary>Another living monster this step would crowd: inside the separation and closer than before.</summary>
    private static MonsterState? Blocker(MonsterState monster, float nextX, float nextY, IReadOnlyList<MonsterState> others, GameTuning tuning)
    {
        float separation = tuning.ActorSeparationTiles;
        foreach (MonsterState other in others)
        {
            if (ReferenceEquals(other, monster) || other.IsDying)
            {
                continue;
            }

            float after = DistanceSquared(other.Body.X, other.Body.Y, nextX, nextY);
            if (after < separation * separation
                && after < DistanceSquared(other.Body.X, other.Body.Y, monster.Body.X, monster.Body.Y))
            {
                return other;
            }
        }

        return null;
    }

    /// <summary>
    /// True when a body of the donor monster's 0.3 half-width can run
    /// straight from (x, y) to the centre of a tile without touching a wall
    /// or closed door, sampled every quarter tile.
    /// </summary>
    private static bool ClearRun(DungeonLevel level, float x, float y, (int X, int Y) tile)
    {
        const float radius = 0.3f;
        float toX = tile.X + 0.5f;
        float toY = tile.Y + 0.5f;
        float length = MathF.Sqrt(((toX - x) * (toX - x)) + ((toY - y) * (toY - y)));
        int samples = Math.Max(1, (int)MathF.Ceiling(length / 0.25f));
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            float px = x + ((toX - x) * t);
            float py = y + ((toY - y) * t);
            if (!level.IsWalkable((int)MathF.Floor(px - radius), (int)MathF.Floor(py - radius))
                || !level.IsWalkable((int)MathF.Floor(px + radius), (int)MathF.Floor(py - radius))
                || !level.IsWalkable((int)MathF.Floor(px - radius), (int)MathF.Floor(py + radius))
                || !level.IsWalkable((int)MathF.Floor(px + radius), (int)MathF.Floor(py + radius)))
            {
                return false;
            }
        }

        return true;
    }

    private static (int X, int Y) Away(ActorState body, ActorState target)
    {
        int awayX = body.TileX + (body.TileX - target.TileX);
        int awayY = body.TileY + (body.TileY - target.TileY);
        return (awayX, awayY);
    }

    private static float Distance(ActorState left, ActorState right) =>
        MathF.Sqrt(DistanceSquared(left.X, left.Y, right.X, right.Y));

    private static float DistanceSquared(float ax, float ay, float bx, float by) =>
        ((ax - bx) * (ax - bx)) + ((ay - by) * (ay - by));
}
