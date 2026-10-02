using DelveRpg.Kit.Effects;
using DelveRpg.Kit.Random;

namespace DelveRpg.Kit.Combat;

/// <summary>What a blow or bolt is made of. Everything but physical is elemental.</summary>
public enum DamageType
{
    Physical,
    Magic,
    Fire,
    Ice,
    Lightning,
    Poison,
    Paralyze,
}

/// <summary>
/// The status effect an elemental hit leaves behind, keyed by damage type
/// like the donor's StatusEffect.getStatusEffect: fire sets the target
/// burning half the time, ice slows, poison poisons, paralysis paralyzes;
/// physical, magic and lightning leave nothing. Durations and damage cadence
/// are the donor effects' defaults ([donor] statuseffects/BurningEffect.java,
/// PoisonEffect.java, SlowEffect.java, ParalyzeEffect.java).
/// </summary>
public static class ElementalEffects
{
    public static void ApplyOnHit(DamageType damageType, EffectSet effects, IRandomSource random)
    {
        switch (damageType)
        {
            case DamageType.Fire when random.Next(0, 2) == 0:
                effects.Apply(EffectKind.Burning, 600, 1, intervalTicks: 160);
                break;
            case DamageType.Ice:
                effects.Apply(EffectKind.Slowed, 500, 0);
                break;
            case DamageType.Poison:
                effects.Apply(EffectKind.Poison, 1000, 1, intervalTicks: 160);
                break;
            case DamageType.Paralyze:
                effects.Apply(EffectKind.Paralyzed, 500, 0);
                break;
        }
    }
}
