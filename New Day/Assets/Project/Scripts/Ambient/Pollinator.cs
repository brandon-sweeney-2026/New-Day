using UnityEngine;

/// <summary>
/// One butterfly or bee. Made by the PollinatorSpawner (you don't add this yourself).
/// It flutters in from off screen, lands on a flower for a few seconds, may hop to a couple more flowers, then flies away
/// and deletes itself. It takes off early if the player walks right up to it.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Pollinator : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Wing pictures, played in a loop while flying.")]
    public Sprite[] frames;
    public float flapsPerSecond = 9f;

    [Header("Flying")]
    public float speed = 1.6f;
    [Tooltip("How far it drifts side to side while flying (world units). Butterflies flutter a lot, bees fly straighter.")]
    public float wobble = 0.35f;
    public float wobbleSpeed = 5f;

    [Header("Visiting flowers")]
    [Tooltip("Seconds it sits on a flower (random between the two).")]
    public Vector2 restSeconds = new Vector2(2.5f, 6f);
    [Tooltip("How many flowers it visits before leaving (random between the two).")]
    public Vector2Int visits = new Vector2Int(1, 3);
    [Tooltip("How far it will fly to the next flower.")]
    public float hopRadius = 5f;
    [Tooltip("It takes off if the player comes closer than this.")]
    public float scareDistance = 0.9f;

    private enum State { Flying, Resting, Leaving }
    private State state = State.Flying;

    private SpriteRenderer sr;
    private FlowerSpot flower;        // the flower it is heading for or sitting on
    private Vector2 target;           // where it is flying to
    private Vector2 straightPos;      // its position without the side-to-side wobble
    private float restUntil;
    private int visitsLeft;
    private float seed;
    private Transform player;

    /// <summary>Called by the spawner right after it is created.</summary>
    public void Begin(FlowerSpot firstFlower)
    {
        sr = GetComponent<SpriteRenderer>();
        seed = Random.value * 10f;
        straightPos = transform.position;
        visitsLeft = Random.Range(visits.x, visits.y + 1);
        GoTo(firstFlower);
    }

    private void GoTo(FlowerSpot next)
    {
        if (flower != null) flower.Taken = false;
        flower = next;
        if (flower == null) { Leave(); return; }
        flower.Taken = true;
        target = flower.LandingPoint;
        state = State.Flying;
    }

    private void Leave()
    {
        if (flower != null) flower.Taken = false;
        flower = null;
        // Head off in a random direction, far enough to be off screen.
        float a = Random.Range(0f, Mathf.PI * 2f);
        target = straightPos + new Vector2(Mathf.Cos(a), Mathf.Abs(Mathf.Sin(a)) * 0.6f + 0.3f).normalized * 16f;
        state = State.Leaving;
    }

    private void Update()
    {
        if (sr == null) return;

        // wings
        if (frames != null && frames.Length > 0)
        {
            // Resting insects open and close their wings slowly; flying ones flap fast.
            float fps = state == State.Resting ? flapsPerSecond * 0.15f : flapsPerSecond;
            sr.sprite = frames[Mathf.FloorToInt((Time.time + seed) * fps) % frames.Length];
        }

        if (state == State.Resting)
        {
            if (flower == null) { Leave(); return; }
            transform.position = flower.LandingPoint;          // follows the flower as it sways
            straightPos = transform.position;

            bool scared = PlayerIsClose();
            if (Time.time < restUntil && !scared) return;

            visitsLeft--;
            FlowerSpot next = (visitsLeft > 0 && !scared) ? FlowerSpot.RandomNear(straightPos, hopRadius, flower) : null;
            if (next != null) GoTo(next); else Leave();
            return;
        }

        // flying (toward a flower, or away)
        if (state == State.Flying && flower != null) target = flower.LandingPoint;

        Vector2 to = target - straightPos;
        float dist = to.magnitude;
        float step = speed * Time.deltaTime;

        if (dist <= step)
        {
            straightPos = target;
            if (state == State.Leaving) { Destroy(gameObject); return; }
            state = State.Resting;
            restUntil = Time.time + Random.Range(restSeconds.x, restSeconds.y);
            transform.position = straightPos;
            return;
        }

        Vector2 dir = to / dist;
        straightPos += dir * step;

        // Wobble sideways (and a little up and down); it calms down as the insect gets close to the flower.
        float calm = state == State.Flying ? Mathf.Clamp01(dist / 1.2f) : 1f;
        Vector2 side = new Vector2(-dir.y, dir.x);
        Vector2 offset = side * Mathf.Sin((Time.time + seed) * wobbleSpeed) * wobble * calm
                       + Vector2.up * Mathf.Sin((Time.time + seed) * wobbleSpeed * 1.7f) * wobble * 0.4f * calm;
        transform.position = straightPos + offset;
        if (Mathf.Abs(dir.x) > 0.05f) sr.flipX = dir.x < 0f;
    }

    private bool PlayerIsClose()
    {
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) return false;
            player = pc.transform;
        }
        return Vector2.Distance(player.position, transform.position) < scareDistance;
    }

    private void OnDestroy()
    {
        if (flower != null) flower.Taken = false;      // free the flower for the next visitor
    }
}
