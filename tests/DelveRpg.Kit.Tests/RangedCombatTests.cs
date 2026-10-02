using DelveRpg.Kit.Actors;
using DelveRpg.Kit.Combat;
using DelveRpg.Kit.Effects;
using DelveRpg.Kit.Progression;
using DelveRpg.Kit.Random;
using DelveRpg.Kit.Rules;
using DelveRpg.Kit.Session;
using DelveRpg.Kit.World;
using Xunit;

namespace DelveRpg.Kit.Tests;

/// <summary>Bows, wands, a casting monster and the effects elemental hits leave.</summary>
public sealed class RangedCombatTests
{
    private const int HallWidth = 14;

    private static RunPlan Plan => new([new FloorSpec(0, 1, "One", "Test", false)]);

    /// <summary>
    /// A one-tile-wide east–west hall (floor x 1..12 on row 1) with the player
    /// at its west end facing east, holding the given slots.
    /// </summary>
    internal static RunSession Hall(
        IRandomSource random,
        IReadOnlyList<SnapshotSlot> slots,
        string? weaponId,
        IReadOnlyList<SnapshotMonster>? monsters = null,
        float playerX = 1.5f)
    {
        var tiles = new byte[HallWidth * 3];
        Array.Fill(tiles, (byte)TileKind.Wall);
        for (int x = 1; x < HallWidth - 1; x++)
        {
            tiles[HallWidth + x] = (byte)TileKind.Floor;
        }

        var explored = new byte[tiles.Length];
        var floor = new SnapshotFloor(HallWidth, 3, 1, "Test", tiles, explored, 1, 1, 12, 1);
        RunSession fresh = new(new ScriptedCatalog(), new GameTuning(), Plan, 7UL, MetaProgression.Fresh, new ScriptedRandom());
        List<SnapshotSlot> allSlots = slots.ToList();
        while (allSlots.Count < 24)
        {
            allSlots.Add(new SnapshotSlot(null, 0));
        }

        RunSnapshot snapshot = fresh.Capture() with
        {
            Floor = floor,
            PlayerX = playerX,
            PlayerY = 1.5f,
            Facing = MathF.PI / 2f,
            PitchDegrees = 0f,
            Slots = allSlots,
            WieldedSlot = weaponId is null ? -1 : 0,
            WeaponItemId = weaponId,
            Monsters = monsters ?? [],
            GroundItems = [],
        };

        return new RunSession(new ScriptedCatalog(), new GameTuning(), Plan, snapshot, MetaProgression.Fresh, random);
    }

    internal static void HoldThenRelease(RunSession session, int heldTicks)
    {
        for (int i = 0; i < heldTicks; i++)
        {
            session.Tick(RunInput.Idle with { AttackHeld = true });
        }

        session.Tick(RunInput.Idle);
    }

