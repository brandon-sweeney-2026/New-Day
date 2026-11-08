using System.Collections;
using UnityEngine;

/// <summary>
/// Shows the equipped item's picture in the player's hand and swings it while the item is used.
/// It works for EVERY tool automatically: it just uses the item's icon, so a new tool needs no extra animation art.
/// The body animation (UseTool clips) plays at the same time.
///
/// Put this on the Player (Farm > Game > Set Up Game Systems In Scene does it for you).
/// Tune the 'Hand positions' and swing angles below if the tool looks off for your sprite.
/// Icons are assumed to be drawn pointing UP-RIGHT (like most pixel-art tool icons).
/// </summary>
[RequireComponent(typeof(PlayerActions))]
public class HeldItemVisual : MonoBehaviour
{
    [System.Serializable]
    public class Pose
    {
        [Tooltip("Where the hand is, relative to the player's feet.")]
        public Vector2 hand;
        [Tooltip("Rotation (degrees) at the start of the swing.")]
        public float startAngle;
        [Tooltip("Rotation (degrees) at the end of the swing.")]
        public float endAngle;
        [Tooltip("Mirror the picture (used for facing left).")]
        public bool flipX;
        [Tooltip("Draw behind the player (used when facing up).")]
        public bool behind;
    }

    [Header("What to show")]
    [SerializeField] private bool showForTools = true;
    [SerializeField] private bool showForWeapons = false;   // your sword animation already draws the sword
    [SerializeField] private bool showForSpells = false;
    [Tooltip("Show whatever is selected in the hotbar in the player's hand (tools, seeds, food, crops, weapons, spells...).")]
    [SerializeField] private bool carryEquipped = true;
    [SerializeField] private float carryScale = 0.65f;
    [Tooltip("Tilt of the carried item in degrees (mirrored when facing left).")]
    [SerializeField] private float carryAngle = 15f;
    [SerializeField] private float swingScale = 1f;

    [Header("Hand positions and swing (order: Down, Up, Left, Right)")]
    [SerializeField] private Pose down = new Pose { hand = new Vector2(0.28f, 0.5f), startAngle = 20f, endAngle = -135f };
    [SerializeField] private Pose up = new Pose { hand = new Vector2(0.28f, 0.6f), startAngle = -30f, endAngle = 60f, behind = true };
    [SerializeField] private Pose left = new Pose { hand = new Vector2(-0.35f, 0.55f), startAngle = -45f, endAngle = 45f, flipX = true };
    [SerializeField] private Pose right = new Pose { hand = new Vector2(0.35f, 0.55f), startAngle = 45f, endAngle = -45f };
    [Tooltip("Carried items sit this far from the hand positions above (move it down/in if they float).")]
    [SerializeField] private Vector2 carryNudge = new Vector2(0f, -0.1f);

    private PlayerActions actions;
    private Inventory inventory;
    private SpriteRenderer sr;
    private SpriteRenderer bodyRenderer;
    private Coroutine swing;

    private void Awake()
    {
        actions = GetComponent<PlayerActions>();
        inventory = GetComponent<Inventory>();
        bodyRenderer = GetComponentInChildren<SpriteRenderer>();

        var go = new GameObject("HeldItem");
        go.transform.SetParent(transform, false);
        sr = go.AddComponent<SpriteRenderer>();
        sr.enabled = false;
    }

    private void OnEnable()
    {
        if (actions != null) actions.OnItemUsed += OnItemUsed;
    }

    private void Start()
    {
        if (inventory != null)
        {
            inventory.OnChanged += RefreshCarry;
            inventory.OnSelectionChanged += RefreshCarry;
        }
        RefreshCarry();
    }

    private void Update()
    {
        if (swing == null) RefreshCarry();   // follows the facing direction while walking
    }

    private void OnDisable()
    {
        if (actions != null) actions.OnItemUsed -= OnItemUsed;
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnChanged -= RefreshCarry;
            inventory.OnSelectionChanged -= RefreshCarry;
        }
    }

    // ------------------------------------------------------------------

    private void OnItemUsed(ItemData item, float duration)
    {
        if (item == null || item.icon == null) return;
        if (!Allowed(item)) return;

        if (swing != null) StopCoroutine(swing);
        swing = StartCoroutine(Swing(item, duration));
    }

    private bool Allowed(ItemData item)
    {
        switch (item.type)
        {
            case ItemData.ItemType.Tool: return showForTools;
            case ItemData.ItemType.Weapon: return showForWeapons;
            case ItemData.ItemType.Spell: return showForSpells;
            default: return false;
        }
    }

    private Pose PoseFor(Vector2 facing)
    {
        if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y)) return facing.x > 0f ? right : left;
        return facing.y > 0f ? up : down;
    }

    private IEnumerator Swing(ItemData item, float duration)
    {
        Pose pose = PoseFor(actions.Facing);
        sr.sprite = item.icon;
        sr.flipX = pose.flipX;
        sr.sortingOrder = (bodyRenderer != null ? bodyRenderer.sortingOrder : 10) + (pose.behind ? -1 : 1);
        sr.transform.localScale = Vector3.one * swingScale;
        sr.enabled = true;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float u = Mathf.Clamp01(t / duration);

            // Wind up slowly (first 35%), then strike quickly.
            float p = Mathf.Clamp01((u - 0.35f) / 0.4f);
            p = p * p;
            float angle = Mathf.Lerp(pose.startAngle, pose.endAngle, p);

            sr.transform.localPosition = pose.hand;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        swing = null;
        RefreshCarry();
    }

    /// <summary>Shows the selected item in the player's hand (hidden while the sword animation is drawing its own sword).</summary>
    private void RefreshCarry()
    {
        if (swing != null) return;

        ItemData item = inventory != null ? inventory.EquippedItem : null;
        bool show = carryEquipped && item != null && item.icon != null;

        // Weapons and spells have their own animation while in use, so hide the carried copy meanwhile.
        if (show && actions.IsBusy && !Allowed(item)) show = false;

        sr.enabled = show;
        if (!show) return;

        Pose pose = PoseFor(actions.Facing);
        sr.sprite = item.icon;
        sr.flipX = pose.flipX;
        sr.sortingOrder = (bodyRenderer != null ? bodyRenderer.sortingOrder : 10) + (pose.behind ? -1 : 1);
        sr.transform.localScale = Vector3.one * carryScale;
        sr.transform.localPosition = pose.hand + carryNudge;
        sr.transform.localRotation = Quaternion.Euler(0f, 0f, pose.flipX ? -carryAngle : carryAngle);
    }
}
