using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Every number, list and colour the player menu uses, in one asset you can edit in the Inspector:
/// the keys that open it, how fast skills level up, the perks, the crafting recipes, the achievements
/// and how friendship is earned.
///
/// The asset lives at Assets/Resources/PlayerMenuSettings.asset. Make it with Farm > Set Up > Player Menu.
/// If the asset does not exist yet the game still works: it uses the built-in defaults written below.
/// </summary>
[CreateAssetMenu(fileName = "PlayerMenuSettings", menuName = "Farm/Player Menu Settings")]
public class PlayerMenuSettings : ScriptableObject
{
    /// <summary>The kinds of bonus a perk can give. (Equipment gives the same bonuses through its ItemData fields.)</summary>
    public enum BonusKind { MaxHealth, MaxMagic, Defense, MoveSpeedPercent, DamagePercent, AxePower, PickaxePower, ExtraCropChancePercent }

    /// <summary>What an achievement watches: a GameState counter, a GameState flag, or how much gold you hold.</summary>
    public enum GoalKind { Counter, Flag, Gold }

    /// <summary>"Every time this counter goes up by 1, give this much XP to this skill."</summary>
    [Serializable]
    public class XpRule
    {
        [Tooltip("The GameState counter to watch, for example crops_harvested or mined_rocks.")]
        public string counter = "";
        [Tooltip("Tick to match every counter that STARTS with the text above (killed_ matches killed_Bat, killed_Bug...).")]
        public bool startsWith;
        public PlayerSkills.Skill skill;
        [Min(0)] public int xpEach = 1;

        public XpRule() { }      // (Unity needs an empty constructor to add a new one in the Inspector)

        public XpRule(string counter, bool startsWith, PlayerSkills.Skill skill, int xpEach)
        {
            this.counter = counter; this.startsWith = startsWith; this.skill = skill; this.xpEach = xpEach;
        }
    }

    /// <summary>One box in a skill's experience tree. Each level costs one perk point.</summary>
    [Serializable]
    public class Perk
    {
        [Tooltip("Unique short name with no spaces. Used to remember how many points are in it.")]
        public string id = "perk";
        public PlayerSkills.Skill skill;
        public string perkName = "New Perk";
        [TextArea] public string description = "";
        [Min(1)] public int maxLevel = 5;
        public BonusKind bonus;
        [Tooltip("How much of the bonus one level gives.")]
        public float amountPerLevel = 1f;

        public Perk() { }

        public Perk(string id, PlayerSkills.Skill skill, string perkName, string description, int maxLevel, BonusKind bonus, float amountPerLevel)
        {
            this.id = id; this.skill = skill; this.perkName = perkName; this.description = description;
            this.maxLevel = maxLevel; this.bonus = bonus; this.amountPerLevel = amountPerLevel;
        }
    }

    /// <summary>One achievement.</summary>
    [Serializable]
    public class Achievement
    {
        [Tooltip("Unique short name with no spaces. Used to remember that it is done.")]
        public string id = "achievement";
        public string title = "New Achievement";
        [TextArea] public string description = "";
        public Sprite icon;
        public GoalKind kind = GoalKind.Counter;
        [Tooltip("Counter or flag name (not used for Gold). For example crops_harvested.")]
        public string key = "";
        [Tooltip("The number to reach (not used for Flag).")]
        [Min(1)] public int target = 1;
        [Tooltip("Gold given when it is earned (0 = none).")]
        [Min(0)] public int rewardGold = 0;

        public Achievement() { }

        public Achievement(string id, string title, string description, GoalKind kind, string key, int target, int rewardGold)
        {
            this.id = id; this.title = title; this.description = description;
            this.kind = kind; this.key = key; this.target = target; this.rewardGold = rewardGold;
        }
    }

    [Header("Opening the menu")]
    [Tooltip("Opens the menu, and closes it again from anywhere inside it.")]
    public Key openKey = Key.Tab;
    [Tooltip("A second key that opens the menu. Inside the menu Esc always means 'go back'. Set to None to turn off.")]
    public Key secondOpenKey = Key.Escape;

