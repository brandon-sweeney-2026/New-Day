using UnityEngine;

/// <summary>
/// Something an enemy spits or throws (a pebble, a bubble, a fireball). ShooterEnemy makes these and calls Launch.
/// It flies in a straight line, hurts the player if it touches them, and breaks on walls and rocks or after a few seconds.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    [Tooltip("Seconds before it fizzles out by itself.")] public float lifeSeconds = 4f;
    [Tooltip("How close (world units) it has to get to the player to hit.")] public float hitRadius = 0.4f;
    [Tooltip("How hard it pushes the player.")] public float knockback = 4f;
    [Tooltip("Degrees per second it spins (only for looks).")] public float spinSpeed = 360f;

    private Vector2 velocity;
    private int damage;
    private GameObject owner;
    private Transform player;
    private float dieAt;

    /// <summary>Sends it flying. 'direction' does not need to be length 1.</summary>
    public void Launch(Vector2 direction, float speed, int damageAmount, GameObject shooter)
    {
        velocity = direction.normalized * speed;
        damage = damageAmount;
        owner = shooter;
        dieAt = Time.time + lifeSeconds;
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
    }

    private void Update()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        if (Time.time >= dieAt) { Destroy(gameObject); return; }

        // hit the player? (the player's body is a little above their feet)
        if (player != null)
        {
            Vector2 body = (Vector2)player.position + Vector2.up * 0.4f;
            if (Vector2.Distance(transform.position, body) <= hitRadius)
            {
                var target = player.GetComponentInParent<IDamageable>();
                if (target != null && target.DamageTeam == Team.Player)
                    target.TakeDamage(new DamageInfo(damage, velocity.normalized * knockback, Team.Enemy, owner != null ? owner : gameObject));
                Destroy(gameObject);
                return;
            }
        }

        // hit something solid (a wall, a rock)? Triggers, enemies and the player don't count.
        foreach (Collider2D c in Physics2D.OverlapPointAll(transform.position))
        {
            if (c.isTrigger || c.GetComponentInParent<EnemyBase>() != null || c.GetComponentInParent<PlayerController>() != null) continue;
            Destroy(gameObject);
            return;
        }
    }
}
