using System;
using System.Collections.Generic;

// Everything in this file is plain data: it describes what one save file holds.
// SaveSystem fills these in, turns them into text (JSON) and writes that text to disk.
// Things are remembered by NAME (an item's asset name, a crop's asset name), because a save
// file cannot hold a link to a Unity asset.

/// <summary>One stack of items (a backpack slot or a mailbox slot). An empty slot has item = "".</summary>
[Serializable]
public class SavedStack
{
    public string item = "";
    public int count;
    public int pollution;
    public int quality;
}

/// <summary>One tilled soil tile.</summary>
[Serializable]
public class SavedSoil
{
    public int x, y;
    public bool watered;
}

/// <summary>One crop growing on a tile.</summary>
[Serializable]
public class SavedCrop
{
    public int x, y;
    public string crop = "";        // the CropData asset's name
    public int daysGrown;
    public float pollutionSum;
    public int samples;
    public bool withered;
}

/// <summary>The soil and crops of one scene, and the day they were written down on.</summary>
[Serializable]
public class SavedFarm
{
    public string scene = "";
    public int totalDays;
    public List<SavedSoil> soil = new List<SavedSoil>();
    public List<SavedCrop> crops = new List<SavedCrop>();
}

/// <summary>Something SoilCare put on a tile: moisture gel, fertilizer or a sprinkler.</summary>
[Serializable]
public class SavedSoilMark
{
    public string scene = "";
    public int kind;                // 0 = moisture gel, 1 = fertilizer, 2 = sprinkler
    public int x, y;
    public int value;               // gel: mornings left.  fertilizer: tier.  sprinkler: not used
    public string item = "";        // sprinkler: the item's asset name
}

/// <summary>The calendar and the time of day.</summary>
[Serializable]
public class SavedClock
{
    public bool valid;              // false = nothing remembered yet, use the TimeManager's Inspector values
    public int day, year, weekDay, totalDays, season;
    public float minutes;
}

/// <summary>One quest in the journal.</summary>
[Serializable]
public class SavedQuest
{
    public string id = "", title = "", giver = "", description = "";
    public bool done;
}

/// <summary>Somebody the player has met (their friendship points are in the GameState counters).</summary>
[Serializable]
public class SavedPerson
{
    public string id = "", name = "";
}

/// <summary>One piece of farm clutter (a weed, a stone, litter, a fallen tree) lying on the farm.</summary>
[Serializable]
public class SavedDebris
{
    public string kind = "";        // the Kind id in FarmDebrisSettings, for example "weed_tuft"
    public float x, y;              // where it stands (its feet)
}

/// <summary>A whole saved game. One of these is written to each save slot.</summary>
[Serializable]
public class SaveFile
{
    public int version = 1;

    // ---- shown on the title screen, so a slot can be described without loading it ----
    public string savedAt = "";     // real-world date and time
    public string dateText = "";    // "Tue, Spring 5"
    public int year = 1;
    public int gold;

    // ---- the game ----
    public string gameState = "";   // gold, flags and counters (GameState.SaveData)
    public string house = "";       // house upgrades owned and in use (HouseManager.SaveRemembered)
    public SavedClock clock = new SavedClock();
    public List<SavedStack> backpack = new List<SavedStack>();
    public int selectedSlot;
    public List<SavedStack> mailbox = new List<SavedStack>();
    public List<string> equipment = new List<string>();
    public List<SavedFarm> farms = new List<SavedFarm>();
    public List<SavedSoilMark> soilCare = new List<SavedSoilMark>();
    public List<SavedQuest> quests = new List<SavedQuest>();
    public List<SavedPerson> people = new List<SavedPerson>();

    // ---- farm clutter (FarmDebrisSpawner). Saves made before this existed have hasDebris = false: the farm gets a fresh scatter.
    public bool hasDebris;
    public int debrisDay = -1;      // the day of the last morning spawn (-1 = the farm has not been scattered yet)
    public List<SavedDebris> debris = new List<SavedDebris>();
}
