namespace DelveRpg.Kit.Effects;

public enum EffectKind
{
    Poison,
    Burning,
    Slowed,
    Hasted,
    Paralyzed,

    /// <summary>Halves non-physical damage taken (the donor's Resist Magic ShieldEffect).</summary>
    MagicResist,

    /// <summary>Halves physical damage taken (the donor's Iron Skin, damageMod 0.5).</summary>
    IronSkin,

    /// <summary>Heals its magnitude once per interval (the donor's RestoreHealthEffect).</summary>
    Regenerating,
}

/// <summary>
/// One timed effect on an actor. Magnitude is the damage dealt every
/// <see cref="IntervalTicks"/> ticks (poison, burning) or the effect's strength.
/// </summary>
public readonly record struct ActiveEffect(EffectKind Kind, int RemainingTicks, int Magnitude, int IntervalTicks = 1, int ElapsedTicks = 0);

/// <summary>
/// The timed effects on one actor. Effects refresh their duration instead of
/// stacking, matching the donor's status-effect policy.
/// </summary>
public sealed class EffectSet
{
    private readonly List<ActiveEffect> _effects = new();

    public IReadOnlyList<ActiveEffect> Active => _effects;

    public void Apply(EffectKind kind, int durationTicks, int magnitude, int intervalTicks = 1)
    {
        intervalTicks = Math.Max(1, intervalTicks);
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].Kind == kind)
            {
                _effects[i] = _effects[i] with
                {
                    RemainingTicks = durationTicks,
                    Magnitude = Math.Max(_effects[i].Magnitude, magnitude),
                    IntervalTicks = intervalTicks,
                };
                return;
            }
        }

        _effects.Add(new ActiveEffect(kind, durationTicks, magnitude, intervalTicks));
    }

    public bool IsActive(EffectKind kind) => _effects.Any(effect => effect.Kind == kind);

    /// <summary>End every effect (the donor's Restoration potion).</summary>
    public void Clear() => _effects.Clear();

    /// <summary>
    /// Advance one tick; returns damage dealt by effects this tick, negative
    /// when regeneration heals more. Burning and poison strike once per
    /// interval; poison never takes the last hit point, like the donor's
    /// non-lethal PoisonEffect.
    /// </summary>
    public int Tick(int currentHp)
    {
        int damage = 0;
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = _effects[i] with { ElapsedTicks = _effects[i].ElapsedTicks + 1 };
            if (effect.Kind is EffectKind.Poison or EffectKind.Burning
                && effect.ElapsedTicks % effect.IntervalTicks == 0)
            {
                bool spares = effect.Kind == EffectKind.Poison && currentHp - damage - effect.Magnitude <= 0;
                damage += spares ? 0 : effect.Magnitude;
            }
            else if (effect.Kind == EffectKind.Regenerating && effect.ElapsedTicks % effect.IntervalTicks == 0)
            {
                damage -= effect.Magnitude;
            }

            if (effect.RemainingTicks <= 1)
            {
                _effects.RemoveAt(i);
            }
            else
            {
                _effects[i] = effect with { RemainingTicks = effect.RemainingTicks - 1 };
            }
        }

        return damage;
    }

    /// <summary>Speed multiplier from stacked slow/haste. Slow wins ties.</summary>
    public float SpeedMultiplier()
    {
        float multiplier = 1f;
        if (IsActive(EffectKind.Slowed))
        {
            multiplier *= 0.5f;
        }

        if (IsActive(EffectKind.Hasted))
        {
            multiplier *= 1.5f;
        }

        return multiplier;
    }

    public bool IsParalyzed => IsActive(EffectKind.Paralyzed);
}