    internal static void Idle(RunSession session, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            session.Tick(RunInput.Idle);
        }
    }

    [Fact]
    public void A_bow_without_arrows_looses_nothing()
    {
        RunSession session = Hall(new ScriptedRandom(), [new SnapshotSlot("test.item.bow", 1)], "test.item.bow");

        HoldThenRelease(session, 36);

        Assert.Empty(session.Projectiles);
        Assert.Contains(session.Messages, message => message.Text.Contains("no arrows"));
    }

    [Fact]
    public void A_full_charge_arrow_flies_at_range_over_eight_and_strikes_the_first_monster()
    {
        // Every draw low: the weapon roll adds nothing to the bow's base 3.
        RunSession session = Hall(
            ScriptedRandom.Always(0),
            [new SnapshotSlot("test.item.bow", 1), new SnapshotSlot("test.item.arrows", 6)],
            "test.item.bow",
            [new SnapshotMonster("test.monster.rat", 8.5f, 1.5f, 6, 0f)]);

        for (int i = 0; i < 36; i++)
        {
            session.Tick(RunInput.Idle with { AttackHeld = true });
        }

        session.Tick(RunInput.Idle);

        Projectile arrow = Assert.Single(session.Projectiles);
        Assert.Equal(1f, arrow.VelocityX, 3); // full power × range 8 / 8
        Assert.False(arrow.Floating);
        Assert.Equal(5, session.Player.Inventory.Slot(1)?.Count);

        Idle(session, 10);

        Assert.Empty(session.Projectiles);
        MonsterState rat = Assert.Single(session.Monsters);
        Assert.Equal(3, rat.Body.Hp); // 6 − 3, no dodge, no armor
        Assert.Equal("test.item.arrows", Assert.Single(rat.Carried).ArchetypeId);
    }

    [Fact]
    public void A_monster_killed_by_arrows_drops_the_arrows_it_caught()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [new SnapshotSlot("test.item.bow", 1), new SnapshotSlot("test.item.arrows", 6)],
            "test.item.bow",
            [new SnapshotMonster("test.monster.rat", 6.5f, 1.5f, 5, 0f)]);

        HoldThenRelease(session, 36);
        Idle(session, 8 + RunSession.DeathDelayTicks);

        Assert.Empty(session.Monsters);
        GroundItem dropped = Assert.Single(session.GroundItems);
        Assert.Equal("test.item.arrows", dropped.Item.ArchetypeId);
        Assert.Equal(1, dropped.Item.Count);
    }

    [Fact]
    public void An_arrow_that_hits_a_wall_lies_before_it_unless_it_breaks()
    {
        RunSession kept = Hall(
            ScriptedRandom.Always(999_999),
            [new SnapshotSlot("test.item.bow", 1), new SnapshotSlot("test.item.arrows", 6)],
            "test.item.bow");
        HoldThenRelease(kept, 36);
        Idle(kept, 20);

        Assert.Empty(kept.Projectiles);
        GroundItem lying = Assert.Single(kept.GroundItems);
        Assert.Equal((12, 1), (lying.X, lying.Y));

        // A low draw is the one-in-ten break.
        RunSession broken = Hall(
            ScriptedRandom.Always(0),
            [new SnapshotSlot("test.item.bow", 1), new SnapshotSlot("test.item.arrows", 6)],
            "test.item.bow");
        HoldThenRelease(broken, 36);
        Idle(broken, 20);

        Assert.Empty(broken.Projectiles);
        Assert.Empty(broken.GroundItems);
    }

    [Fact]
    public void A_quick_tap_arrow_drops_to_the_floor_close_by()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [new SnapshotSlot("test.item.bow", 1), new SnapshotSlot("test.item.arrows", 6)],
            "test.item.bow");

        HoldThenRelease(session, 1);
        Idle(session, 40);

        Assert.Empty(session.Projectiles);
        GroundItem lying = Assert.Single(session.GroundItems);
        Assert.True(lying.X <= 2, $"a tapped arrow flew to x={lying.X}");
    }

    [Fact]
    public void An_auto_fire_wand_zaps_while_held_and_fizzles_when_spent()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(0),
            [new SnapshotSlot("test.item.wand", 1, Charges: 3)],
            "test.item.wand");

        session.Tick(RunInput.Idle with { AttackHeld = true });
        Projectile bolt = Assert.Single(session.Projectiles);
        Assert.True(bolt.Floating);
        Assert.Equal(DamageType.Magic, bolt.DamageType);
        Assert.Equal(0.45f, bolt.VelocityX, 3);
        Assert.Equal(2, session.Player.Inventory.Slot(0)?.Charges);

        for (int i = 0; i < 45; i++)
        {
            session.Tick(RunInput.Idle with { AttackHeld = true });
        }

        Assert.Equal(0, session.Player.Inventory.Slot(0)?.Charges);
        Assert.Equal("test.item.wand", session.Player.Inventory.Slot(0)?.ArchetypeId);
        Assert.Contains(session.Messages, message => message.Text.Contains("no charges"));
    }

    [Fact]
    public void A_fire_bolt_sets_its_target_burning()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(0),
            [new SnapshotSlot("test.item.fire-wand", 1, Charges: 3)],
            "test.item.fire-wand",
            [new SnapshotMonster("test.monster.rat", 4.5f, 1.5f, 50, 0f)]);

        HoldThenRelease(session, 5);
        Idle(session, 8);

        MonsterState rat = Assert.Single(session.Monsters);
        Assert.True(rat.Body.Effects.IsActive(EffectKind.Burning));
        Assert.True(rat.Body.Hp < 50);
    }

    [Fact]
    public void A_caster_that_does_not_chase_holds_and_casts_a_bolt_that_ignores_armor()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(0),
            [],
            null,
            [new SnapshotMonster("test.monster.eye", 8.5f, 1.5f, 2, 0f)]);
        int startHp = session.Player.Body.Hp;

        Idle(session, 140);

        MonsterState eye = Assert.Single(session.Monsters);
        Assert.Equal(8.5f, eye.Body.X);
        Assert.Equal(MonsterBrainState.Chasing, eye.BrainState);
        // The first cast waits out the alert beat (40), then flies 7 tiles at 0.1;
        // a low roll is 3, taken whole.
        Assert.Equal(startHp - 3, session.Player.Body.Hp);
        Assert.Contains(session.Messages, message => message.Text.Contains("bolt hits you for 3"));
    }

    [Fact]
    public void A_keep_distance_monster_backs_away_from_a_close_player()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(999_999),
            [],
            null,
            [new SnapshotMonster("test.monster.skulker", 3.5f, 1.5f, 6, 0f)]);

        Idle(session, 30);

        Assert.True(Assert.Single(session.Monsters).Body.X > 3.5f);
    }

    [Fact]
    public void Wand_charges_survive_the_save_boundary()
    {
        RunSession session = Hall(
            ScriptedRandom.Always(0),
            [new SnapshotSlot("test.item.wand", 1, Charges: 3)],
            "test.item.wand");
        session.Tick(RunInput.Idle with { AttackHeld = true });

        SnapshotSlot slot = session.Capture().Slots[0];

        Assert.Equal(2, slot.Charges);
    }

    [Fact]
    public void Burning_and_poison_strike_once_per_interval_and_poison_never_kills()
    {
        var effects = new EffectSet();
        effects.Apply(EffectKind.Burning, 600, 1, intervalTicks: 160);
        int damage = 0;
        for (int i = 0; i < 159; i++)
        {
            damage += effects.Tick(10);
        }

        Assert.Equal(0, damage);
        Assert.Equal(1, effects.Tick(10));

        var poison = new EffectSet();
        poison.Apply(EffectKind.Poison, 1000, 1, intervalTicks: 160);
        int poisonDamage = 0;
        for (int i = 0; i < 400; i++)
        {
            poisonDamage += poison.Tick(1);
        }

        Assert.Equal(0, poisonDamage);
    }

    [Fact]
    public void Elemental_hits_leave_the_donor_status_effects()
    {
        EffectSet Hit(DamageType type, int draw)
        {
            var effects = new EffectSet();
            ElementalEffects.ApplyOnHit(type, effects, new ScriptedRandom(draw));
            return effects;
        }

        Assert.True(Hit(DamageType.Fire, 0).IsActive(EffectKind.Burning));
        Assert.Empty(Hit(DamageType.Fire, 1).Active); // fire catches half the time
        Assert.True(Hit(DamageType.Ice, 0).IsActive(EffectKind.Slowed));
        Assert.True(Hit(DamageType.Poison, 0).IsActive(EffectKind.Poison));
        Assert.True(Hit(DamageType.Paralyze, 0).IsParalyzed);
        Assert.Empty(Hit(DamageType.Magic, 0).Active);
        Assert.Empty(Hit(DamageType.Physical, 0).Active);
    }
}
