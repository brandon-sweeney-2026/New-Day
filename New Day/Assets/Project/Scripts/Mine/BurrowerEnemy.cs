using UnityEngine;

/// <summary>
/// An enemy that travels UNDERGROUND (moles and sand worms). While it is under the ground you only see a moving mound of dirt and
/// you cannot hit it. It tunnels toward you, the mound shakes for a moment as a warning, then it bursts up: now it can bite you
/// and you can hit it. After a few seconds it digs back down and does it again.
/// Watch the mound, step away when it shakes, and strike while the creature is up. Every number is in the Inspector.
/// </summary>
public class BurrowerEnemy : EnemyBase
{
    [Header("Art (the setup menu fills these in)")]
    [Tooltip("The mound of dirt shown while it is underground.")] public Sprite[] moundFrames;
    [Tooltip("The creature, shown while it is up.")] public Sprite[] upFrames;
    public float animFps = 5f;

    [Header("Underground")]
    public float burrowSpeed = 3.2f;
    [Tooltip("It starts tunnelling toward the player inside this distance.")] public float aggroRange = 9f;
    [Tooltip("It comes up when it gets this close to the player.")] public float surfaceRange = 1.1f;
    [Tooltip("Seconds the mound shakes before it bursts up (your warning).")] public float warnSeconds = 0.45f;

    [Header("Above ground")]
    [Tooltip("Seconds it stays up (this is when you can hit it).")] public float upSeconds = 2.2f;
    [Tooltip("Seconds it must stay underground before it can come up again.")] public float minBurrowSeconds = 1.2f;

    private enum State { Under, Warn, Up }
    private State state = State.Under;
    private float timer;
    private float animTime;
    private Collider2D hitbox;
    private Vector3 spriteHome;

    protected override void Awake()
    {
        base.Awake();
        if (enemyName == "Enemy") enemyName = "Burrower";
        hitbox = GetComponent<Collider2D>();
        timer = minBurrowSeconds;
        if (sr != null) spriteHome = sr.transform.localPosition;
    }

    protected override bool CanTouch() { return state == State.Up; }       // it can only bite while it is up

    protected override void Think(float dt)
    {
        Vector2 toPlayer = PlayerCenter - rb.position;
        float dist = toPlayer.magnitude;
        timer -= dt;

        switch (state)
        {
            case State.Under:
                if (dist < aggroRange && dist > surfaceRange * 0.6f) desiredVelocity = toPlayer.normalized * burrowSpeed;
                if (dist <= surfaceRange && timer <= 0f) { state = State.Warn; timer = warnSeconds; }
                break;

            case State.Warn:
                if (timer <= 0f) { state = State.Up; timer = upSeconds; }
                break;

            case State.Up:
                if (timer <= 0f) { state = State.Under; timer = minBurrowSeconds; }
                break;
        }

        // underground it cannot be hit: switching the collider off does that (the bat uses the same trick while it is high up)
        if (hitbox != null) hitbox.enabled = state == State.Up;
    }

    protected override void Animate()
    {
        if (sr == null) return;
        animTime += Time.deltaTime;
        Sprite s = Frame(state == State.Up ? upFrames : moundFrames, animTime, animFps);
        if (s != null) sr.sprite = s;
        // the mound trembles while it is about to burst up
        float shake = state == State.Warn ? Mathf.Sin(Time.time * 70f) * 0.05f : 0f;
        sr.transform.localPosition = spriteHome + new Vector3(shake, 0f, 0f);
    }
}
