using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.World;

namespace DelveRpg.Kit.Ai;

/// <summary>
/// One monster's decisions per tick. The donor's monster is flag-driven —
/// idle until it detects the player, chase along a grid path, flee below the
/// health threshold — and so is this. Detection is range-and-line-of-sight;
/// the light-based stealth of the donor is approximated by the archetype's
/// detect range (see docs/gameplay-design.md).
/// </summary>
public static class MonsterBrain
{
    private const int PathRefreshTicks = 30;
    private const int PathHorizon = 64;

    public static void Tick(
        MonsterState monster,
        DungeonLevel level,
        PlayerState player,
        GameTuning tuning,
        float speedMultiplier)
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

        bool seesTarget = LineOfSight.CanSee(level, body.TileX, body.TileY, target.TileX, target.TileY)
            && Distance(body, target) <= monster.Archetype.DetectRange;

        if (monster.BrainState == MonsterBrainState.Idle && seesTarget)
        {
            monster.BrainState = MonsterBrainState.Chasing;
            monster.PathRefreshRemaining = 0;
        }

        switch (monster.BrainState)
        {
            case MonsterBrainState.Idle:
                return;
            case MonsterBrainState.Fleeing:
                StepTowards(monster, level, Away(body, target), speedMultiplier);
                return;
            case MonsterBrainState.Chasing:
                if (!seesTarget && Distance(body, target) > monster.Archetype.DetectRange * 2)
                {
                    monster.BrainState = MonsterBrainState.Idle;
                    return;
                }

                StepAlongPath(monster, level, target, tuning, speedMultiplier);
                return;
        }
    }

    private static void StepAlongPath(
        MonsterState monster,
        DungeonLevel level,
        ActorState target,
        GameTuning tuning,
        float speedMultiplier)
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

        if (monster.PathIndex >= monster.Path.Count)
        {
            return;
        }

        (int nextX, int nextY) = monster.Path[monster.PathIndex];
        if (level.At(nextX, nextY).Kind == World.TileKind.DoorClosed)
        {
            // A chase goes through doors; the donor's monsters open them too.
            level.TryOpenDoor(nextX, nextY);
        }

        float speed = 0.03f * speedMultiplier * monster.Archetype.Stats.Speed / 4f;
        if (MoveTowards(monster.Body, nextX + 0.5f, nextY + 0.5f, speed, level))
        {
            monster.PathIndex++;
        }
    }

    private static void StepTowards(MonsterState monster, DungeonLevel level, (int X, int Y) tile, float speedMultiplier)
    {
        MoveTowards(monster.Body, tile.X + 0.5f, tile.Y + 0.5f, 0.035f * speedMultiplier, level);
    }

    /// <summary>Move one body towards a point; returns true when the point is reached.</summary>
    private static bool MoveTowards(ActorState body, float targetX, float targetY, float speed, DungeonLevel level)
    {
        float dx = targetX - body.X;
        float dy = targetY - body.Y;
        float distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance <= speed || distance <= 0.0001f)
        {
            body.X = targetX;
            body.Y = targetY;
            return true;
        }

        float nextX = body.X + ((dx / distance) * speed);
        float nextY = body.Y + ((dy / distance) * speed);
        if (level.IsWalkable((int)MathF.Floor(nextX), (int)MathF.Floor(body.Y)))
        {
            body.X = nextX;
        }

        if (level.IsWalkable((int)MathF.Floor(body.X), (int)MathF.Floor(nextY)))
        {
            body.Y = nextY;
        }

        return false;
    }

    private static (int X, int Y) Away(ActorState body, ActorState target)
    {
        int awayX = body.TileX + (body.TileX - target.TileX);
        int awayY = body.TileY + (body.TileY - target.TileY);
        return (awayX, awayY);
    }

    private static float Distance(ActorState left, ActorState right)
    {
        float dx = left.X - right.X;
        float dy = left.Y - right.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
