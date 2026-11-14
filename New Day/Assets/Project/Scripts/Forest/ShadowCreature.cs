using UnityEngine;

/// <summary>
/// The polluted forest animals: a rabbit, squirrel, hedgehog, bird and turtle, each a dark shadow of itself with glowing eyes.
/// One script does them all - the "Style" decides how it moves, and the setup menu fills in good numbers for each animal.
///
/// Every animal follows the same simple rhythm so the player can learn it:
///   WANDER  - ambles about near home.
///   REST    - it spotted you; it pauses and keeps its eyes on you.
///   WARN    - a short, readable warning (it crouches / curls up / flaps) and LOCKS its direction.
///   GO      - a fast burst in that locked direction. Dodge sideways!   Then back to REST.
/// The turtle is the exception: it plods slowly and steadily (Style = Plod) and is very tough.
/// When one is banished it adds 1 to the counter "shadows_banished" (the Fairy's quests can use that).
/// </summary>
public class ShadowCreature : EnemyBase
{
    public enum Style { Hop, Dart, Roll, Dash, Plod }

    [Header("Animal")]
    public Style style = Style.Hop;

    [Header("Art (the setup menu fills these in)")]
    public Sprite[] moveFrames;         // faces right; flipped for left
    public Sprite warnFrame;            // crouch / curl / flap
    public float animFps = 8f;

    [Header("Movement")]
    public float wanderSpeed = 1.1f;
    public float goSpeed = 6f;
    [Tooltip("Seconds of the fast burst.")] public float goSeconds = 0.35f;
    [Tooltip("Seconds it rests (eyes on you) between bursts.")] public float restSeconds = 0.5f;
    [Tooltip("Seconds of the warning before a burst. This is your cue to dodge.")] public float warnSeconds = 0.25f;
    [Tooltip("On = it keeps turning to face you during the warning (it is aimed at the last moment). Off = it locks its direction the moment the warning starts, so you can dodge early.")] public bool aimBeforeEachBurst = true;
    [Tooltip("After it reaches you it runs away for one burst (squirrels hit and run).")] public bool hitAndRun = false;

    [Header("Senses")]
    public float aggroRange = 6.5f;
    public float loseRange = 10f;

    private enum State { Wander, Rest, Warn, Go }
    private State state = State.Wander;
    private float stateTimer, wanderTimer, animTime, stuckTimer;
    private bool wanderPaused, retreatNext;
    private Vector2 wanderDir, goDir, spawnPoint, faceDir = Vector2.right;
    private bool moving;

    protected override void Awake()
    {
        base.Awake();
        spawnPoint = transform.position;
        wanderDir = Random.insideUnitCircle.normalized;
        if (string.IsNullOrEmpty(enemyName) || enemyName == "Enemy") enemyName = "Shadow " + style;
    }

    protected override void Think(float dt)
    {
        Vector2 toPlayer = PlayerCenter - rb.position;
        float dist = toPlayer.magnitude;
        Vector2 dirToPlayer = dist > 0.01f ? toPlayer / dist : Vector2.right;
        float speed = 0f;
        Vector2 dir = Vector2.zero;
        moving = false;

        if (style == Style.Plod)
        {
            // the turtle: slow and steady, always toward you once it notices you
            if (state == State.Wander && dist < aggroRange) state = State.Rest;
            else if (state == State.Rest && dist > loseRange) state = State.Wander;
            if (state == State.Rest) { dir = dirToPlayer; speed = goSpeed; }
            else WanderStep(dt, out dir, out speed);
            Finish(dir, speed, dt);
            return;
        }

        switch (state)
        {
            case State.Wander:
                WanderStep(dt, out dir, out speed);
                if (dist < aggroRange) { state = State.Rest; stateTimer = restSeconds; }
                break;

            case State.Rest:
                faceDir = dirToPlayer;
                stateTimer -= dt;
                if (dist > loseRange) state = State.Wander;
                else if (stateTimer <= 0f) { state = State.Warn; stateTimer = warnSeconds; goDir = retreatNext ? -dirToPlayer : dirToPlayer; }
                break;

            case State.Warn:
                stateTimer -= dt;
                if (aimBeforeEachBurst && !retreatNext) { goDir = dirToPlayer; faceDir = goDir; }   // aims until the very last moment of the warning
                if (stateTimer <= 0f) { state = State.Go; stateTimer = goSeconds; faceDir = goDir; }
                break;

            case State.Go:
                dir = goDir; speed = goSpeed;
                stateTimer -= dt;
                bool reached = Vector2.Distance(rb.position + Vector2.up * 0.3f, PlayerCenter) < touchRadius + 0.15f;
                if (stateTimer <= 0f || (reached && hitAndRun))
                {
                    retreatNext = hitAndRun && reached;
                    state = State.Rest; stateTimer = restSeconds;
                }
                break;
        }
        Finish(dir, speed, dt);
    }

    private void WanderStep(float dt, out Vector2 dir, out float speed)
    {
        wanderTimer -= dt;
        if (wanderTimer <= 0f)
        {
            wanderTimer = Random.Range(1.2f, 3f);
            wanderPaused = Random.value < 0.35f;
            wanderDir = Vector2.Distance(rb.position, spawnPoint) > 3.5f ? (spawnPoint - rb.position).normalized : Random.insideUnitCircle.normalized;
        }
        dir = wanderDir;
        speed = wanderPaused ? 0f : wanderSpeed;
        if (speed > 0f) faceDir = dir;
    }

    private void Finish(Vector2 dir, float speed, float dt)
    {
        // stuck against a wall? pick a new way
        if (speed > 0.5f && rb.linearVelocity.magnitude < speed * 0.2f)
        {
            stuckTimer += dt;
            if (stuckTimer > 0.3f)
            {
                stuckTimer = 0f;
                wanderDir = Random.insideUnitCircle.normalized;
                if (state == State.Go) { state = State.Rest; stateTimer = restSeconds; }
            }
        }
        else stuckTimer = 0f;

        moving = speed > 0.05f;
        desiredVelocity = dir * speed;
    }

    protected override void Animate()
    {
        if (sr == null) return;
        if (Mathf.Abs(faceDir.x) > 0.1f) sr.flipX = faceDir.x < 0f;
        if (moving) animTime += Time.deltaTime;

        Sprite s;
        if (state == State.Warn && warnFrame != null) s = warnFrame;
        else if (moveFrames != null && moveFrames.Length > 0) s = moving ? Frame(moveFrames, animTime, animFps) : moveFrames[0];
        else s = null;
        if (s != null) sr.sprite = s;

        // little warning shake so it reads clearly (only if the picture is on a child object)
        if (sr.transform != transform)
            sr.transform.localPosition = new Vector3(state == State.Warn ? Mathf.Sin(Time.time * 70f) * 0.04f : 0f, sr.transform.localPosition.y, 0f);
    }

    protected override void Die()
    {
        GameState.AddCounter("shadows_banished");
        base.Die();
    }
}
