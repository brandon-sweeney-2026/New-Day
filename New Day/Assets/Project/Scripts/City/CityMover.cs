using UnityEngine;

/// <summary>
/// One passer-by in the city: a pedestrian, a cyclist, a scooter or a car. CityTraffic makes these.
/// It follows its route from one end to the other, then it is destroyed (it has walked or driven off the map).
///
///  - Pedestrians can be talked to: they say one to three lines and carry on.
///  - Wheels stop for the player: a car, scooter or bike brakes when the player is standing right in front of it.
///  - Everybody stands still while the game clock is paused (a conversation or a menu is open).
/// </summary>
public class CityMover : MonoBehaviour, IInteractable
{
    // filled in by CityTraffic.Spawn
    [HideInInspector] public CityTraffic.Kind kind;
    [HideInInspector] public Vector2[] route;
    [HideInInspector] public float speed = 2f;
    [HideInInspector] public float sideStep;             // how far to one side of the route they keep (people in a group walk side by side)
    [HideInInspector] public float framesPerSecond = 7f;
    [HideInInspector] public Sprite[] down, up, side;    // walkers: rows of walk frames. wheels: only 'side' is used (facing right)
    [HideInInspector] public string[] pages;             // what a pedestrian says
    [HideInInspector] public DialogueSpeaker speaker;
    [HideInInspector] public float stopDistance = 1.6f;

    private SpriteRenderer sr;
    private Transform player;
    private SpriteRenderer playerSr;
    private int leg;                     // which piece of the route they are on
    private float animTime;
    private Vector2 dir = Vector2.right;
    private bool talkedTo;

    public bool IsWalker { get { return kind == CityTraffic.Kind.Pedestrian; } }
    public string InteractPrompt { get { return IsWalker && pages != null && pages.Length > 0 ? "Say hello" : ""; } }

    /// <summary>Puts them 'distance' tiles along their route (used to fill the streets when the scene opens, and to space out a group).</summary>
    public void Begin(float distance)
    {
        sr = GetComponent<SpriteRenderer>();
        leg = 0;
        Vector2 at = route[0];
        while (leg + 1 < route.Length)
        {
            float len = Vector2.Distance(route[leg], route[leg + 1]);
            if (distance <= len) { at = Vector2.MoveTowards(route[leg], route[leg + 1], Mathf.Max(0f, distance)); break; }
            distance -= len; leg++; at = route[leg];
        }
        if (leg + 1 < route.Length) dir = (route[leg + 1] - route[leg]).normalized;
        transform.position = Offset(at);
        Draw(false);
    }

    private Vector2 Offset(Vector2 onRoute)
    {
        // "to one side" = at right angles to the way they are going
        return onRoute + new Vector2(-dir.y, dir.x) * sideStep;
    }

    private void Update()
    {
        if (route == null || route.Length < 2) { Destroy(gameObject); return; }
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) { player = pc.transform; playerSr = pc.GetComponentInChildren<SpriteRenderer>(); }
        }

        bool paused = TimeManager.Instance != null && TimeManager.Instance.Paused;
        bool moving = !paused && !Blocked();
        if (moving)
        {
            float step = speed * Time.deltaTime;
            Vector2 at = (Vector2)transform.position - new Vector2(-dir.y, dir.x) * sideStep;      // back onto the route line
            while (step > 0f && leg + 1 < route.Length)
            {
                Vector2 to = route[leg + 1] - at;
                float len = to.magnitude;
                if (len <= step) { at = route[leg + 1]; step -= len; leg++; if (leg + 1 < route.Length) dir = (route[leg + 1] - route[leg]).normalized; }
                else { dir = to / len; at += dir * step; step = 0f; }
            }
            transform.position = Offset(at);
            animTime += Time.deltaTime;
            if (leg + 1 >= route.Length) { Destroy(gameObject); return; }          // reached the far end: gone
        }
        Draw(moving);
    }

    /// <summary>Wheels brake when the player is close in front of them, so nobody gets run over.</summary>
    private bool Blocked()
    {
        if (IsWalker || player == null) return false;
        Vector2 to = (Vector2)player.position - (Vector2)transform.position;
        float ahead = Vector2.Dot(to, dir);
        float across = Mathf.Abs(Vector2.Dot(to, new Vector2(-dir.y, dir.x)));
        return ahead > 0f && ahead < stopDistance && across < 0.9f;
    }

    private void Draw(bool moving)
    {
        if (sr == null) return;
        if (IsWalker)
        {
            Sprite[] set; bool flip = false;
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) { set = side; flip = dir.x < 0f; }
            else set = dir.y > 0f ? up : down;
            if (set != null && set.Length > 0)
                sr.sprite = moving && set.Length > 1 ? set[1 + Mathf.FloorToInt(animTime * framesPerSecond) % (set.Length - 1)] : set[0];
            sr.flipX = flip;
        }
        else if (side != null && side.Length > 0)
        {
            sr.sprite = side[moving ? Mathf.FloorToInt(animTime * framesPerSecond) % side.Length : 0];
            sr.flipX = dir.x < 0f;
        }

        if (playerSr != null)
        {
            sr.sortingLayerID = playerSr.sortingLayerID;
            sr.sortingOrder = playerSr.sortingOrder + (transform.position.y < player.position.y - 0.05f ? 2 : -2);
        }
    }

    public void Interact(PlayerActions p)
    {
        if (!IsWalker || pages == null || pages.Length == 0 || DialogueBox.IsOpen) return;
        if (talkedTo) { DialogueBox.Show(speaker, new[] { "Sorry, I really have to go!" }, null, null); return; }
        talkedTo = true;
        if (player != null) { Vector2 d = player.position - transform.position; if (d.sqrMagnitude > 0.01f) { Vector2 keep = dir; dir = d.normalized; Draw(false); dir = keep; } }
        DialogueBox.Show(speaker, pages, null, null);
    }
}
