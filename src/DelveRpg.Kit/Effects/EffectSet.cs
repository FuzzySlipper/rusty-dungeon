namespace DelveRpg.Kit.Effects;

public enum EffectKind
{
    Poison,
    Burning,
    Slowed,
    Hasted,
    Paralyzed,
}

/// <summary>One timed effect on an actor. Magnitude is the per-tick damage or speed factor.</summary>
public readonly record struct ActiveEffect(EffectKind Kind, int RemainingTicks, int Magnitude);

/// <summary>
/// The timed effects on one actor. Effects refresh their duration instead of
/// stacking, matching the donor's status-effect policy.
/// </summary>
public sealed class EffectSet
{
    private readonly List<ActiveEffect> _effects = new();

    public IReadOnlyList<ActiveEffect> Active => _effects;

    public void Apply(EffectKind kind, int durationTicks, int magnitude)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].Kind == kind)
            {
                _effects[i] = new ActiveEffect(kind, durationTicks, Math.Max(_effects[i].Magnitude, magnitude));
                return;
            }
        }

        _effects.Add(new ActiveEffect(kind, durationTicks, magnitude));
    }

    public bool IsActive(EffectKind kind) => _effects.Any(effect => effect.Kind == kind);

    /// <summary>Advance one tick; returns damage dealt by effects this tick.</summary>
    public int Tick()
    {
        int damage = 0;
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = _effects[i];
            if (effect.Kind is EffectKind.Poison or EffectKind.Burning)
            {
                damage += effect.Magnitude;
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
