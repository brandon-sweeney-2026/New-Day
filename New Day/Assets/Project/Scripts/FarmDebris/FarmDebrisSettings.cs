using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All the numbers for the random clutter that turns up on the farm: pollution litter, weeds, stones and fallen trees.
///
/// The asset lives at Assets/Resources/FarmDebrisSettings.asset. Farm > Set Up > Farm Debris makes it (and the prefabs it lists).
/// Running that menu again KEEPS your numbers; delete the asset to get the starting numbers back.
///
/// HOW IT WORKS
///   - Every thing that can appear is a "Kind": a prefab, which group it belongs to and a weight (bigger weight = picked more often
///     inside its group). Example: cans 4, debris 3, black lumps 2, purple lumps 1 means a purple lump is 1 in 10 pollution spawns.
///   - Every group has a "Group Rule": how many are there on a brand new farm, how many new ones appear each morning on average
///     (1.5 = one or two), the most there can ever be, and the seasons it appears in.
///   - Pollution is special: its "per day" number grows with every dealing with Sterling (see SterlingInfluence) and with how dirty
///     the river is (RiverPollution.Level 0-5).
/// </summary>
[CreateAssetMenu(fileName = "FarmDebrisSettings", menuName = "Farm/Farm Debris Settings")]
public class FarmDebrisSettings : ScriptableObject
{
    /// <summary>The four kinds of clutter. Each has its own Group Rule.</summary>
    public enum Group { Pollution, Weed, Stone, FallenTree }

    [System.Serializable]
    public class Kind
    {
        [Tooltip("A short name, used to remember what lay where (for example 'can'). Must be different for every kind.")]
        public string id = "thing";
        public Group group = Group.Weed;
        [Tooltip("The prefab that is placed. Weeds and trees need a Breakable, litter a LitterPickup.")]
        public GameObject prefab;
        [Min(0f), Tooltip("Inside its group, a bigger weight is picked more often.")]
        public float weight = 1f;
    }

    [System.Serializable]
    public class GroupRule
    {
        public Group group = Group.Weed;
        [Min(0), Tooltip("Brand new farm. Pollution: exactly this many pieces. Weeds, stones and fallen trees: their SHARE of the tiles that Start Coverage fills "
                       + "(weeds 12, stones 8, trees 2 = 12 out of every 22 covered tiles are weeds). With Start Coverage at 0 it is an exact count for every group.")]
        public int startCount = 5;
        [Min(0f), Tooltip("How many new ones appear each morning on average. 0.25 = one about every four days, 1.5 = one or two a day.")]
        public float perDay = 1f;
        [Min(0), Tooltip("Never more than this many on the farm at once.")]
        public int maxOnFarm = 20;
        [Tooltip("Seasons in which new ones appear (ones already there stay).")]
        public bool spring = true, summer = true, autumn = true, winter = true;

        public bool AppearsIn(TimeManager.Season s)
        {
            switch (s)
            {
                case TimeManager.Season.Spring: return spring;
                case TimeManager.Season.Summer: return summer;
                case TimeManager.Season.Autumn: return autumn;
                default: return winter;
            }
        }
    }

    [Header("Where")]
    [Tooltip("The scene the clutter appears in.")]
    public string farmSceneName = "Farm";
    [Tooltip("ON = things may land on paths too. OFF = paths stay clear.")]
    public bool allowOnPaths = false;
    [Min(0f), Tooltip("New things never appear closer than this to the player (so nothing pops up under your feet).")]
    public float keepAwayFromPlayer = 2.5f;
    [Min(0f), Tooltip("Things keep at least this far from each other.")]
    public float minSpacing = 1.2f;
    [Min(1), Tooltip("When you come back after nights away, at most this many days of new clutter are added at once.")]
    public int maxCatchUpDays = 7;

    [Header("Starting farm")]
    [Range(0f, 1f), Tooltip("How much of the free ground a brand new farm starts covered in: 0.5 = half of every tile weeds, stones and fallen trees "
                         + "may stand on (not paths, water, soil, buildings or keep-clear spots). Split between the groups by their Start Count. "
                         + "0 = use each group's Start Count as an exact number instead. The Max On Farm limits don't apply to this first scatter, "
                         + "so no new weeds, stones or trees appear until you have cleared the farm below those limits.")]
    public float startCoverage = 0.5f;

    [Header("What can appear")]
    public List<Kind> kinds = new List<Kind>();
    public List<GroupRule> rules = new List<GroupRule>();

    [Header("Pollution and Sterling")]
    [Min(0f), Tooltip("Extra pollution spawns per day for EVERY dealing with Sterling (a purchase in his tower, a smelt, a daily chat). 0.03 = 10 dealings add 0.3 a day.")]
    public float pollutionPerDayPerDealing = 0.03f;
    [Min(0f), Tooltip("Extra pollution spawns per day for each river pollution level (0-5).")]
    public float pollutionPerDayPerRiverLevel = 0.1f;
    [Min(0f), Tooltip("Pollution spawns per day never go above this, however much you deal with Sterling.")]
    public float pollutionMaxPerDay = 3f;
    [Min(0), Tooltip("The most litter the farm can hold also grows by 1 for this many dealings (0 = it never grows).")]
    public int dealingsPerExtraLitterRoom = 4;

    // ------------------------------------------------------------------ helpers

    public GroupRule RuleFor(Group g)
    {
        foreach (GroupRule r in rules) if (r != null && r.group == g) return r;
        return null;
    }

    public Kind KindById(string id)
    {
        foreach (Kind k in kinds) if (k != null && k.id == id) return k;
        return null;
    }

    /// <summary>Picks a kind of the group at random, using the weights. Null if the group has none.</summary>
    public Kind PickKind(Group g)
    {
        float total = 0f;
        foreach (Kind k in kinds) if (k != null && k.group == g && k.prefab != null) total += k.weight;
        if (total <= 0f) return null;
        float roll = Random.value * total;
        Kind last = null;
        foreach (Kind k in kinds)
        {
            if (k == null || k.group != g || k.prefab == null) continue;
            last = k;
            roll -= k.weight;
            if (roll <= 0f) return k;
        }
        return last;
    }

    /// <summary>How many of this group appear per day right now. Pollution goes up with Sterling dealings and a dirty river.</summary>
    public float PerDay(GroupRule r)
    {
        if (r == null) return 0f;
        if (r.group != Group.Pollution) return r.perDay;
        float p = r.perDay
                + SterlingInfluence.Dealings * pollutionPerDayPerDealing
                + RiverPollution.Level * pollutionPerDayPerRiverLevel;
        return Mathf.Min(p, pollutionMaxPerDay);
    }

    /// <summary>The most of this group the farm can hold right now (litter room grows with Sterling dealings).</summary>
    public int MaxOnFarm(GroupRule r)
    {
        if (r == null) return 0;
        if (r.group != Group.Pollution || dealingsPerExtraLitterRoom <= 0) return r.maxOnFarm;
        return r.maxOnFarm + SterlingInfluence.Dealings / dealingsPerExtraLitterRoom;
    }

    private static FarmDebrisSettings current;

    /// <summary>The settings in use: the asset in Resources, or an empty set (nothing spawns) if there is none.</summary>
    public static FarmDebrisSettings Current
    {
        get
        {
            if (current == null) current = Resources.Load<FarmDebrisSettings>("FarmDebrisSettings");
            return current;
        }
    }
}
