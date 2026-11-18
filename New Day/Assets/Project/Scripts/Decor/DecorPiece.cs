using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One piece of furniture (or a window, picture, rug...) placed in a room from the decor catalogue.
/// The object's position is the bottom-left corner of the floor space it takes, so it lines up with the tiles.
///
/// Change 'Item' or 'Facing' in the Inspector (or use the Interior Decorator window, or Ctrl+Shift+R) and the picture,
/// the collider and the drawing order update by themselves.
///
/// Drawing order: floor pieces are drawn in front of the player when the player is behind them (higher up the screen)
/// and behind the player when the player is in front, like BehindPlayerSorter. Between pieces, the project's
/// "sort by Y" setting puts the lower piece in front. Rugs lie under everything, wall pieces sit on the wall.
/// </summary>
[DisallowMultipleComponent]
public class DecorPiece : MonoBehaviour
{
    [Tooltip("What this is. Change it to swap the piece for another one.")]
    public DecorItem item;
    [Tooltip("Which way it faces. Front = towards the camera.")]
    public DecorFacing facing = DecorFacing.Front;
    [Tooltip("Surface pieces only: how many pixels up the picture is drawn (the height of the counter or table it stands on).")]
    [Min(0)] public int lift = 0;
    [Tooltip("Surface pieces only: depth in tiles of the piece it stands on (for the drawing order).")]
    [Min(0)] public int hostDepth = 0;

    [Header("Made by the piece itself")]
    [SerializeField] private SpriteRenderer art;

    // shared by every piece: the player, so each piece doesn't search for it
    private static Transform player;
    private static SpriteRenderer playerSr;
    private SortingGroup group;

    /// <summary>The floor space this piece takes, in tiles, for its current facing.</summary>
    public Vector2Int Size { get { return item != null ? item.SizeFor(facing) : Vector2Int.one; } }

    /// <summary>The picture renderer (a child called "Art").</summary>
    public SpriteRenderer Art { get { return art; } }

    private void Awake()
    {
        group = GetComponent<SortingGroup>();
    }

    private void Start()
    {
        Refresh();
    }

    /// <summary>
    /// Makes the picture, collider and drawing order match Item and Facing. Safe to call any time (the editor tools call it).
    /// </summary>
    public void Refresh()
    {
        if (item == null) return;

        if (art == null)
        {
            Transform t = transform.Find("Art");
            if (t == null) { t = new GameObject("Art").transform; t.SetParent(transform, false); }
            art = t.GetComponent<SpriteRenderer>();
            if (art == null) art = t.gameObject.AddComponent<SpriteRenderer>();
        }
        art.sprite = item.SpriteFor(facing);
        art.spriteSortPoint = SpriteSortPoint.Pivot;
        art.transform.localPosition = new Vector3(0f, item.mount == DecorMount.Surface ? lift / 16f : 0f, 0f);

        if (group == null) group = GetComponent<SortingGroup>();
        if (group == null) group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = FixedOrder();

        // floor pieces that block the player get a box the size of their floor space (a little smaller, so pieces can touch)
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        bool wantsBox = item.mount == DecorMount.Floor && item.blocksPlayer;
        if (wantsBox)
        {
            if (box == null) box = gameObject.AddComponent<BoxCollider2D>();
            Vector2 size = Size;
            box.size = new Vector2(Mathf.Max(0.2f, size.x - 0.1f), Mathf.Max(0.2f, size.y - 0.15f));
            box.offset = new Vector2(size.x * 0.5f, size.y * 0.5f);
            box.isTrigger = false;
            box.enabled = true;
        }
        else if (box != null) box.enabled = false;
    }

    /// <summary>The order for pieces that never change it: rugs under everything, wall pieces on the wall.</summary>
    private int FixedOrder()
    {
        if (item == null) return 10;
        switch (item.mount)
        {
            case DecorMount.Flat: return 2;
            case DecorMount.Wall: return 3;
            default: return playerSr != null ? playerSr.sortingOrder - 5 : 5;
        }
    }

    private void LateUpdate()
    {
        if (item == null || group == null) return;
        if (item.mount == DecorMount.Flat || item.mount == DecorMount.Wall) return;

        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc == null) return;
            player = pc.transform;
            playerSr = pc.GetComponent<SpriteRenderer>();
        }

        // the player is "behind" a piece once their feet are past the middle of its floor space
        float depth = item.mount == DecorMount.Surface ? Mathf.Max(1, hostDepth) : Size.y;
        int playerOrder = playerSr != null ? playerSr.sortingOrder : 10;
        bool playerBehind = player.position.y > transform.position.y + depth * 0.5f;
        int order = playerBehind ? playerOrder + 5 : playerOrder - 5;
        if (group.sortingOrder != order) group.sortingOrder = order;
    }

    private void OnDrawGizmosSelected()
    {
        if (item == null) return;
        Vector2 s = Size;
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        Gizmos.DrawWireCube(transform.position + new Vector3(s.x * 0.5f, s.y * 0.5f, 0f), new Vector3(s.x, s.y, 0f));
    }
}
