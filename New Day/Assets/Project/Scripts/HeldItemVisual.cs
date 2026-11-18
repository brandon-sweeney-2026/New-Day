using System.Collections;
using UnityEngine;

/// <summary>
/// Shows the equipped item's picture in the player's hand and moves it while the item is used.
/// It works for EVERY tool automatically: it just uses the item's icon, so a new tool needs no extra animation art.
/// The body animation (UseTool clips) plays at the same time.
///
/// Each kind of tool has its own movement (picked from the item's Tool Action):
///   Hoe, Pickaxe  - CHOP:  raised up and back, then brought down hard, with a small bounce when it lands.
///   Axe           - HACK:  a short pull back, a fast swing that goes a little too far, then it settles.
///   Sickle/Scythe - SWEEP: one wide, smooth arc from one side to the other.
///   Watering Can  - POUR:  held out in front, tipped over, bobbing gently, then tipped back.
///   anything else - the plain swing.
/// The numbers for these are under "Tool animations" in the Inspector.
///
/// Put this on the Player (Farm > Set Up > Game Systems In Scene does it for you).
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

    [Header("Tool animations (each tool moves in its own way)")]
    [Range(0f, 1f), Tooltip("Hoe / Pickaxe / Axe: how far the tool is pulled back before the hit. 0.35 = a third of a swing past the starting angle.")]
    [SerializeField] private float windUp = 0.35f;
    [Tooltip("Hoe / Pickaxe: how high the hand lifts while winding up (world units).")]
    [SerializeField] private float chopLift = 0.15f;
    [Tooltip("Hoe / Pickaxe / Axe: how far the hand pushes toward the target as the tool lands (world units).")]
    [SerializeField] private float strikeLunge = 0.12f;
    [Range(0f, 0.5f), Tooltip("Hoe / Pickaxe / Axe: how much the tool bounces back after it lands.")]
    [SerializeField] private float hitBounce = 0.1f;
    [Range(0f, 1f), Tooltip("Scythe: how much wider than a normal swing the sweep is. 0.5 = half a swing extra on each side.")]
    [SerializeField] private float sweepExtra = 0.5f;
    [Tooltip("Watering can: how far it tips over while pouring (degrees).")]
    [SerializeField] private float pourTilt = 55f;
    [Tooltip("Watering can: how far in front of the hand it is held while pouring (world units).")]
    [SerializeField] private float pourReach = 0.25f;

    /// <summary>The different ways a tool can move while it is used.</summary>
    private enum ToolMotion { Plain, Chop, Hack, Sweep, Pour }

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

        ToolMotion motion = MotionFor(item);
        Vector2 facing = actions.Facing.normalized;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float u = Mathf.Clamp01(t / duration);   // 0 at the start of the use, 1 at the end

            // 'swingT' says where the tool is between the pose's start angle (0) and end angle (1).
            // It may go below 0 (pulled back further) or above 1 (swung through further).
            float swingT;
            Vector2 offset = Vector2.zero;           // how far the hand has moved from its normal place
            float angle;

            switch (motion)
            {
                case ToolMotion.Chop:
                    // 0-40%: raise it up and back.  40-60%: bring it down fast.  60-100%: it has landed, with a small bounce.
                    if (u < 0.4f)
                    {
                        float k = EaseOut(u / 0.4f);
                        swingT = -windUp * k;
                        offset = Vector2.up * chopLift * k;
                    }
                    else if (u < 0.6f)
                    {
                        float k = (u - 0.4f) / 0.2f;
                        k = k * k;                                              // speeds up as it falls
                        swingT = Mathf.LerpUnclamped(-windUp, 1f, k);
                        offset = Vector2.up * chopLift * (1f - k) + facing * strikeLunge * k;
                    }
                    else
                    {
                        float k = (u - 0.6f) / 0.4f;
                        swingT = 1f - hitBounce * Mathf.Sin(k * Mathf.PI);     // up a little and back down
                        offset = facing * strikeLunge * (1f - k);
                    }
                    angle = Mathf.LerpUnclamped(pose.startAngle, pose.endAngle, swingT);
                    break;

                case ToolMotion.Hack:
                    // 0-30%: a short pull back.  30-55%: a fast swing that overshoots.  55-100%: it settles back.
                    if (u < 0.3f)
                    {
                        swingT = -windUp * 0.6f * EaseOut(u / 0.3f);
                    }
                    else if (u < 0.55f)
                    {
                        float k = (u - 0.3f) / 0.25f;
                        k = k * k;
                        swingT = Mathf.LerpUnclamped(-windUp * 0.6f, 1f + hitBounce, k);
                        offset = facing * strikeLunge * 1.3f * k;
                    }
                    else
                    {
                        float k = EaseOut((u - 0.55f) / 0.45f);
                        swingT = Mathf.LerpUnclamped(1f + hitBounce, 1f - hitBounce, k);
                        offset = facing * strikeLunge * 1.3f * (1f - k);
                    }
                    angle = Mathf.LerpUnclamped(pose.startAngle, pose.endAngle, swingT);
                    break;

                case ToolMotion.Sweep:
                    // One smooth arc that starts before the normal start angle and ends past the normal end angle.
                    swingT = Mathf.LerpUnclamped(-sweepExtra, 1f + sweepExtra, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 0.1f) / 0.7f)));
                    offset = facing * strikeLunge * Mathf.Sin(u * Mathf.PI);   // reaches out in the middle of the sweep
                    angle = Mathf.LerpUnclamped(pose.startAngle, pose.endAngle, swingT);
                    break;

                case ToolMotion.Pour:
                    // Tip over during the first quarter, hold (bobbing a little), tip back during the last 15%.
                    float tip = u < 0.25f ? EaseOut(u / 0.25f) : (u > 0.85f ? 1f - (u - 0.85f) / 0.15f : 1f);
                    float bob = Mathf.Sin(t * 22f) * 3f * tip;                  // a few degrees of wobble while the water runs
                    // The picture points up-right, so tipping forward is clockwise (negative) - or the other way when mirrored.
                    angle = (pose.flipX ? 1f : -1f) * (pourTilt * tip + bob);
                    offset = facing * pourReach * tip + Vector2.up * 0.08f * tip;
                    break;

                default:
                    // The plain swing: wind up slowly (first 35%), then strike quickly.
                    swingT = Mathf.Clamp01((u - 0.35f) / 0.4f);
                    swingT = swingT * swingT;
                    angle = Mathf.Lerp(pose.startAngle, pose.endAngle, swingT);
                    break;
            }

            sr.transform.localPosition = pose.hand + offset;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        swing = null;
        RefreshCarry();
    }

    /// <summary>Which movement an item uses. Add a line here if you make a new kind of tool.</summary>
    private static ToolMotion MotionFor(ItemData item)
    {
        if (item.type != ItemData.ItemType.Tool) return ToolMotion.Plain;
        switch (item.toolAction)
        {
            case ItemData.ToolAction.Hoe:
            case ItemData.ToolAction.Pickaxe: return ToolMotion.Chop;
            case ItemData.ToolAction.Axe: return ToolMotion.Hack;
            case ItemData.ToolAction.Sickle: return ToolMotion.Sweep;
            case ItemData.ToolAction.WateringCan: return ToolMotion.Pour;
            default: return ToolMotion.Plain;
        }
    }

    /// <summary>Starts fast and slows down toward the end (0 to 1).</summary>
    private static float EaseOut(float k)
    {
        k = Mathf.Clamp01(k);
        return 1f - (1f - k) * (1f - k);
    }

    /// <summary>Shows the selected item in the player's hand (hidden while the sword animation is drawing its own sword).</summary>
    private void RefreshCarry()
    {
        if (swing != null) return;

        ItemData item = inventory != null ? inventory.EquippedItem : null;
        bool show = carryEquipped && item != null && item.icon != null;

        // Weapons and spells have their own animation while in use, so hide the carried copy meanwhile.
        if (show && actions.IsBusy && !Allowed(item)) show = false;
        // While fishing, FishingRig draws the rod (swinging, with a line), so hide the carried copy.
        if (FishingManager.IsFishing) show = false;

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
