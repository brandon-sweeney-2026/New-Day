using UnityEngine;

/// <summary>
/// A slime. It moves only by HOPPING: it sits still, squashes down to warn you, then jumps a short way toward you and sits again.
/// It cannot change direction in the air, so step aside while it squashes and hit it when it lands.
/// A slime can also SPLIT: give it a 'Split Into' prefab and it bursts into smaller slimes when it dies (the magma slime does).
/// Every number is in the Inspector.
/// </summary>
public class SlimeEnemy : EnemyBase
{
    [Header("Art (the setup menu fills these in)")]
    [Tooltip("4 pictures in this order: sitting, squashed (about to hop), in the air, landing.")]
    public Sprite[] frames;

    [Header("Hopping")]
    [Tooltip("Seconds it sits between hops.")] public Vector2 restSeconds = new Vector2(0.5f, 1.1f);
    [Tooltip("Seconds it squashes down before a hop (your warning).")] public float squashSeconds = 0.3f;
    [Tooltip("Seconds one hop lasts.")] public float hopSeconds = 0.45f;
    [Tooltip("Speed while in the air.")] public float hopSpeed = 4.5f;
    [Tooltip("It hops toward the player inside this distance, and at random outside it.")] public float aggroRange = 8f;

    [Header("Splitting (optional)")]
    [Tooltip("Leave empty for a slime that just dies.")] public GameObject splitInto;
    [Min(0)] public int splitCount = 2;

    private enum State { Rest, Squash, Hop, Land }
    private State state = State.Rest;
    private float timer;
    private Vector2 hopDirection;

    protected override void Awake()
    {
        base.Awake();
        if (enemyName == "Enemy") enemyName = "Slime";
        timer = Random.Range(0f, restSeconds.y);       // so a group of slimes doesn't hop in step
    }

    protected override void Think(float dt)
    {
        timer -= dt;
        switch (state)
        {
            case State.Rest:
                if (timer <= 0f) { state = State.Squash; timer = squashSeconds; }
                break;

            case State.Squash:
                if (timer <= 0f)
                {
                    // the direction is chosen ONCE, at take-off
                    Vector2 toPlayer = PlayerCenter - rb.position;
                    hopDirection = toPlayer.magnitude < aggroRange ? toPlayer.normalized : Random.insideUnitCircle.normalized;
                    state = State.Hop; timer = hopSeconds;
                }
                break;

            case State.Hop:
                desiredVelocity = hopDirection * hopSpeed;
                if (timer <= 0f) { state = State.Land; timer = 0.15f; }
                break;

            case State.Land:
                if (timer <= 0f) { state = State.Rest; timer = Random.Range(restSeconds.x, restSeconds.y); }
                break;
        }
    }

    protected override void Animate()
    {
        if (sr == null || frames == null || frames.Length < 4) return;
        sr.sprite = frames[(int)state];                // the four states are in the same order as the four pictures
        if (state == State.Hop && Mathf.Abs(hopDirection.x) > 0.1f) sr.flipX = hopDirection.x < 0f;
    }

    protected override void Die()
    {
        if (splitInto != null)
            for (int i = 0; i < splitCount; i++)
            {
                Vector3 at = transform.position + (Vector3)(Random.insideUnitCircle * 0.5f);
                Instantiate(splitInto, at, Quaternion.identity, transform.parent);
            }
        base.Die();
    }
}
