using UnityEngine;

/// <summary>
/// The mountain bat. Black and brown with yellow eyes.
///
/// Behaviour: it flutters HIGH above the player in a loose circle (you can't hit it up there). Every few seconds it pauses, wobbles
/// to warn you, then SWOOPS down in a curve, brushes past you low (it can hurt you AND be hit now), and climbs away again.
/// Its shadow on the ground shows where it is. Every number is in the Inspector.
///
/// Prefab layout (the setup menu builds it): root = this script + Rigidbody2D + a trigger CircleCollider2D; child "Body" = the bat picture.
/// </summary>
public class BatEnemy : EnemyBase
{
    [Header("Art")]
    [Tooltip("The child object that holds the bat picture. It is lifted up and down to show flying height.")]
    public Transform body;
    public Sprite[] flapFrames;
    public Sprite[] diveFrames;
    public float flapFps = 11f;

    [Header("Flying")]
    [Tooltip("How high above the ground it flies while circling (world units).")] public float cruiseHeight = 2.2f;
    public float orbitRadius = 3f;
    public float orbitDegreesPerSecond = 55f;
    public float flySpeed = 4.2f;
    public float aggroRange = 9f;

    [Header("Swoop")]
    public Vector2 timeBetweenSwoops = new Vector2(2.2f, 3.8f);
    public float warnSeconds = 0.5f;
    [Tooltip("How fast it flies during a swoop (world units per second). The swoop always moves at this speed, " +
             "so a longer swoop simply takes longer.")]
    [Min(0.5f)] public float swoopSpeed = 6.5f;
    [Tooltip("It only starts a swoop when you are this close (world units). Farther away, it flies toward you first. " +
             "If you run off during the warning, the swoop falls short instead of rocketing after you.")]
    [Min(1f)] public float maxSwoopDistance = 5f;
    [Tooltip("The shortest swoop (world units, start to finish), so a swoop from right above you still has a visible dive.")]
    [Min(0.5f)] public float minSwoopLength = 3f;
    [Tooltip("Extra reach (world units) for its swoop to hit you, on top of Touch Reach.")]
    [Min(0f)] public float swoopHitReach = 0.15f;
    [Tooltip("Lowest point of the swoop (world units above the ground).")] public float lowHeight = 0.25f;
    [Tooltip("Below this height it can be hit and can hurt the player.")] public float vulnerableHeight = 1.1f;
    public float recoverSeconds = 0.9f;

    private enum State { Idle, Circle, Warn, Swoop, Recover }
    private State state = State.Idle;
    private float height;
    private float orbitAngle;
    private float orbitDir = 1f;
    private float stateTimer;
    private float swoopCountdown;
    private Vector2 swoopStart, swoopEnd;
    private float swoopDuration;          // seconds this swoop takes = its length / swoopSpeed
    private Vector2 swoopLastPos;         // where it was last frame (to catch you even if it moves far in one frame)
    private bool swoopHitDone;
    private Vector2 home;
    private float animTime;
    private Collider2D hitbox;

    protected override void Awake()
    {
        base.Awake();
        if (enemyName == "Enemy") enemyName = "Bat";
        hitbox = GetComponent<Collider2D>();
        if (hitbox != null) hitbox.isTrigger = true;
        height = cruiseHeight;
        home = transform.position;
        orbitAngle = Random.Range(0f, 360f);
        orbitDir = Random.value < 0.5f ? -1f : 1f;
        swoopCountdown = Random.Range(timeBetweenSwoops.x, timeBetweenSwoops.y);
        if (body == null && sr != null) body = sr.transform;
        if (sr != null) sr.sortingOrder = 24;      // flies above everything
    }

    // the bat's shadow lies on the ground, far below the bat itself
    protected override int ShadowOrder() { return LightingManager.Instance != null ? LightingManager.Instance.ShadowSortingOrder + 1 : 4; }

    protected override bool CanTouch() { return false; }   // the bat hurts only during a swoop (handled below)