    [Header("Look")]
    [Tooltip("Leave empty to use Unity's built-in font.")]
    public Font font;
    public Color dimColor = new Color(0f, 0f, 0f, 0.55f);
    public Color panelColor = new Color(0.14f, 0.1f, 0.08f, 0.97f);
    public Color tabColor = new Color(0.24f, 0.17f, 0.11f, 1f);
    public Color tabSelectedColor = new Color(0.55f, 0.38f, 0.16f, 1f);
    public Color rowColor = new Color(1f, 1f, 1f, 0.06f);
    public Color rowSelectedColor = new Color(1f, 0.85f, 0.3f, 0.35f);
    public Color detailPanelColor = new Color(0f, 0f, 0f, 0.3f);
    public Color detailTextColor = new Color(0.9f, 0.85f, 0.7f);
    public Color headingColor = new Color(0.95f, 0.75f, 0.4f);
    public Color goodColor = new Color(0.7f, 1f, 0.7f);

    [Header("Skills")]
    [Tooltip("XP needed for level 1. Level L needs this x L x (L+1) / 2 in total (with 40: 40, 120, 240, 400...).")]
    [Min(1)] public int xpForFirstLevel = 40;
    [Tooltip("What gives XP. The game already counts these things in GameState counters.")]
    public List<XpRule> xpRules = DefaultXpRules();
    [Tooltip("The experience trees. Every skill level gives one point to spend on that skill's perks.")]
    public List<Perk> perks = DefaultPerks();

    [Header("Crafting")]
    [Tooltip("Recipes shown in the Crafting tab. Tick 'Crafted' on each Recipe so it is not counted as a cooked meal.")]
    public List<Recipe> craftingRecipes = new List<Recipe>();

    [Header("Achievements")]
    public List<Achievement> achievements = DefaultAchievements();

    [Header("Friendship")]
    [Min(1)] public int pointsPerHeart = 100;
    [Range(1, 20)] public int maxHearts = 10;
    [Tooltip("Points for the first chat with someone each day.")]
    public int talkPoints = 10;
    [Tooltip("Points for one gift (one gift per person per day). Loves / Likes / Dislikes come from the NPC Profile.")]
    public int lovedGiftPoints = 80;
    public int likedGiftPoints = 45;
    public int neutralGiftPoints = 20;
    public int dislikedGiftPoints = -30;

    // ------------------------------------------------------------------
    // Access (built-in defaults are used when the asset has not been made yet)
    // ------------------------------------------------------------------

    private static PlayerMenuSettings current;

