namespace DelveRpg.Kit.World;

/// <summary>
/// Grid line of sight. The donor walks a parametric ray between two points and
/// lets corner planes block it; this port samples the same idea on the tile
/// grid with a Bresenham walk, which keeps the rule "walls and closed doors
/// block sight" identical where it matters.
/// </summary>
public static class LineOfSight
{
    public static bool CanSee(DungeonLevel level, int fromX, int fromY, int toX, int toY)
    {
        int x = fromX;
        int y = fromY;
        int dx = Math.Abs(toX - fromX);
        int dy = Math.Abs(toY - fromY);
        int stepX = fromX < toX ? 1 : -1;
        int stepY = fromY < toY ? 1 : -1;
        int error = dx - dy;

        while (x != toX || y != toY)
        {
            int doubled = 2 * error;
            if (doubled > -dy)
            {
                error -= dy;
                x += stepX;
            }

            if (doubled < dx)
            {
                error += dx;
                y += stepY;
            }

            if (x == toX && y == toY)
            {
                return true;
            }

            if (level.BlocksSight(x, y))
            {
                return false;
            }
        }

        return true;
    }
}