    protected override void Think(float dt)
    {
        Vector2 pos = rb.position;
        float dist = Vector2.Distance(pos, PlayerCenter);

        switch (state)
        {
            case State.Idle:
                height = cruiseHeight + Mathf.Sin(Time.time * 2f) * 0.15f;
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    stateTimer = Random.Range(1.2f, 2.5f);
                    Vector2 around = home + Random.insideUnitCircle * 3f;
                    desiredVelocity = (around - pos).normalized * flySpeed * 0.4f;
                }
                if (dist < aggroRange) { state = State.Circle; swoopCountdown = Random.Range(timeBetweenSwoops.x, timeBetweenSwoops.y); }
                break;

            case State.Circle:
                orbitAngle += orbitDir * orbitDegreesPerSecond * dt;
                Vector2 anchor = (Vector2)player.position + new Vector2(Mathf.Cos(orbitAngle * Mathf.Deg2Rad), Mathf.Sin(orbitAngle * Mathf.Deg2Rad) * 0.6f) * orbitRadius;
                Vector2 to = anchor - pos;
                desiredVelocity = Vector2.ClampMagnitude(to * 3f, flySpeed);
                height = cruiseHeight + Mathf.Sin(Time.time * 3f) * 0.18f;
                swoopCountdown -= dt;
                if (dist > aggroRange * 1.6f) { state = State.Idle; home = pos; }
                else if (swoopCountdown <= 0f)
                {
                    // ready to swoop: only if you are close enough, otherwise fly straight toward you first
                    if (dist <= maxSwoopDistance) { state = State.Warn; stateTimer = warnSeconds; }
                    else desiredVelocity = (PlayerCenter - pos).normalized * flySpeed;
                }
                break;

            case State.Warn:
                desiredVelocity = Vector2.zero;
                height = cruiseHeight + 0.35f + Mathf.Sin(Time.time * 40f) * 0.04f;       // rears up and trembles
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    swoopStart = pos;
                    Vector2 toTarget = PlayerCenter - swoopStart;                          // where you are NOW: keep moving!
                    // If you ran off during the warning, it aims only as far as Max Swoop Distance (the swoop falls short).
                    toTarget = Vector2.ClampMagnitude(toTarget, maxSwoopDistance);
                    if (toTarget.sqrMagnitude < 0.0001f) toTarget = Random.insideUnitCircle.normalized * 0.1f;
                    Vector2 path = toTarget / 0.55f;                                       // passes you at 55% of the swoop
                    if (path.magnitude < minSwoopLength) path = path.normalized * minSwoopLength;
                    swoopEnd = swoopStart + path;
                    // Fixed speed: the time grows with the length. (Before, every swoop took the same time,
                    // so a long swoop had to fly much faster.)
                    swoopDuration = path.magnitude / swoopSpeed;
                    swoopLastPos = pos;
                    state = State.Swoop; stateTimer = 0f; swoopHitDone = false;
                }
                break;

            case State.Swoop:
                stateTimer += dt;
                float t = Mathf.Clamp01(stateTimer / Mathf.Max(0.05f, swoopDuration));
                Vector2 wanted = Vector2.Lerp(swoopStart, swoopEnd, t);
                // follow the path; allowed a little faster than swoopSpeed only to catch up after a bump
                desiredVelocity = Vector2.ClampMagnitude((wanted - pos) / Mathf.Max(dt, 0.001f), swoopSpeed * 1.5f);
                float u = t < 0.55f ? (0.55f - t) / 0.55f : (t - 0.55f) / 0.45f;          // 1 at the ends, 0 at the lowest point
                height = lowHeight + (cruiseHeight + 0.35f - lowHeight) * u * u;

                if (!swoopHitDone && height < vulnerableHeight && SwoopReachesPlayer(swoopLastPos, pos))
                {
                    swoopHitDone = true;
                    HurtPlayer();
                }
                swoopLastPos = pos;
                if (t >= 1f) { state = State.Recover; stateTimer = recoverSeconds; }
                break;

            case State.Recover:
                height = Mathf.MoveTowards(height, cruiseHeight, 3f * dt);
                Vector2 away = pos - PlayerCenter;
                desiredVelocity = away.normalized * flySpeed * 0.7f;
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    state = State.Circle;
                    swoopCountdown = Random.Range(timeBetweenSwoops.x, timeBetweenSwoops.y);
                    orbitDir = Random.value < 0.5f ? -1f : 1f;
                    Vector2 rel = pos - (Vector2)player.position;
                    orbitAngle = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
                }
                break;
        }

        // lift the picture to show height, shrink the shadow the higher it is, and only be hittable when low
        if (body != null) body.localPosition = new Vector3(0f, height, 0f);
        SetShadow(Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(height / (cruiseHeight + 0.4f))));
        if (hitbox != null) hitbox.enabled = height < vulnerableHeight;
    }

    /// <summary>
    /// True if the swooping bat touches the player: its collider touches the player's collider, or its path since last frame
    /// passed close to the player (so a fast swoop can't skip past you between two frames).
    /// </summary>
    private bool SwoopReachesPlayer(Vector2 from, Vector2 to)
    {
        if (IsTouchingPlayer(swoopHitReach)) return true;
        Vector2 seg = to - from;
        float len2 = seg.sqrMagnitude;
        float k = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(PlayerCenter - from, seg) / len2) : 0f;
        Vector2 closest = from + seg * k;                                     // the point on the path nearest the player
        return Vector2.Distance(closest, PlayerCenter) < touchRadius + swoopHitReach;
    }

    protected override void Animate()
    {
        if (sr == null) return;
        bool diving = state == State.Swoop;
        animTime += Time.deltaTime * (state == State.Warn ? 2f : 1f);
        Sprite[] set = diving && diveFrames != null && diveFrames.Length > 0 ? diveFrames : flapFrames;
        Sprite s = Frame(set, animTime, diving ? flapFps * 0.7f : flapFps);
        if (s != null) sr.sprite = s;
        if (Mathf.Abs(rb.linearVelocity.x) > 0.3f) sr.flipX = rb.linearVelocity.x < 0f;
    }

    protected override void OnHurt(DamageInfo info)
    {
        // being hit knocks it off its attack
        if (state == State.Swoop || state == State.Warn) { state = State.Recover; stateTimer = recoverSeconds; }
    }
}
