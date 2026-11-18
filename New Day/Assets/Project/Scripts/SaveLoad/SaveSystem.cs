using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saves the game to a file and loads it again. There are three save slots.
///
/// WHEN IT SAVES: by itself at the end of every day (DayRecap.FinishDay calls AutoSave), and once when a
/// new game is started from the title screen. There is no "save now" button: like most farming games, a day
/// you do not finish is not kept.
///
/// WHAT IS SAVED: the date, gold, story flags and counters (so also skills, perks, friendship, achievements
/// and mine progress), the backpack, the mailbox, worn equipment, house upgrades, tilled soil and crops,
/// sprinklers / fertilizer / moisture gel, the quest journal, the people you have met, and the farm clutter
/// (weeds, stones, litter, fallen trees: FarmDebrisSpawner).
/// NOT saved yet: trees, pollution painted on the farm, items lying on the ground, and where you are standing
/// (a loaded game always starts in bed on the farm, on the morning of the saved day).
///
/// WHERE: one text file per slot (save_slot1.json ...) in Unity's "persistent data" folder.
/// Farm > Test and Debug > Open Save Folder shows it.
///
/// This class also keeps the "session memory": the clock and each scene's soil and crops are remembered
/// while you are in another scene, so they are still right when you walk back.
///
/// This is a plain static class, so there is nothing to put in a scene.
/// </summary>
public static class SaveSystem
{
    /// <summary>How many save slots the title screen offers.</summary>
    public const int SlotCount = 3;

    /// <summary>The scene a loaded game starts in (the one with the bed).</summary>
    public const string HomeScene = "Farm";

    /// <summary>A farm you were away from for longer than this only catches up this many nights of growth.</summary>
    private const int MaxCatchUpDays = 28;

    /// <summary>The slot this game saves into (0, 1 or 2), or -1 when the game was not started from the title screen.</summary>
    public static int CurrentSlot { get; private set; }

    /// <summary>True from the moment a save is applied until its scene has finished loading.</summary>
    public static bool IsLoading { get; private set; }

    /// <summary>The day and time, carried from scene to scene. 'valid' is false until a TimeManager has written it.</summary>
    public static SavedClock Clock { get; private set; }

    // The soil and crops of each scene, by scene name.
    private static readonly Dictionary<string, SavedFarm> farms = new Dictionary<string, SavedFarm>();

    // After loading a save the player has to be put next to the bed once the scene is ready.
    private static bool wakeAtBedAfterLoad;

    // Static data survives between Play sessions when "Enter Play Mode Options" is on, so reset it explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        CurrentSlot = -1;
        IsLoading = false;
        Clock = new SavedClock();
        farms.Clear();
        wakeAtBedAfterLoad = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // ------------------------------------------------------------------
    // Slots and files
    // ------------------------------------------------------------------

    /// <summary>The folder the save files are in.</summary>
    public static string SaveFolder { get { return Application.persistentDataPath; } }

    private static string PathOf(int slot)
    {
        return Path.Combine(SaveFolder, "save_slot" + (slot + 1) + ".json");
    }

    public static bool HasSave(int slot)
    {
        return slot >= 0 && slot < SlotCount && File.Exists(PathOf(slot));
    }

    public static bool AnySaveExists
    {
        get { for (int i = 0; i < SlotCount; i++) if (HasSave(i)) return true; return false; }
    }

    /// <summary>The slot that was saved last, or -1 if there are no saves. Used by "Continue".</summary>
    public static int MostRecentSlot()
    {
        int best = -1;
        DateTime bestTime = DateTime.MinValue;
        for (int i = 0; i < SlotCount; i++)
        {
            if (!HasSave(i)) continue;
            DateTime written = File.GetLastWriteTimeUtc(PathOf(i));
            if (best < 0 || written > bestTime) { best = i; bestTime = written; }
        }
        return best;
    }

