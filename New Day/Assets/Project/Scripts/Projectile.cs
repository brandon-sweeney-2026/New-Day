using UnityEngine;

/// <summary>
/// A flying spell bolt. Created by Projectile.Spawn() (player spells, and later enemy spitters/mages).
/// It damages the first IDamageable of a different team that it touches, and disappears on walls.
/// Walls = any solid (non-trigger) Collider2D. Put your world colliders on those.
/// </summary>
public class Projectile : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float lifeLeft;
    private DamageInfo info;
    private float radius = 0.15f;
    private float knockback;
    private GameObject owner;

    public static Projectile Spawn(Vector2 position, Vector2 direction, float speed, float lifetime,
                                   int damage, float knockbackForce, Team team, GameObject owner,
                                   Sprite sprite, Color color, float size = 0.35f)
    {
        var go = new GameObject("Projectile");
        go.transform.position = position;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : RuntimeSprites.Circle;
        sr.color = sprite != null ? Color.white : color;
        sr.sortingOrder = 30;
        if (sprite == null) go.transform.localScale = Vector3.one * size;

        if (direction.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (sprite != null) go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        var p = go.AddComponent<Projectile>();
        p.direction = direction.normalized;
        p.speed = speed;
        p.lifeLeft = lifetime;
        p.owner = owner;
        p.knockback = knockbackForce;
        p.info = new DamageInfo(damage, Vector2.zero, team, owner);
        p.radius = 0.15f;
        return p;
    }

    private void Update()
    {
        lifeLeft -= Time.deltaTime;
        if (lifeLeft <= 0f) { Destroy(gameObject); return; }

        Vector2 step = direction * speed * Time.deltaTime;
        transform.position += (Vector3)step;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;

            var damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                if (damageable.DamageTeam == info.team) continue;    // no friendly fire
                DamageInfo d = info;
                d.knockback = direction * knockback;
                damageable.TakeDamage(d);
                Destroy(gameObject);
                return;
            }

            // Solid scenery stops the bolt. Triggers (pickups, interactables) are ignored.
            if (!hit.isTrigger)
            {
                Destroy(gameObject);
                return;
            }
        }
    }
}
