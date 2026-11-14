using UnityEngine;

/// <summary>Who a hit belongs to. Attacks only hurt the other teams.</summary>
public enum Team { Neutral, Player, Enemy }

/// <summary>Everything a hit carries.</summary>
public struct DamageInfo
{
    public int amount;
    public Vector2 knockback;   // a velocity pushed onto the target
    public Team team;           // team of the attacker
    public GameObject source;
    public bool crit;           // true for a critical strike (shown by the damage numbers)

    public DamageInfo(int amount, Vector2 knockback, Team team, GameObject source)
    {
        this.amount = amount;
        this.knockback = knockback;
        this.team = team;
        this.source = source;
        this.crit = false;
    }
}

/// <summary>Implement this on anything that can be hurt (player, enemies, breakable crates...).</summary>
public interface IDamageable
{
    Team DamageTeam { get; }
    void TakeDamage(DamageInfo info);
}
