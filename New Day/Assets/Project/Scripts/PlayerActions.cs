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
///        Tool   - Hoe tills, Watering Can waters, Sickle harvests, Shovel digs up a crop,
///                 Axe/Pickaxe hit trees and rocks (Breakable).
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

    // ---- public state ----
    public Inventory Inventory { get { return inventory; } }
    public PlayerStats Stats { get { return stats; } }
    public PlayerController Controller { get; private set; }
    public Vector2 Facing { get { return Controller.FacingDirection; } }
    public Vector3Int TargetCell { get; private set; }
    public bool HasTargetCell { get; private set; }
    public IInteractable CurrentInteractable { get; private set; }
    public bool IsBusy { get; private set; }

    /// <summary>Raised when an item starts being used: (item, how long the use lasts). Visual scripts listen to this.</summary>
    public event System.Action<ItemData, float> OnItemUsed;

    private SpriteRenderer highlight;
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
    }

    private void Start()
    {
        if (farm == null) farm = FindAnyObjectByType<FarmManager>();
        if (crops == null) crops = FindAnyObjectByType<CropManager>();

        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in animator.parameters) animatorParams.Add(p.nameHash);

        if (showTargetTile)
        {
            var go = new GameObject("TargetTile");
            highlight = go.AddComponent<SpriteRenderer>();
            highlight.sprite = RuntimeSprites.Square;
            highlight.color = targetColor;
            highlight.sortingOrder = 4;
            go.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (highlight != null) Destroy(highlight.gameObject);
    }

    private Vector2 Origin { get { return (Vector2)transform.position + originOffset; } }

    private void Update()
    {
        if (GameState.InputLocked || IsBusy || (stats != null && stats.IsDead))
        {
            if (highlight != null) highlight.gameObject.SetActive(false);
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
        if (!HasTargetCell) return;

        TargetCell = farm.WorldToCell(Origin + Facing * targetDistance);

        ItemData item = inventory != null ? inventory.EquippedItem : null;
        bool needsTile = item != null && (item.type == ItemData.ItemType.Tool || item.type == ItemData.ItemType.Seed);

        if (highlight != null)
        {
            bool show = needsTile && CurrentInteractable == null;
            highlight.gameObject.SetActive(show);
            if (show) highlight.transform.position = farm.CellToWorldCenter(TargetCell);
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
            case ItemData.ItemType.Weapon: StartCoroutine(Swing(item)); break;
            case ItemData.ItemType.Spell: StartCoroutine(Cast(item)); break;
            default: break;   // crops and resources can't be "used" - sell them at the shipping bin
        }
    }

    // ---- tools ----

    private void UseTool(ItemData tool)
    {
        bool did = false;

        switch (tool.toolAction)
        {
            case ItemData.ToolAction.Hoe:
                if (TreeManager.Instance != null && TreeManager.Instance.HasTree(TargetCell))
                {
                    GameEvents.ShowToast("A tree is in the way. Chop it down first.");
                    break;
                }
                did = farm != null && farm.TryTill(TargetCell);
                break;

            case ItemData.ToolAction.WateringCan:
                did = farm != null && farm.TryWater(TargetCell);
                break;

            case ItemData.ToolAction.Sickle:
                did = crops != null && crops.TryHarvest(TargetCell, inventory);
                break;

            case ItemData.ToolAction.Shovel:
                did = crops != null && crops.TryRemove(TargetCell);
                break;

            case ItemData.ToolAction.Axe:
            case ItemData.ToolAction.Pickaxe:
                did = HitToolTarget(tool.toolAction);
                break;

            case ItemData.ToolAction.FishingRod:
                GameEvents.ShowToast("Fishing isn't implemented yet.");
                break;

            default:
                GameEvents.ShowToast(tool.itemName + " has no Tool Action set. Pick one on the item.");
                break;
        }

        if (did) StartCoroutine(Lock(toolDuration, UseToolTrigger, tool));
    }

    private bool HitToolTarget(ItemData.ToolAction action)
    {
        Vector2 center = Origin + Facing * interactDistance;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(center, interactRadius))
        {
            var target = hit.GetComponentInParent<IToolTarget>();
            if (target != null && target.OnToolUsed(action, this)) return true;
        }
        return false;
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
