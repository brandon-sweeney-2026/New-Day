using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE one action button. Put this on the Player (replaces FarmToolController and PlayerAttack).
///
/// Press Space (or J, or gamepad A):
///   1. If you are standing in front of something interactive (bed, shipping bin, NPC) you use that.
///   2. Otherwise the EQUIPPED item (the selected inventory slot) is used:
///        Tool   - Hoe tills, Watering Can waters, Sickle/Scythe harvests, Shovel digs up a crop,
///                 Axe/Pickaxe hit trees and rocks (Breakable).
///                 Better tools do more: the Hoe, Watering Can and Scythe work on several tiles in a row
///                 (the item's Tool Reach), the Axe and Pickaxe count as several hits (the item's Tool Power).
///        Seed   - plants on tilled soil.
///        Food   - eats it (restores health).
///        Weapon - swings and damages enemies in front of you.
///        Spell  - casts a bolt (costs magic) or heals you.
///
/// Optional Animator triggers on the player (skipped if the Animator doesn't have them):
///   "Attack" (weapons), "UseTool" (tools, seeds), "Cast" (spells).
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerActions : MonoBehaviour
{
    [Header("References (found automatically if empty)")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private FarmManager farm;
    [SerializeField] private CropManager crops;
    [SerializeField] private Animator animator;

    [Header("Controls")]
    [SerializeField] private bool useSpace = true;
    [SerializeField] private bool useJKey = true;
    [SerializeField] private bool useGamepadSouth = true;
    [SerializeField] private bool useLeftMouse = false;

    [Header("Targeting")]
    [Tooltip("Where the player's 'hands' are, relative to its position. Move this up if your pivot is at the feet.")]
    [SerializeField] private Vector2 originOffset = new Vector2(0f, 0.3f);
    [Tooltip("How far in front (in tiles) the farming target tile is.")]
    [SerializeField] private float targetDistance = 0.9f;
    [SerializeField] private float interactDistance = 0.7f;
    [SerializeField] private float interactRadius = 0.6f;
    [SerializeField] private bool showTargetTile = true;
    [SerializeField] private Color targetColor = new Color(1f, 1f, 1f, 0.28f);

    [Header("Timing")]
    [Tooltip("How long the player is locked in place after using a tool or seed.")]
    [SerializeField] private float toolDuration = 1f / 3f;
    [Tooltip("How long the player is locked in place while pouring the watering can (a pour looks rushed if it is too short).")]
    [SerializeField] private float pourDuration = 0.55f;
    [Range(0.1f, 0.9f)]
    [Tooltip("The moment in a tool swing when it lands (0.55 = just past half way). Dirt, chips and other effects appear then.")]
    [SerializeField] private float strikePoint = 0.55f;

    [Header("Tool effects")]
    [Tooltip("Show dirt, water drops, wood chips and so on when a tool is used.")]
    [SerializeField] private bool showToolEffects = true;

    // ---- public state ----
    /// <summary>Every tile the equipped tool will work on (1 for a basic tool, more for upgraded ones). The first one is TargetCell.</summary>
    public IReadOnlyList<Vector3Int> TargetCells { get { return targetCells; } }
    public Inventory Inventory { get { return inventory; } }
    public PlayerStats Stats { get { return stats; } }
    public PlayerController Controller { get; private set; }
    public Vector2 Facing { get { return Controller.FacingDirection; } }
    public Vector3Int TargetCell { get; private set; }
    public bool HasTargetCell { get; private set; }
    public IInteractable CurrentInteractable { get; private set; }
    public bool IsBusy { get; private set; }
    /// <summary>The player's hands (a bit above the feet). Weapons swing from here.</summary>
    public Vector2 HandOrigin { get { return Origin; } }
    /// <summary>True while the action button is held down (the bow uses this to draw and release).</summary>
    public bool ActionHeld
    {
        get
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && ((useSpace && kb.spaceKey.isPressed) || (useJKey && kb.jKey.isPressed))) return true;
            if (useGamepadSouth && Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) return true;
            if (useLeftMouse && Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
            return false;
        }
    }
    /// <summary>Used by WeaponController to take over the player while a weapon is in use.</summary>
    public void SetBusy(bool busy) { IsBusy = busy; }
    public void SetCooldown(float seconds) { nextActionTime = Time.time + seconds; }
    public void RaiseItemUsed(ItemData item, float duration) { if (OnItemUsed != null) OnItemUsed(item, duration); }
    public Animator BodyAnimator { get { return animator; } }

    /// <summary>Raised when an item starts being used: (item, how long the use lasts). Visual scripts listen to this.</summary>
    public event System.Action<ItemData, float> OnItemUsed;

    private readonly List<SpriteRenderer> highlights = new List<SpriteRenderer>();   // one square per target tile
    private readonly List<Vector3Int> targetCells = new List<Vector3Int>();
    private SpriteRenderer bodyRenderer;       // the player's picture (effects are drawn just in front of it)
    private Vector3 lastToolHitPoint;          // where the axe / pickaxe landed (for the chips)
    private ItemData swingingWeapon;
    private readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
    private float nextActionTime;
    private readonly HashSet<int> animatorParams = new HashSet<int>();

    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int UseToolTrigger = Animator.StringToHash("UseTool");
    private static readonly int CastTrigger = Animator.StringToHash("Cast");

    private void Awake()
    {
        Controller = GetComponent<PlayerController>();
        if (inventory == null) inventory = GetComponent<Inventory>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        bodyRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        if (farm == null) farm = FindAnyObjectByType<FarmManager>();
        if (crops == null) crops = FindAnyObjectByType<CropManager>();

        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in animator.parameters) animatorParams.Add(p.nameHash);

    }

    private void OnDestroy()
    {
        foreach (SpriteRenderer h in highlights)
            if (h != null) Destroy(h.gameObject);
    }

    /// <summary>The white square number 'index' (they are made the first time they are needed).</summary>
    private SpriteRenderer Highlight(int index)
    {
        while (highlights.Count <= index)
        {
            var go = new GameObject("TargetTile");
            var h = go.AddComponent<SpriteRenderer>();
            h.sprite = RuntimeSprites.Square;
            h.color = targetColor;
            h.sortingOrder = 4;
            go.SetActive(false);
            highlights.Add(h);
        }
        return highlights[index];
    }

    private void HideHighlights()
    {
        foreach (SpriteRenderer h in highlights)
            if (h != null && h.gameObject.activeSelf) h.gameObject.SetActive(false);
    }

    /// <summary>One tile step in the direction the player faces (right, left, up or down).</summary>
    private Vector3Int FacingStep()
    {
        Vector2 f = Facing;
        if (Mathf.Abs(f.x) > Mathf.Abs(f.y)) return new Vector3Int(f.x > 0f ? 1 : -1, 0, 0);
        return new Vector3Int(0, f.y > 0f ? 1 : -1, 0);
    }

    /// <summary>How many tiles in a row this item works on. Only the Hoe, Watering Can and Sickle/Scythe use their Tool Reach.</summary>
    private static int ReachOf(ItemData item)
    {
        if (item == null || item.type != ItemData.ItemType.Tool) return 1;
        switch (item.toolAction)
        {
            case ItemData.ToolAction.Hoe:
            case ItemData.ToolAction.WateringCan:
            case ItemData.ToolAction.Sickle:
                return Mathf.Max(1, item.toolReach);
            default:
                return 1;
        }
    }

    private Vector2 Origin { get { return (Vector2)transform.position + originOffset; } }

    private void Update()
    {
        if (GameState.InputLocked || IsBusy || (stats != null && stats.IsDead))
        {
            HideHighlights();
            CurrentInteractable = null;
            return;
        }

        UpdateTarget();
        UpdateInteractable();

        if (ActionPressed() && Time.time >= nextActionTime) DoAction();
    }

    private bool ActionPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (useSpace && kb.spaceKey.wasPressedThisFrame) return true;
            if (useJKey && kb.jKey.wasPressedThisFrame) return true;
        }
        if (useGamepadSouth && Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        if (useLeftMouse && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
        return false;
    }

    // ------------------------------------------------------------------
    // Finding what is in front of the player
    // ------------------------------------------------------------------

    private void UpdateTarget()
    {
        HasTargetCell = farm != null;
        targetCells.Clear();
        if (!HasTargetCell) { HideHighlights(); return; }

        TargetCell = farm.WorldToCell(Origin + Facing * targetDistance);

        ItemData item = inventory != null ? inventory.EquippedItem : null;
        bool needsTile = item != null && (item.type == ItemData.ItemType.Tool || item.type == ItemData.ItemType.Seed);

        // An upgraded hoe / watering can / scythe works on a row of tiles: the one in front of you and the ones behind it.
        int reach = ReachOf(item);
        Vector3Int step = FacingStep();
        for (int i = 0; i < reach; i++) targetCells.Add(TargetCell + step * i);

        // One white square per target tile.
        bool show = showTargetTile && needsTile && CurrentInteractable == null;
        int wanted = show ? targetCells.Count : 0;
        for (int i = 0; i < Mathf.Max(wanted, highlights.Count); i++)
        {
            SpriteRenderer h = Highlight(i);
            bool on = i < wanted;
            if (h.gameObject.activeSelf != on) h.gameObject.SetActive(on);
            if (on) h.transform.position = farm.CellToWorldCenter(targetCells[i]);
        }
    }

    private void UpdateInteractable()
    {
        CurrentInteractable = null;
        Vector2 center = Origin + Facing * interactDistance;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, interactRadius);

        float best = float.MaxValue;
        foreach (Collider2D hit in hits)
        {
            if (hit.transform.IsChildOf(transform)) continue;
            var interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable == null) continue;

            float d = ((Vector2)hit.bounds.center - center).sqrMagnitude;
            if (d < best) { best = d; CurrentInteractable = interactable; }
        }
    }

    /// <summary>Short text for the HUD: what pressing the button will do right now.</summary>
    public string ActionPrompt
    {
        get
        {
            if (CurrentInteractable != null)
            {
                string p = CurrentInteractable.InteractPrompt;
                return string.IsNullOrEmpty(p) ? "" : p;
            }
            return "";
        }
    }

    // ------------------------------------------------------------------
    // The action
    // ------------------------------------------------------------------

    private void DoAction()
    {
        if (CurrentInteractable != null)
        {
            CurrentInteractable.Interact(this);
            nextActionTime = Time.time + 0.25f;
            return;
        }

        ItemData item = inventory != null ? inventory.EquippedItem : null;
        if (item == null) return;

        switch (item.type)
        {
            case ItemData.ItemType.Tool: UseTool(item); break;
            case ItemData.ItemType.Seed: Plant(item); break;
            case ItemData.ItemType.Food: Eat(item); break;
            case ItemData.ItemType.Weapon:
                if (item.weaponKind != ItemData.WeaponKind.Generic)
                {
                    var wc = GetComponent<WeaponController>();
                    if (wc != null) { wc.Begin(item); break; }
                    GameEvents.ShowToast("Add the WeaponController to the Player (Farm > Set Up > Weapons).");
                    break;
                }
                StartCoroutine(Swing(item));
                break;
            case ItemData.ItemType.Spell: StartCoroutine(Cast(item)); break;
            default: break;   // crops and resources can't be "used" - sell them at the shipping bin
        }
    }

    // ---- tools ----

    private void UseTool(ItemData tool)
    {
        bool did = false;
        float duration = toolDuration;
        var worked = new List<Vector3Int>();   // the tiles the tool really changed (the effects are drawn on these)

        switch (tool.toolAction)
        {
            case ItemData.ToolAction.Hoe:
                // Tills every target tile it can (a better hoe has more target tiles).
                foreach (Vector3Int cell in targetCells)
                {
                    if (TreeManager.Instance != null && TreeManager.Instance.HasTree(cell)) continue;
                    if (farm != null && farm.TryTill(cell)) worked.Add(cell);
                }
                did = worked.Count > 0;
                if (!did && TreeManager.Instance != null && TreeManager.Instance.HasTree(TargetCell))
                    GameEvents.ShowToast("A tree is in the way. Chop it down first.");
                break;

            case ItemData.ToolAction.WateringCan:
                foreach (Vector3Int cell in targetCells)
                    if (farm != null && farm.TryWater(cell)) worked.Add(cell);
                did = worked.Count > 0;
                duration = pourDuration;
                break;

            case ItemData.ToolAction.Sickle:
                for (int i = 0; i < targetCells.Count; i++)
                {
                    Vector3Int cell = targetCells[i];
                    // Only the tile right in front of you says "isn't ready yet"; the rest of the row is skipped quietly.
                    // (Withered crops count too: the scythe clears them.)
                    if (i > 0 && (crops == null || !(crops.IsRipe(cell) || crops.IsWithered(cell)))) continue;
                    if (crops != null && crops.TryHarvest(cell, inventory)) worked.Add(cell);
                }
                did = worked.Count > 0;
                break;

            case ItemData.ToolAction.Shovel:
                did = crops != null && crops.TryRemove(TargetCell);
                if (did) worked.Add(TargetCell);
                break;

            case ItemData.ToolAction.Axe:
            case ItemData.ToolAction.Pickaxe:
                did = HitToolTarget(tool.toolAction, tool.toolPower);
                break;

            case ItemData.ToolAction.FishingRod:
                FishingManager.TryStart(this, tool);
                break;

            default:
                GameEvents.ShowToast(tool.itemName + " has no Tool Action set. Pick one on the item.");
                break;
        }

        if (did)
        {
            StartCoroutine(Lock(duration, UseToolTrigger, tool));
            if (showToolEffects) StartCoroutine(ToolEffects(tool.toolAction, worked, duration));
        }
    }

    /// <summary>Hits the tree or rock in front of the player. 'power' = how many hits this swing counts as.</summary>
    private bool HitToolTarget(ItemData.ToolAction action, int power)
    {
        Vector2 center = Origin + Facing * interactDistance;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, interactRadius))
        {
            var target = hit.GetComponentInParent<IToolTarget>();
            if (target != null && target.OnToolUsed(action, Mathf.Max(1, power), this))
            {
                lastToolHitPoint = hit.ClosestPoint(Origin);   // the spot on the tree / rock nearest the player
                return true;
            }
        }
        return false;
    }

    /// <summary>The little effects that go with a tool: dirt for the hoe, drops for the watering can, chips for the axe and pickaxe...</summary>
    private IEnumerator ToolEffects(ItemData.ToolAction action, List<Vector3Int> cells, float duration)
    {
        int order = (bodyRenderer != null ? bodyRenderer.sortingOrder : 10) + 2;
        int layer = bodyRenderer != null ? bodyRenderer.sortingLayerID : 0;
        if (farm == null) cells.Clear();        // no farm in this scene = no tiles to draw effects on

        if (action == ItemData.ToolAction.WateringCan)
        {
            // Drops fall from the spout onto the watered tiles for most of the pour, then each tile gets a small splash.
            Vector3 spout = Origin + Facing * 0.55f + Vector2.up * 0.15f;
            float start = duration * 0.2f, end = duration * 0.8f;
            yield return new WaitForSeconds(start);
            for (float t = start; t < end; t += 0.04f)
            {
                if (cells.Count > 0)
                {
                    Vector3 to = farm.CellToWorldCenter(cells[Random.Range(0, cells.Count)]);
                    ToolFx.WaterDrop(spout, to + (Vector3)(Random.insideUnitCircle * 0.25f), order, layer);
                }
                yield return new WaitForSeconds(0.04f);
            }
            foreach (Vector3Int cell in cells) ToolFx.Splash(farm.CellToWorldCenter(cell), order, layer);
            yield break;
        }

        // Everything else happens at the moment the tool lands.
        yield return new WaitForSeconds(duration * strikePoint);

        switch (action)
        {
            case ItemData.ToolAction.Hoe:
            case ItemData.ToolAction.Shovel:
                foreach (Vector3Int cell in cells) ToolFx.Dirt(farm.CellToWorldCenter(cell), order, layer);
                break;
            case ItemData.ToolAction.Sickle:
                ToolFx.Sweep(Origin, Facing, order, layer);
                foreach (Vector3Int cell in cells) ToolFx.Leaves(farm.CellToWorldCenter(cell), order, layer);
                break;
            case ItemData.ToolAction.Axe:
                ToolFx.WoodChips(lastToolHitPoint, order, layer);
                break;
            case ItemData.ToolAction.Pickaxe:
                ToolFx.StoneChips(lastToolHitPoint, order, layer);
                break;
        }
    }

    // ---- seeds and food ----

    private void Plant(ItemData seed)
    {
        // Tree seeds: plant a sapling on allowed ground (set in the TreeManager).
        TreeData treeKind = TreeManager.Instance != null ? TreeManager.Instance.FindBySeed(seed) : null;
        if (treeKind != null)
        {
            string why = TreeManager.Instance.WhyNot(TargetCell, true);
            if (why != null || TreeManager.Instance.Plant(TargetCell, treeKind, 0, false, true) == null)
            {
                GameEvents.ShowToast(why ?? "A tree can't grow here.");
                return;
            }
            inventory.RemoveFromSlot(inventory.SelectedIndex, 1);
            GameState.AddCounter("seeds_planted");
            StartCoroutine(Lock(toolDuration, UseToolTrigger, seed));
            return;
        }

        if (crops == null || farm == null) return;

        CropData crop = crops.FindBySeed(seed);
        if (crop == null)
        {
            GameEvents.ShowToast("No crop uses " + seed.itemName + " (add it to the CropManager).");
            return;
        }

        // Wrong season for this seed (or winter)? Say so instead of silently doing nothing.
        string notNow = crops.WhyNotPlantable(crop);
        if (notNow != null)
        {
            GameEvents.ShowToast(notNow);
            nextActionTime = Time.time + 0.4f;
            return;
        }

        if (!farm.IsTilled(TargetCell) || !crops.TryPlant(TargetCell, crop)) return;

        inventory.RemoveFromSlot(inventory.SelectedIndex, 1);
        GameState.AddCounter("seeds_planted");
        StartCoroutine(Lock(toolDuration, UseToolTrigger, seed));
    }

    private void Eat(ItemData food)
    {
        if (stats == null) return;
        bool needsHealth = stats.Health < stats.maxHealth && food.energyRestored > 0;
        bool needsMagic = stats.Magic < stats.maxMagic && food.magicRestored > 0;
        if (!needsHealth && !needsMagic && (food.energyRestored > 0 || food.magicRestored > 0))
        {
            GameEvents.ShowToast("You're not hurt.");
            return;
        }
        stats.Heal(food.energyRestored);
        stats.RestoreMagic(food.magicRestored);
        inventory.RemoveFromSlot(inventory.SelectedIndex, 1);
        GameState.AddCounter("food_eaten");
    }

    // ---- weapons ----

    private IEnumerator Swing(ItemData weapon)
    {
        IsBusy = true;
        Controller.CanMove = false;
        swingingWeapon = weapon;
        hitThisSwing.Clear();
        Trigger(AttackTrigger);
        if (OnItemUsed != null) OnItemUsed(weapon, weapon.swingDuration);

        if (weapon.hitDelay >= 0f)
        {
            yield return new WaitForSeconds(weapon.hitDelay);
            ApplyWeaponHit(weapon);
            yield return new WaitForSeconds(Mathf.Max(0f, weapon.swingDuration - weapon.hitDelay));
        }
        else
        {
            // Hits come from an Animation Event calling SwordHit() on this component.
            yield return new WaitForSeconds(weapon.swingDuration);
        }

        swingingWeapon = null;
        Controller.CanMove = true;
        IsBusy = false;
        nextActionTime = Time.time + weapon.attackCooldown;
    }

    /// <summary>Optional Animation Event target (same name as before). Only used when the weapon's Hit Delay is -1.</summary>
    public void SwordHit()
    {
        if (swingingWeapon != null) ApplyWeaponHit(swingingWeapon);
    }

    private void ApplyWeaponHit(ItemData weapon)
    {
        Vector2 center = Origin + Facing * weapon.attackRange;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, weapon.attackRadius))
        {
            if (hit.transform.IsChildOf(transform)) continue;
            var target = hit.GetComponentInParent<IDamageable>();
            if (target == null || target.DamageTeam == Team.Player || !hitThisSwing.Add(target)) continue;

            Vector2 push = ((Vector2)hit.bounds.center - Origin).normalized * weapon.knockbackForce;
            target.TakeDamage(new DamageInfo(weapon.damage, push, Team.Player, gameObject));
        }
    }

    // ---- spells ----

    private IEnumerator Cast(ItemData spell)
    {
        if (stats == null) yield break;

        if (spell.spellEffect == ItemData.SpellEffect.Heal && stats.Health >= stats.maxHealth)
        {
            GameEvents.ShowToast("You're not hurt.");
            yield break;
        }

        if (!stats.TrySpendMagic(spell.magicCost))
        {
            GameEvents.ShowToast("Not enough magic.");
            nextActionTime = Time.time + 0.4f;
            yield break;
        }

        IsBusy = true;
        Controller.CanMove = false;
        Trigger(CastTrigger);
        if (OnItemUsed != null) OnItemUsed(spell, spell.castTime);
        yield return new WaitForSeconds(spell.castTime);

        if (spell.spellEffect == ItemData.SpellEffect.Heal)
        {
            stats.Heal(spell.healAmount);
        }
        else
        {
            Projectile.Spawn(Origin + Facing * 0.5f, Facing, spell.projectileSpeed, spell.projectileLifetime,
                             spell.spellDamage, spell.knockbackForce, Team.Player, gameObject,
                             spell.projectileSprite, spell.projectileColor);
        }

        Controller.CanMove = true;
        IsBusy = false;
        nextActionTime = Time.time + 0.15f;
    }

    // ---- helpers ----

    private IEnumerator Lock(float seconds, int trigger, ItemData item)
    {
        IsBusy = true;
        Controller.CanMove = false;
        Trigger(trigger);
        if (OnItemUsed != null) OnItemUsed(item, seconds);
        yield return new WaitForSeconds(seconds);
        Controller.CanMove = true;
        IsBusy = false;
    }

    private void Trigger(int hash)
    {
        if (animator != null && animatorParams.Contains(hash)) animator.SetTrigger(hash);
    }

    private void OnDrawGizmosSelected()
    {
        var pc = GetComponent<PlayerController>();
        Vector2 f = pc != null ? pc.FacingDirection : Vector2.down;
        Vector2 o = (Vector2)transform.position + originOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(o + f * interactDistance, interactRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(o + f * targetDistance, Vector3.one * 0.9f);
    }
}
