using UnityEngine;

/// <summary>One thing an enemy can drop when it dies.</summary>
[System.Serializable]
public class EnemyDrop
{
    public ItemData item;
    [Min(1)] public int min = 1;
    [Min(1)] public int max = 1;
    [Range(0f, 1f)] [Tooltip("1 = always, 0.25 = one time in four.")] public float chance = 1f;
}

/// <summary>
/// The shared part of every enemy: health, getting hit (your sword and spells hurt it), knockback, a flash when hurt,
/// touching the player hurts them, drops when it dies, and a soft shadow on the ground.
/// You don't add this directly - use BugEnemy, BatEnemy and so on, which build on it. Everything below shows in their Inspector.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public string enemyName = "Enemy";
    [Min(1)] public int maxHealth = 10;
    [Tooltip("How much it hurts the player when it touches them.")]
    [Min(0)] public int contactDamage = 3;
    public float contactKnockback = 6f;
    [Tooltip("Seconds between touches.")] public float touchCooldown = 0.9f;
    [Tooltip("How big a gap (world units) between the enemy's collider and the player's collider still counts as touching. " +
             "0 = the colliders must really touch. A little above 0 feels fair, because the two solid bodies push each other apart.")]
    [Min(0f)] public float touchReach = 0.12f;
    [Tooltip("Only used if the enemy or the player has no collider: how close (world units) their middles must be to touch.")]
    public float touchRadius = 0.6f;

    [Header("When it is hit")]
    [Range(0f, 1f)] [Tooltip("0 = flies back a long way, 1 = can't be pushed.")] public float knockbackResist = 0f;
    [Tooltip("Seconds it freezes when hit.")] public float stunSeconds = 0.25f;
    public Color hurtColor = new Color(1f, 0.45f, 0.45f);

    [Header("Drops")]
    public EnemyDrop[] drops;
    [Min(0)] public int goldMin = 0;
    [Min(0)] public int goldMax = 0;

    [Header("Performance")]
    [Tooltip("Enemies farther than this from the player stand still (saves speed).")] public float sleepDistance = 22f;

    [Header("Ground shadow")]
    public Vector2 shadowSize = new Vector2(0.8f, 0.3f);
    public Vector2 shadowOffset = new Vector2(0f, 0.05f);

    public Team DamageTeam { get { return Team.Enemy; } }
    public bool IsDead { get; private set; }
    public float Health { get; private set; }

    protected Rigidbody2D rb;
    protected SpriteRenderer sr;
    protected Transform player;
    protected Vector2 desiredVelocity;
    protected Vector2 knockVelocity;
    protected float stunUntil;

    private SpriteRenderer blob;
    private Collider2D bodyCollider;      // this enemy's own collider (on the root object)
    private Collider2D playerCollider;    // the player's solid collider
    private float nextTouch;
    private float flashUntil;
    private Color baseColor = Color.white;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        Health = maxHealth;
        bodyCollider = GetComponent<Collider2D>();

        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;

        // our own soft shadow (the sun-shadow system leaves enemies to this)
        gameObject.AddComponent<LightingExempt>();
        var sg = new GameObject("GroundShadow");
        sg.transform.SetParent(transform, false);
        sg.transform.localPosition = shadowOffset;
        sg.transform.localScale = new Vector3(shadowSize.x, shadowSize.y, 1f);
        blob = sg.AddComponent<SpriteRenderer>();
        blob.sprite = RuntimeSprites.Circle;
        blob.color = new Color(0f, 0f, 0f, 0.3f);
    }

    protected virtual void Update()
    {
        if (IsDead) return;
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
            if (player == null) return;
            playerCollider = FindPlayerCollider(player);
        }

        UpdateVisualsCommon();

        if (Vector2.Distance(rb.position, player.position) > sleepDistance) { desiredVelocity = Vector2.zero; return; }

        desiredVelocity = Vector2.zero;
        if (Time.time >= stunUntil) Think(Time.deltaTime);
        Animate();
        CheckTouch();
    }

    private void FixedUpdate()
    {
        if (IsDead || rb == null) return;
        knockVelocity = Vector2.MoveTowards(knockVelocity, Vector2.zero, 25f * Time.fixedDeltaTime);
        Vector2 move = Time.time < stunUntil ? Vector2.zero : desiredVelocity;
        rb.linearVelocity = move + knockVelocity;
    }

    /// <summary>Called every frame while the enemy is awake and not stunned. Set 'desiredVelocity' here.</summary>
    protected abstract void Think(float dt);
    /// <summary>Pick the right sprite frame.</summary>
    protected abstract void Animate();

    protected virtual void OnHurt(DamageInfo info) { }
    protected virtual void OnHitPlayer() { }
    /// <summary>The place the player's body is (a bit above their feet).</summary>
    protected Vector2 PlayerCenter { get { return (Vector2)player.position + Vector2.up * 0.4f; } }

    private void CheckTouch()
    {
        if (contactDamage <= 0 || Time.time < nextTouch || !CanTouch()) return;
        if (!IsTouchingPlayer()) return;
        HurtPlayer();
    }

    /// <summary>The player's solid (non-trigger) collider, or any collider if it has no solid one.</summary>
    private static Collider2D FindPlayerCollider(Transform p)
    {
        Collider2D any = null;
        foreach (Collider2D c in p.GetComponents<Collider2D>())
        {
            if (!c.isTrigger) return c;
            if (any == null) any = c;
        }
        return any;
    }

    /// <summary>
    /// True when this enemy is touching the player.
    /// It measures the real gap between the two colliders, so it works the same from every side, corners (diagonals) included.
    /// (The old way measured between two fixed points, and the solid bodies stopped the enemy before those points got close
    /// enough when it came from below or from a diagonal tile.)
    /// 'extraReach' adds to Touch Reach for special attacks (the bat's swoop).
    /// </summary>
    protected bool IsTouchingPlayer(float extraReach = 0f)
    {
        if (player == null) return false;
        if (playerCollider == null) playerCollider = FindPlayerCollider(player);

        if (bodyCollider != null && playerCollider != null && bodyCollider.enabled && playerCollider.enabled)
        {
            ColliderDistance2D gap = bodyCollider.Distance(playerCollider);
            if (gap.isValid) return gap.distance <= touchReach + extraReach;   // distance is negative when they overlap
        }

        // no usable colliders: fall back to the distance between their middles
        return Vector2.Distance(rb.position + Vector2.up * 0.3f, PlayerCenter) <= touchRadius + extraReach;
    }

    /// <summary>The direction to push the player: from the middle of this enemy's body to the middle of the player's body.</summary>
    protected Vector2 PushDirection()
    {
        Vector2 from = bodyCollider != null && bodyCollider.enabled ? (Vector2)bodyCollider.bounds.center : rb.position + Vector2.up * 0.3f;
        Vector2 to = playerCollider != null && playerCollider.enabled ? (Vector2)playerCollider.bounds.center : PlayerCenter;
        Vector2 dir = to - from;
        if (dir.sqrMagnitude < 0.0001f) dir = rb.linearVelocity.sqrMagnitude > 0.01f ? rb.linearVelocity : Vector2.down;   // right on top: push the way it was moving
        return dir.normalized;
    }

    /// <summary>Override to stop touching from hurting (the bat can only hurt while it is low).</summary>
    protected virtual bool CanTouch() { return true; }

    protected void HurtPlayer()
    {
        var target = player.GetComponentInParent<IDamageable>();
        if (target == null) target = player.GetComponent<IDamageable>();
        if (target == null || target.DamageTeam != Team.Player) return;
        // The strength of this push is turned into a distance by PlayerStats (Knockback Distance).
        Vector2 push = PushDirection() * contactKnockback;
        target.TakeDamage(new DamageInfo(contactDamage, push, Team.Enemy, gameObject));
        nextTouch = Time.time + touchCooldown;
        OnHitPlayer();
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || info.team == Team.Enemy) return;
        // Hits from the player get the damage bonus from perks and equipment (see PlayerBonuses).
        Health -= info.team == Team.Player ? PlayerBonuses.ScaleDamage(info.amount) : info.amount;
        knockVelocity = info.knockback * (1f - knockbackResist);
        stunUntil = Time.time + stunSeconds;
        flashUntil = Time.time + 0.18f;
        OnHurt(info);
        if (Health <= 0f) Die();
    }

    protected virtual void Die()
    {
        IsDead = true;
        if (drops != null)
            foreach (EnemyDrop d in drops)
            {
                if (d == null || d.item == null || Random.value > d.chance) continue;
                ItemPickup.Spawn(d.item, Random.Range(d.min, Mathf.Max(d.min, d.max) + 1), transform.position + Vector3.up * 0.2f);
            }
        if (goldMax > 0) GameState.AddGold(Random.Range(goldMin, goldMax + 1));
        GameState.AddCounter("killed_" + enemyName);
        Destroy(gameObject);
    }

    private void UpdateVisualsCommon()
    {
        if (sr != null) sr.color = Time.time < flashUntil ? hurtColor : baseColor;
        if (blob != null)
        {
            float a = LightingManager.Instance != null ? LightingManager.Instance.ShadowAlpha : 0.3f;
            blob.color = new Color(0f, 0f, 0f, Mathf.Clamp(a * 0.9f, 0.05f, 0.45f));
            blob.sortingOrder = ShadowOrder();
            blob.sortingLayerID = sr != null ? sr.sortingLayerID : blob.sortingLayerID;
        }
    }

    /// <summary>Sorting order of the ground shadow (just under the enemy by default).</summary>
    protected virtual int ShadowOrder() { return sr != null ? sr.sortingOrder - 1 : 0; }

    /// <summary>The sprite's picture for a frame list, looping at 'fps'.</summary>
    protected static Sprite Frame(Sprite[] frames, float time, float fps)
    {
        if (frames == null || frames.Length == 0) return null;
        return frames[Mathf.FloorToInt(time * fps) % frames.Length];
    }

    protected void SetShadow(float scale, float alpha01 = 1f)
    {
        if (blob == null) return;
        blob.transform.localScale = new Vector3(shadowSize.x * scale, shadowSize.y * scale, 1f);
    }

    protected void SetShadowLocal(Vector2 localPos)
    {
        if (blob != null) blob.transform.localPosition = localPos;
    }
}
