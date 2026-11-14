using UnityEngine;

/// <summary>
/// The six-legged mountain bug. Dark grey with yellow eyes.
///
/// Behaviour: it scuttles about until it spots you, then comes for you FAST in a straight line - but it is SLOW TO TURN, so you can
/// dodge sideways and let it overshoot. When it lines up with you it does a short, fast CHARGE it cannot steer, then is tired for a moment.
/// (Hit it then!) Every number is in the Inspector.
/// </summary>
public class BugEnemy : EnemyBase
{
    [Header("Art (the setup menu fills these in)")]
    public Sprite[] sideFrames;      // faces right
    public Sprite[] downFrames;
    public Sprite[] upFrames;
    public float animFps = 10f;

    [Header("Movement")]
    public float wanderSpeed = 1.2f;
    public float chaseSpeed = 4.5f;
    public float chargeSpeed = 7.5f;
    [Tooltip("How fast it can turn while chasing (degrees per second). Low = easy to dodge.")]
    public float turnRate = 80f;
    [Tooltip("Turning while wandering.")] public float wanderTurnRate = 70f;
    [Range(0.2f, 1f)] [Tooltip("It slows to this fraction of its speed when it has to turn all the way round.")]
    public float turnSlowdown = 0.55f;

    [Header("Senses")]
    public float aggroRange = 7f;
    public float loseRange = 11f;

    [Header("Charge")]
    public float chargeRange = 5f;
    [Tooltip("Must be facing you within this many degrees to charge.")] public float chargeAlignDegrees = 20f;
    public float chargeSeconds = 0.7f;
    public float tiredSeconds = 0.9f;
    public float chargeCooldown = 1.5f;

    private enum State { Wander, Chase, Charge, Tired }
    private State state = State.Wander;
    private float heading;            // degrees, 0 = right, 90 = up
    private float wanderHeading;
    private float wanderTimer;
    private bool wanderPaused;
    private float stateTimer;
    private float nextCharge;
    private float stuckTimer;
    private float animTime;
    private bool moving;
    private Vector2 spawnPoint;

    protected override void Awake()
    {
        base.Awake();
        heading = Random.Range(0f, 360f);
        wanderHeading = heading;
        spawnPoint = transform.position;
        if (string.IsNullOrEmpty(enemyName) || enemyName == "Enemy") enemyName = "Bug";
    }

    private static Vector2 Dir(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }

    private void TurnToward(float target, float rate, float dt)
    {
        float d = Mathf.DeltaAngle(heading, target);
        heading += Mathf.Clamp(d, -rate * dt, rate * dt);
    }

    protected override void Think(float dt)
    {
        Vector2 toPlayer = PlayerCenter - rb.position;
        float dist = toPlayer.magnitude;
        float toPlayerAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
        float speed = 0f;

        switch (state)
        {
            case State.Wander:
                wanderTimer -= dt;
                if (wanderTimer <= 0f)
                {
                    wanderTimer = Random.Range(1.5f, 3.5f);
                    wanderPaused = Random.value < 0.3f;
                    // drift back toward home if it has wandered far
                    wanderHeading = Vector2.Distance(rb.position, spawnPoint) > 6f
                        ? Mathf.Atan2(spawnPoint.y - rb.position.y, spawnPoint.x - rb.position.x) * Mathf.Rad2Deg
                        : Random.Range(0f, 360f);
                }
                TurnToward(wanderHeading, wanderTurnRate, dt);
                speed = wanderPaused ? 0f : wanderSpeed;
                if (dist < aggroRange) { state = State.Chase; }
                break;

            case State.Chase:
                float delta = Mathf.DeltaAngle(heading, toPlayerAngle);
                TurnToward(toPlayerAngle, turnRate, dt);
                speed = chaseSpeed * Mathf.Lerp(1f, turnSlowdown, Mathf.Abs(delta) / 180f);
                if (dist > loseRange) state = State.Wander;
                else if (dist < chargeRange && Mathf.Abs(delta) < chargeAlignDegrees && Time.time >= nextCharge)
                {
                    state = State.Charge; stateTimer = chargeSeconds;
                }
                break;

            case State.Charge:
                speed = chargeSpeed;                    // no steering at all
                stateTimer -= dt;
                if (stateTimer <= 0f) { state = State.Tired; stateTimer = tiredSeconds; nextCharge = Time.time + chargeCooldown; }
                break;

            case State.Tired:
                speed = 0f;
                stateTimer -= dt;
                if (stateTimer <= 0f) state = dist < loseRange ? State.Chase : State.Wander;
                break;
        }

        // stuck on a rock or wall? turn away
        if (speed > 0.5f && rb.linearVelocity.magnitude < speed * 0.25f)
        {
            stuckTimer += dt;
            if (stuckTimer > 0.35f)
            {
                stuckTimer = 0f;
                heading += Random.Range(100f, 180f) * (Random.value < 0.5f ? -1f : 1f);
                wanderHeading = heading;
                if (state == State.Charge) { state = State.Tired; stateTimer = tiredSeconds; nextCharge = Time.time + chargeCooldown; }
            }
        }
        else stuckTimer = 0f;

        moving = speed > 0.05f;
        desiredVelocity = Dir(heading) * speed;
    }

    protected override void Animate()
    {
        if (sr == null) return;
        if (moving) animTime += Time.deltaTime * (state == State.Charge ? 1.6f : 1f);
        Vector2 d = Dir(heading);

        Sprite[] set;
        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) { set = sideFrames; sr.flipX = d.x < 0f; }
        else { set = d.y > 0f ? upFrames : downFrames; sr.flipX = false; }

        Sprite s = moving ? Frame(set, animTime, animFps) : (set != null && set.Length > 0 ? set[0] : null);
        if (s != null) sr.sprite = s;
    }
}