    /// <summary>A short description of a slot for the title screen, for example "Tue, Spring 5, Year 1     1,240 G".</summary>
    public static string Describe(int slot)
    {
        if (!HasSave(slot)) return "Empty";
        SaveFile file = Read(slot);
        if (file == null) return "This save can't be read";
        return file.dateText + ", Year " + file.year + "     " + file.gold.ToString("N0") + " G";
    }

    /// <summary>When a slot was last saved (real-world date and time), or "".</summary>
    public static string SavedAt(int slot)
    {
        SaveFile file = HasSave(slot) ? Read(slot) : null;
        return file != null ? file.savedAt : "";
    }

    /// <summary>Removes a slot's save file for good.</summary>
    public static void DeleteSlot(int slot)
    {
        try
        {
            if (HasSave(slot)) File.Delete(PathOf(slot));
        }
        catch (Exception e)
        {
            Debug.LogError("Save: could not delete slot " + (slot + 1) + ". " + e.Message);
        }
    }

    /// <summary>Reads a slot's file. Returns null if there is none or it is damaged.</summary>
    public static SaveFile Read(int slot)
    {
        if (!HasSave(slot)) return null;
        try
        {
            SaveFile file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(PathOf(slot)));
            // A file that is not a save at all comes back with no game data in it.
            return file != null && !string.IsNullOrEmpty(file.gameState) ? file : null;
        }
        catch (Exception e)
        {
            Debug.LogError("Save: could not read slot " + (slot + 1) + ". " + e.Message);
            return null;
        }
    }

    private static bool Write(int slot, SaveFile file)
    {
        try
        {
            // Write to a spare file first and then swap it in. If the game is closed halfway through
            // writing, the old save is still whole.
            string path = PathOf(slot);
            string spare = path + ".tmp";
            File.WriteAllText(spare, JsonUtility.ToJson(file, true));
            File.Copy(spare, path, true);
            File.Delete(spare);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Save: could not write slot " + (slot + 1) + ". " + e.Message);
            return false;
        }
    }

    // ------------------------------------------------------------------
    // Starting, saving and loading a game
    // ------------------------------------------------------------------

    /// <summary>
    /// Begins a new game in a slot (anything already saved there is replaced) and saves it straight away,
    /// so the slot shows up under "Load Game" even if you quit before the first night.
    /// </summary>
    public static bool StartNewGame(int slot)
    {
        CurrentSlot = Mathf.Clamp(slot, 0, SlotCount - 1);
        DayRecap.BeginDay();
        return Save();
    }

    /// <summary>Called at the end of every day. Returns true if the game was saved.</summary>
    public static bool AutoSave()
    {
        return Save();
    }

    /// <summary>Saves the game into the current slot. Does nothing (and returns false) when there is no slot.</summary>
    public static bool Save()
    {
        if (CurrentSlot < 0) return false;
        return Write(CurrentSlot, Capture());
    }

    /// <summary>
    /// Loads a slot: puts all the saved numbers back, then loads the farm scene, where the player wakes up in bed.
    /// Returns false (and changes nothing) if the slot can't be read.
    /// </summary>
    public static bool Load(int slot)
    {
        SaveFile file = Read(slot);
        if (file == null) return false;

        IsLoading = true;           // from here on, the scene we are leaving must not overwrite what we just loaded
        Apply(file);
        CurrentSlot = slot;
        wakeAtBedAfterLoad = true;

        MenuUI.CloseAll();
        SceneExit.NextSpawnId = "";
        string scene = Application.CanStreamedLevelBeLoaded(HomeScene) ? HomeScene : SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(scene);
        return true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsLoading) return;
        IsLoading = false;

        if (wakeAtBedAfterLoad)
        {
            wakeAtBedAfterLoad = false;
            new GameObject("WakeUpAtBed").AddComponent<WakeUpAtBed>();      // moves the player to the bed, then removes itself
        }
    }

    // ------------------------------------------------------------------
    // Writing the game down
    // ------------------------------------------------------------------

    /// <summary>Gathers everything that is saved into one SaveFile.</summary>
    private static SaveFile Capture()
    {
        var file = new SaveFile();

        // Things that live in the scene are written into the session memory first, so they are up to date.
        TimeManager time = TimeManager.Instance;
        if (time != null) Clock = time.CaptureClock();
        foreach (FarmManager farm in UnityEngine.Object.FindObjectsByType<FarmManager>()) RememberSoil(farm);
        if (CropManager.Instance != null) RememberCrops(CropManager.Instance);

        file.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        file.dateText = time != null ? time.GetDateString() : "";
        file.year = Clock.valid ? Clock.year : 1;
        file.gold = GameState.Gold;

        file.gameState = GameState.SaveData();
        file.house = HouseManager.SaveRemembered();
        file.clock = Clock;

        foreach (ItemStack s in Inventory.AllSlots) file.backpack.Add(ToSaved(s));
        file.selectedSlot = Inventory.SavedSelectedIndex;
        foreach (ItemStack s in ShippingBin.Pending) file.mailbox.Add(ToSaved(s));

        for (int i = 0; i < PlayerEquipment.SlotCount; i++)
        {
            ItemData worn = PlayerEquipment.Get(i);
            file.equipment.Add(worn != null ? worn.name : "");
        }

        file.farms.AddRange(farms.Values);
        file.soilCare = SoilCare.CaptureForSave();

        foreach (QuestLog.Entry q in QuestLog.Entries)
            file.quests.Add(new SavedQuest { id = q.id, title = q.title, giver = q.giver, description = q.description, done = q.done });
        foreach (Friendship.Person p in Friendship.Known)
            file.people.Add(new SavedPerson { id = p.id, name = p.name });

        file.hasDebris = true;
        file.debrisDay = FarmDebrisSpawner.CaptureForSave(file.debris);

        return file;
    }

    private static SavedStack ToSaved(ItemStack s)
    {
        if (s == null || s.IsEmpty) return new SavedStack();
        return new SavedStack { item = s.item.name, count = s.count, pollution = s.pollution, quality = s.quality };
    }

    // ------------------------------------------------------------------
    // Putting a saved game back
    // ------------------------------------------------------------------

    /// <summary>
    /// Copies a SaveFile into the game. This only fills in the remembered numbers; the scene is loaded
    /// afterwards, and its TimeManager, FarmManager, HouseManager and so on read them as they wake up.
    /// </summary>
    private static void Apply(SaveFile file)
    {
        GameState.LoadData(file.gameState);
        PlayerSkills.SyncAfterLoad();           // or the loaded counters would be counted as freshly earned XP
        HouseManager.LoadRemembered(file.house);

        Clock = file.clock != null ? file.clock : new SavedClock();

        Inventory.LoadFromSave(ToStacks(file.backpack, Inventory.SlotCount), file.selectedSlot);

        ItemStack[] mail = ToStacks(file.mailbox, ShippingBin.SlotCount);
        for (int i = 0; i < ShippingBin.SlotCount; i++)
        {
            ItemStack to = ShippingBin.Pending[i];
            to.item = mail[i].item; to.count = mail[i].count; to.pollution = mail[i].pollution; to.quality = mail[i].quality;
        }

        var worn = new ItemData[PlayerEquipment.SlotCount];
        for (int i = 0; i < worn.Length; i++)
            worn[i] = file.equipment != null && i < file.equipment.Count ? SaveCatalog.Current.FindItem(file.equipment[i]) : null;
        PlayerEquipment.LoadFromSave(worn);

        farms.Clear();
        if (file.farms != null)
            foreach (SavedFarm f in file.farms)
                if (f != null && !string.IsNullOrEmpty(f.scene)) farms[f.scene] = f;

        SoilCare.LoadFromSave(file.soilCare);

        QuestLog.ClearForLoad();
        if (file.quests != null)
            foreach (SavedQuest q in file.quests) QuestLog.Restore(q.id, q.title, q.giver, q.description, q.done);

        Friendship.ClearForLoad();
        if (file.people != null)
            foreach (SavedPerson p in file.people) Friendship.Restore(p.id, p.name);

        // weeds, stones, litter and fallen trees (an old save without them gets a fresh scatter)
        FarmDebrisSpawner.LoadFromSave(file.hasDebris ? file.debris : null, file.hasDebris ? file.debrisDay : -1);

        DayRecap.BeginDay();                    // today's XP is counted from the loaded numbers
    }

    /// <summary>Turns saved stacks back into real ones. Always returns exactly 'size' stacks; unknown items become empty slots.</summary>
    private static ItemStack[] ToStacks(List<SavedStack> saved, int size)
    {
        var stacks = new ItemStack[size];
        for (int i = 0; i < size; i++)
        {
            stacks[i] = new ItemStack();
            SavedStack s = saved != null && i < saved.Count ? saved[i] : null;
            if (s == null || s.count <= 0 || string.IsNullOrEmpty(s.item)) continue;

            ItemData item = SaveCatalog.Current.FindItem(s.item);
            if (item == null) continue;
            stacks[i].item = item;
            stacks[i].count = s.count;
            stacks[i].pollution = s.pollution;
            stacks[i].quality = s.quality;
        }
        return stacks;
    }

    // ------------------------------------------------------------------
    // Session memory: the clock, and each scene's soil and crops
    // ------------------------------------------------------------------

    /// <summary>Today's day number, even while no TimeManager is alive (in the middle of a scene change).</summary>
    private static int Today
    {
        get
        {
            if (TimeManager.Instance != null) return TimeManager.Instance.TotalDays;
            return Clock.valid ? Clock.totalDays : 1;
        }
    }

    /// <summary>Called by a TimeManager as its scene is left.</summary>
    public static void RememberClock(TimeManager time)
    {
        if (IsLoading || time == null) return;
        Clock = time.CaptureClock();
    }

    private static SavedFarm FarmOf(string scene)
    {
        SavedFarm farm;
        if (!farms.TryGetValue(scene, out farm))
        {
            farm = new SavedFarm { scene = scene };
            farms[scene] = farm;
        }
        return farm;
    }

    /// <summary>Called by a FarmManager as its scene is left (and just before saving).</summary>
    public static void RememberSoil(FarmManager farm)
    {
        if (IsLoading || farm == null) return;
        SavedFarm saved = FarmOf(farm.gameObject.scene.name);
        saved.soil = farm.CaptureSoil();
        saved.totalDays = Today;
    }

    /// <summary>Called by a CropManager as its scene is left (and just before saving).</summary>
    public static void RememberCrops(CropManager crops)
    {
        if (IsLoading || crops == null) return;
        SavedFarm saved = FarmOf(crops.gameObject.scene.name);
        saved.crops = crops.CaptureCrops();
        saved.totalDays = Today;
    }

    /// <summary>
    /// Called by a FarmManager when its scene starts: puts the remembered soil and crops back.
    /// If nights went by while you were somewhere else, the farm catches up on them (crops grow, sprinklers run,
    /// the mailbox is paid out), one night at a time.
    /// </summary>
    public static void RestoreFarm(FarmManager farm)
    {
        if (farm == null) return;
        SavedFarm saved;
        if (!farms.TryGetValue(farm.gameObject.scene.name, out saved)) return;      // first visit: keep the scene as it was built

        farm.RestoreSoil(saved.soil);
        CropManager crops = CropManager.Instance;
        if (crops != null && crops.gameObject.scene == farm.gameObject.scene) crops.RestoreCrops(saved.crops);

        int missed = Today - saved.totalDays;
        if (missed > 0)
        {
            missed = Mathf.Min(missed, MaxCatchUpDays);
            for (int i = 0; i < missed; i++) farm.AdvanceDay();
        }
        saved.totalDays = Today;
    }
}
