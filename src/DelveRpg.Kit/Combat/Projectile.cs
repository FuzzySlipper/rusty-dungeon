namespace DelveRpg.Kit.Combat;

/// <summary>
/// One arrow or bolt in flight over the tile grid. Position is in tiles with
/// <see cref="Z"/> the height above the floor (the ceiling is at 1); velocity
/// is per tick. A projectile that is not floating falls under the donor's
/// gravity ([donor] entities/projectiles/Projectile.java tick: za -= 0.0035).
/// </summary>
public sealed class Projectile
{
    public const float Gravity = 0.0035f;

    public Projectile(long id, bool fromPlayer, float x, float y, float z, float velocityX, float velocityY, float velocityZ)
    {
        Id = id;
        FromPlayer = fromPlayer;
        X = x;
        Y = y;
        Z = z;
        VelocityX = velocityX;
        VelocityY = velocityY;
        VelocityZ = velocityZ;
    }

    public long Id { get; }

    /// <summary>A player's shot hits monsters; a monster's hits the player.</summary>
    public bool FromPlayer { get; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }

    public float VelocityX { get; set; }

    public float VelocityY { get; set; }

    public float VelocityZ { get; set; }

    /// <summary>Bolts float on a straight line; arrows arc down.</summary>
    public bool Floating { get; init; }

    public int Damage { get; init; }

    public DamageType DamageType { get; init; }

    /// <summary>Scales the projectile's own velocity into the shove it gives on a hit.</summary>
    public float Knockback { get; init; }

    public string SpriteId { get; init; } = "";

    /// <summary>The item an arrow becomes again where it lands; null for a bolt.</summary>
    public string? AmmoItemId { get; init; }

    public int AgeTicks { get; set; }
}