    /// <summary>The settings the game is using: the asset in Resources, or the defaults if there isn't one.</summary>
    public static PlayerMenuSettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<PlayerMenuSettings>("PlayerMenuSettings");
            if (current == null)
            {
                current = CreateInstance<PlayerMenuSettings>();
                current.hideFlags = HideFlags.DontSave;
            }
            return current;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        current = null;     // look for the asset again each time Play is pressed
    }

    // ------------------------------------------------------------------
    // Defaults
    // ------------------------------------------------------------------

    public static List<XpRule> DefaultXpRules()
    {
        return new List<XpRule>
        {
            new XpRule("crops_harvested", false, PlayerSkills.Skill.Farming, 4),
            new XpRule("seeds_planted", false, PlayerSkills.Skill.Farming, 2),
            new XpRule("meals_cooked", false, PlayerSkills.Skill.Farming, 3),
            new XpRule("mined_rocks", false, PlayerSkills.Skill.Mining, 6),
            new XpRule("broke_Pickaxe", false, PlayerSkills.Skill.Mining, 4),
            new XpRule("trees_chopped", false, PlayerSkills.Skill.Foraging, 10),
            new XpRule("broke_Axe", false, PlayerSkills.Skill.Foraging, 4),
            new XpRule("broke_Sickle", false, PlayerSkills.Skill.Foraging, 2),
            new XpRule("killed_", true, PlayerSkills.Skill.Combat, 8),
            new XpRule("shadows_banished", false, PlayerSkills.Skill.Combat, 10),
        };
    }

    public static List<Perk> DefaultPerks()
    {
        return new List<Perk>
        {
            new Perk("green_thumb", PlayerSkills.Skill.Farming, "Green Thumb", "8% chance per level of one extra crop from each harvest.", 5, BonusKind.ExtraCropChancePercent, 8f),
            new Perk("hearty", PlayerSkills.Skill.Farming, "Hearty Meals", "+5 maximum health per level.", 5, BonusKind.MaxHealth, 5f),

            new Perk("strong_arm", PlayerSkills.Skill.Mining, "Strong Arm", "Your pickaxe counts as 1 more hit per level.", 3, BonusKind.PickaxePower, 1f),
            new Perk("stone_skin", PlayerSkills.Skill.Mining, "Stone Skin", "+1 defense per level (each point takes 1 off every hit).", 3, BonusKind.Defense, 1f),

            new Perk("lumberjack", PlayerSkills.Skill.Foraging, "Lumberjack", "Your axe counts as 1 more hit per level.", 3, BonusKind.AxePower, 1f),
            new Perk("light_feet", PlayerSkills.Skill.Foraging, "Light Feet", "Walk 4% faster per level.", 5, BonusKind.MoveSpeedPercent, 4f),

            new Perk("sharp_edge", PlayerSkills.Skill.Combat, "Sharp Edge", "Deal 6% more damage per level.", 5, BonusKind.DamagePercent, 6f),
            new Perk("tough", PlayerSkills.Skill.Combat, "Tough", "+10 maximum health per level.", 5, BonusKind.MaxHealth, 10f),
            new Perk("clear_mind", PlayerSkills.Skill.Combat, "Clear Mind", "+5 maximum magic per level.", 5, BonusKind.MaxMagic, 5f),
        };
    }

    public static List<Achievement> DefaultAchievements()
    {
        return new List<Achievement>
        {
            new Achievement("first_harvest", "First Harvest", "Harvest your first crop.", GoalKind.Counter, "crops_harvested", 1, 25),
            new Achievement("green_fields", "Green Fields", "Harvest 100 crops.", GoalKind.Counter, "crops_harvested", 100, 250),
            new Achievement("sower", "Sower", "Plant 50 seeds.", GoalKind.Counter, "seeds_planted", 50, 100),
            new Achievement("home_cook", "Home Cook", "Cook 10 meals.", GoalKind.Counter, "meals_cooked", 10, 100),
            new Achievement("handy", "Handy", "Craft something from the Crafting tab.", GoalKind.Counter, "items_crafted", 1, 25),
            new Achievement("rock_breaker", "Rock Breaker", "Mine 50 rocks.", GoalKind.Counter, "mined_rocks", 50, 150),
            new Achievement("timber", "Timber!", "Chop down 25 trees.", GoalKind.Counter, "trees_chopped", 25, 150),
            new Achievement("first_catch", "First Catch", "Catch a fish.", GoalKind.Counter, "fish_caught", 1, 25),
            new Achievement("master_angler", "Master Angler", "Catch 50 fish.", GoalKind.Counter, "fish_caught", 50, 300),
            new Achievement("merchant", "Merchant", "Sell 100 items.", GoalKind.Counter, "items_sold", 100, 200),
            new Achievement("nest_egg", "Nest Egg", "Hold 5000 gold at once.", GoalKind.Gold, "", 5000, 0),
            new Achievement("shadow_chaser", "Shadow Chaser", "Banish 10 shadows in the forest.", GoalKind.Counter, "shadows_banished", 10, 200),
            new Achievement("grove", "The Hidden Grove", "Reach the grove at the heart of the forest.", GoalKind.Flag, "forest_grove_reached", 1, 100),
            new Achievement("fairy_friend", "Fairy's Helper", "Finish a job for the Fairy.", GoalKind.Counter, "fairy_quests_done", 1, 100),
            new Achievement("home_improvement", "Home Improvement", "Buy an upgrade for the farmhouse.", GoalKind.Counter, "house_upgrades", 1, 50),
            new Achievement("new_in_town", "New In Town", "Meet 5 people.", GoalKind.Counter, "people_met", 5, 50),
            new Achievement("good_neighbour", "Good Neighbour", "Give 10 gifts.", GoalKind.Counter, "gifts_given", 10, 150),
        };
    }
}
