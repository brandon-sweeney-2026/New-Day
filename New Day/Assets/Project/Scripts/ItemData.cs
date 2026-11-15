using UnityEngine;

/// <summary>
/// Defines one kind of item (seeds, crops, tools, weapons, spells, resources).
/// Create instances in the editor: right-click in Project window > Create > Farm > Item.
///
/// UPDATED: added Weapon and Spell types, tool actions, combat stats and pollution-aware selling.
/// New enum values were added at the END, so items you already made keep their type.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Farm/Item")]
public class ItemData : ScriptableObject
{
    public enum ItemType { Seed, Crop, Tool, Resource, Food, Weapon, Spell }

    /// <summary>What a Tool does when you press the action button.</summary>
    public enum ToolAction { None, Hoe, WateringCan, Sickle, Axe, Pickaxe, Shovel, FishingRod }

    public enum SpellEffect { Projectile, Heal }

    /// <summary>
    /// Which fighting style a Weapon uses. Generic = the old simple swing. The others are handled by WeaponController
    /// (add that to the Player: Farm > Set Up > Weapons).
    /// </summary>
    public enum WeaponKind { Generic, Sword, Dagger, Flail, Hammer, Bow }

    /// <summary>
    /// Material tier (the same metals as the ores), used by weapons AND tools. It is only a label: what a tool actually does
    /// comes from its Tool Reach and Tool Power numbers, so you can tune each tool in the Inspector.
    /// </summary>
    public enum WeaponTier { Stone, Copper, Iron, Gold, Platinum }

    [Header("Basics")]
    public string itemName = "New Item";
    [TextArea] public string description;
    public Sprite icon;
    public ItemType type = ItemType.Resource;

    [Header("Inventory")]
    [Min(1)] public int maxStack = 99;

    [Header("Economy")]
    [Min(0)] public int sellPrice = 10;
    [Min(0)] public int buyPrice = 0; // 0 = not sold in the shop

    [Header("Food (only used when type is Food)")]
    [Min(0)] public int energyRestored = 0;   // restores this much health
    [Min(0)] public int magicRestored = 0;    // restores this much magic

    [Header("Tool (only used when type is Tool)")]
    public ToolAction toolAction = ToolAction.None;
    [Min(1), Tooltip("Hoe, Watering Can and Sickle/Scythe: how many tiles in a row in front of you one use covers. 1 = just the tile you face.")]
    public int toolReach = 1;
    [Min(1), Tooltip("Axe and Pickaxe: how many hits one swing counts as. 2 = trees and rocks break in half the swings.")]
    public int toolPower = 1;

    [Header("Seed (only used when type is Seed)")]
    [Tooltip("The seasons this seed can be planted and will keep growing in. A crop still in the ground when its last season ends withers.")]
    public bool plantInSpring = true;
    public bool plantInSummer = true;
    public bool plantInAutumn = true;
    [Tooltip("Leave this OFF for crops: nothing grows in winter.")]
    public bool plantInWinter = false;

    [Header("Weapon (only used when type is Weapon)")]
    [Min(0)] public int damage = 5;
    [Tooltip("How far in front of the player the hit area is centred.")]
    public float attackRange = 0.8f;
    public float attackRadius = 0.6f;
    public float knockbackForce = 6f;
    [Tooltip("How long the player is locked in the swing. Match your animation length (frames / frame rate).")]
    public float swingDuration = 0.5f;
    [Tooltip("Seconds into the swing when the blade connects. Use -1 to hit from an Animation Event named SwordHit instead.")]
    public float hitDelay = 0.25f;
    public float attackCooldown = 0.1f;

    [Header("Weapon style (new combat system - used when Weapon Kind is not Generic)")]
    public WeaponKind weaponKind = WeaponKind.Generic;
    [Tooltip("Stone, Copper, Iron, Gold or Platinum. Used by weapons and by the upgradable tools. A label only.")]
    public WeaponTier tier = WeaponTier.Stone;
    [Range(0f, 1f), Tooltip("Chance for a hit to be a critical strike.")] public float critChance = 0.05f;
    [Min(1f), Tooltip("Damage multiplier on a critical strike (2 = double).")] public float critMultiplier = 2f;
    [Min(0.1f), Tooltip("Swing speed. 1 = normal, 1.5 = 50% faster. (Bow: also draws faster.)")] public float swingSpeed = 1f;
    [Range(0f, 1.5f), Tooltip("Walking speed while the weapon is in use (1 = normal, 0.5 = half). Used by the flail and the bow.")] public float moveSpeedWhileUsing = 1f;
    [Tooltip("Hammer: radius of the splash around the impact. Splash targets take Splash Damage % of the damage.")] public float splashRadius = 1.4f;
    [Range(0f, 1f)] public float splashDamagePercent = 0.6f;
    [Tooltip("Flail: how far the ball swings from the player, in tiles.")] public float flailRadius = 1.5f;
    [Tooltip("Flail: degrees per second when it starts turning, and at full speed.")] public float flailStartSpin = 110f;
    public float flailMaxSpin = 620f;
    [Tooltip("Flail: seconds to reach full speed.")] public float flailSpinUpTime = 1.3f;
    [Tooltip("Flail: the longest it will keep spinning while you hold the button.")] public float flailMaxSeconds = 3.5f;
    [Tooltip("Bow: seconds to draw the bow fully (smaller = faster).")] public float chargeSeconds = 1f;
    [Tooltip("Bow: the arrows it uses up. Each shot uses one.")] public ItemData ammo;
    [Tooltip("Pictures used while the weapon is in use. Held picture = the weapon in the hand (leave empty to use the icon). " +
             "Flail: held picture = the handle, Ball Sprite = the spiked ball. Bow: 3 draw frames (relaxed, half, full).")]
    public Sprite heldSprite;
    public Sprite ballSprite;
    public Sprite[] drawFrames;

    [Header("Spell (only used when type is Spell)")]
    public SpellEffect spellEffect = SpellEffect.Projectile;
    [Min(0)] public float magicCost = 10f;
    public int spellDamage = 8;
    public float projectileSpeed = 8f;
    public float projectileLifetime = 1.2f;
    public Sprite projectileSprite;            // leave empty for a plain glowing dot
    public Color projectileColor = new Color(1f, 0.55f, 0.15f);
    public int healAmount = 20;
    public float castTime = 0.25f;

    /// <summary>Can this seed be planted (and keep growing) in this season?</summary>
    public bool CanPlantIn(TimeManager.Season season)
    {
        switch (season)
        {
            case TimeManager.Season.Spring: return plantInSpring;
            case TimeManager.Season.Summer: return plantInSummer;
            case TimeManager.Season.Autumn: return plantInAutumn;
            default: return plantInWinter;
        }
    }

    /// <summary>The seed's seasons as text for the shop and for messages, for example "Spring, Summer".</summary>
    public string PlantSeasonsText()
    {
        var names = new System.Collections.Generic.List<string>();
        if (plantInSpring) names.Add("Spring");
        if (plantInSummer) names.Add("Summer");
        if (plantInAutumn) names.Add("Autumn");
        if (plantInWinter) names.Add("Winter");
        return names.Count == 0 ? "no season" : string.Join(", ", names.ToArray());
    }

    /// <summary>
    /// What one of this item sells for. A pollution level (0-5) lowers the price using the
    /// multipliers in your Pollution Settings asset.
    /// </summary>
    public int GetSellPrice(int pollutionLevel)
    {
        float multiplier = PollutionSettings.Current.SaleMultiplier(pollutionLevel);
        return Mathf.Max(0, Mathf.RoundToInt(sellPrice * multiplier));
    }
}
