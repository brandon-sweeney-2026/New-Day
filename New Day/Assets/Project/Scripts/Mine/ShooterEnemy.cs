using UnityEngine;

/// <summary>
/// An enemy that fights from a DISTANCE. It backs away if you get close, shuffles nearer if you are too far, and every few seconds
/// it stops, puffs up to warn you, and spits.
/// The same script makes very different enemies just by changing 'Shots Per Volley' and 'Spread Degrees':
///   1 shot             = one aimed shot (rock spitter)
///   3 shots, 40 spread = a fan of three (bubble spitter)
///   8 shots, 360       = a ring in every direction (cactus)
/// The damage of a shot is 'Contact Damage' (so it grows with depth like everything else). Every number is in the Inspector.
/// </summary>
public class ShooterEnemy : EnemyBase
{
    [Header("Art (the setup menu fills these in)")]
    public Sprite[] idleFrames;
    [Tooltip("Shown while it is puffed up, about to spit.")] public Sprite[] aimFrames;
    public float animFps = 3f;

    [Header("Movement")]
    public float moveSpeed = 1.6f;
    [Tooltip("It notices the player inside this distance.")] public float aggroRange = 9f;
    [Tooltip("It backs away when the player is closer than this.")] public float tooCloseRange = 3f;
    [Tooltip("It comes nearer when the player is farther than this.")] public float tooFarRange = 6.5f;

    [Header("Shooting")]
    [Tooltip("The thing it spits (a prefab with EnemyProjectile on it).")] public GameObject projectilePrefab;
    public float projectileSpeed = 5f;
    [Min(1)] public int shotsPerVolley = 1;
    [Range(0f, 360f)] [Tooltip("The shots are spread evenly over this many degrees. 360 = a full ring.")] public float spreadDegrees = 0f;
    [Tooltip("Seconds between volleys.")] public Vector2 secondsBetweenVolleys = new Vector2(2f, 3.2f);
    [Tooltip("Seconds it puffs up before spitting (your warning).")] public float warnSeconds = 0.5f;

    private enum State { Idle, Aim }
    private State state = State.Idle;
    private float timer;
    private float animTime;

    protected override void Awake()
    {
        base.Awake();
        if (enemyName == "Enemy") enemyName = "Spitter";
        timer = Random.Range(secondsBetweenVolleys.x, secondsBetweenVolleys.y);
    }

    protected override void Think(float dt)
    {
        Vector2 toPlayer = PlayerCenter - rb.position;
        float dist = toPlayer.magnitude;
        if (dist > aggroRange) { state = State.Idle; return; }          // too far away to care

        timer -= dt;
        if (state == State.Aim)
        {
            if (timer <= 0f)
            {
                Shoot(toPlayer);
                state = State.Idle;
                timer = Random.Range(secondsBetweenVolleys.x, secondsBetweenVolleys.y);
            }
            return;                                                     // stands still while aiming
        }

        if (dist < tooCloseRange) desiredVelocity = -toPlayer.normalized * moveSpeed;
        else if (dist > tooFarRange) desiredVelocity = toPlayer.normalized * moveSpeed;
        if (timer <= 0f) { state = State.Aim; timer = warnSeconds; }
    }

    private void Shoot(Vector2 toPlayer)
    {
        if (projectilePrefab == null) return;
        float aim = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
        bool ring = spreadDegrees >= 359f;
        for (int i = 0; i < shotsPerVolley; i++)
        {
            // spread the shots evenly: a fan is centred on the player, a ring goes all the way round
            float offset = 0f;
            if (shotsPerVolley > 1)
                offset = ring ? 360f * i / shotsPerVolley : Mathf.Lerp(-spreadDegrees * 0.5f, spreadDegrees * 0.5f, (float)i / (shotsPerVolley - 1));
            float angle = (aim + offset) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            Vector3 from = (Vector3)(rb.position + Vector2.up * 0.4f + direction * 0.3f);
            GameObject shot = Instantiate(projectilePrefab, from, Quaternion.identity, transform.parent);
            var projectile = shot.GetComponent<EnemyProjectile>();
            if (projectile != null) projectile.Launch(direction, projectileSpeed, contactDamage, gameObject);
        }
    }

    protected override void Animate()
    {
        if (sr == null) return;
        animTime += Time.deltaTime;
        Sprite[] set = state == State.Aim && aimFrames != null && aimFrames.Length > 0 ? aimFrames : idleFrames;
        Sprite s = Frame(set, animTime, animFps);
        if (s != null) sr.sprite = s;
        if (player != null) sr.flipX = player.position.x < rb.position.x;
    }
}
