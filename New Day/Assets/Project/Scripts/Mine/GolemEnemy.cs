using UnityEngine;

/// <summary>
/// A golem: big, slow and very hard to push around. It plods toward you, and when it is close it raises its arms
/// (a red circle grows on the ground to show where the blow will land) and SLAMS. Everything inside the circle is hurt.
/// After a slam it is worn out for a moment: that is the time to hit it. Walk out of the circle while it winds up.
/// Every number is in the Inspector.
/// </summary>
public class GolemEnemy : EnemyBase
{
    [Header("Art (the setup menu fills these in)")]
    public Sprite[] walkFrames;
    [Tooltip("Arms raised, about to slam.")] public Sprite windupFrame;
    [Tooltip("Arms on the ground, just after the slam.")] public Sprite slamFrame;
    public float animFps = 4f;

    [Header("Movement")]
    public float walkSpeed = 1.3f;
    public float aggroRange = 9f;

    [Header("Slam")]
    [Tooltip("It starts a slam when the player is this close.")] public float slamStartRange = 2f;
    [Tooltip("Everything inside this distance is hurt by the slam.")] public float slamRadius = 2.4f;
    [Tooltip("Seconds it winds up (your warning).")] public float windupSeconds = 0.9f;
    [Tooltip("Seconds it is worn out afterwards.")] public float recoverSeconds = 1.3f;
    public Color warningColor = new Color(1f, 0.25f, 0.15f, 0.3f);

    private enum State { Walk, Windup, Recover }
    private State state = State.Walk;
    private float timer;
    private float animTime;
    private SpriteRenderer warning;

    protected override void Awake()
    {
        base.Awake();
        if (enemyName == "Enemy") enemyName = "Golem";

        // the red warning circle on the ground (hidden until a slam starts)
        var go = new GameObject("SlamWarning");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        go.AddComponent<LightingExempt>();
        warning = go.AddComponent<SpriteRenderer>();
        warning.sprite = RuntimeSprites.Circle;
        warning.color = warningColor;
        warning.enabled = false;
    }

    protected override void Think(float dt)
    {
        Vector2 toPlayer = PlayerCenter - rb.position;
        float dist = toPlayer.magnitude;
        timer -= dt;

        switch (state)
        {
            case State.Walk:
                if (dist < aggroRange) desiredVelocity = toPlayer.normalized * walkSpeed;
                if (dist < slamStartRange) { state = State.Windup; timer = windupSeconds; }
                break;

            case State.Windup:
                if (timer <= 0f)
                {
                    // the blow lands: hurt the player if they are still inside the circle
                    if (dist <= slamRadius) HurtPlayer();
                    CameraFollow2D.Shake(0.15f, 0.2f);
                    state = State.Recover; timer = recoverSeconds;
                }
                break;

            case State.Recover:
                if (timer <= 0f) state = State.Walk;
                break;
        }

        // the circle grows from nothing to its full size during the wind-up
        warning.enabled = state == State.Windup;
        if (warning.enabled)
        {
            float grown = 1f - Mathf.Clamp01(timer / Mathf.Max(0.01f, windupSeconds));
            float size = slamRadius * 2f * Mathf.Lerp(0.2f, 1f, grown);
            warning.transform.localScale = new Vector3(size, size * 0.7f, 1f);      // a little flat, like a circle seen from above
            if (sr != null) { warning.sortingLayerID = sr.sortingLayerID; warning.sortingOrder = ShadowOrder(); }
        }
    }

    protected override void Animate()
    {
        if (sr == null) return;
        Sprite s;
        if (state == State.Windup) s = windupFrame;
        else if (state == State.Recover) s = slamFrame;
        else
        {
            if (desiredVelocity.sqrMagnitude > 0.01f) animTime += Time.deltaTime;
            s = Frame(walkFrames, animTime, animFps);
        }
        if (s != null) sr.sprite = s;
        if (player != null && state == State.Walk) sr.flipX = player.position.x < rb.position.x;
    }
}
