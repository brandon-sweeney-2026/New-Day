using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs a fishing trip from start to finish, out in the world (no screens until the fight):
///
///   1. CAST    - the player swings the rod and the bobber lands in the water (FishingRig draws this). Bait is put on by itself.
///   2. WAIT    - the bobber floats. Press the action button now (or Esc) to reel in early.
///   3. BITE    - a "ding", the bobber is pulled under and a "!" appears. Press the action button within "Hook Window" seconds!
///                Too slow and it gets away.
///   4. CATCH   - sometimes it is JUNK (an old boot, a can, a trash bag...): it is pulled straight out, no fight.
///                Otherwise it is a fish and the rhythm fight starts (FishingUI). Win it and the fish is yours.
///
/// Put ONE of these in the scene (Farm > Set Up > Fishing does it) and fill the lists. PlayerActions calls
/// FishingManager.TryStart when the player uses a Fishing Rod while facing a FishingWater area.
///
/// BUTTONS in the fight:  Up / Down / Left / Right = W A S D or the arrow keys (or the D-pad).
///                        Action 1 = the key below (Space by default, also gamepad A).   Action 2 = the key below (K by default, also gamepad B).
/// </summary>
public class FishingManager : MonoBehaviour
{
    public static FishingManager Instance { get; private set; }
    public static bool IsFishing { get; private set; }

    [Header("Data (the setup menu fills these in)")]
    public FishingRodData[] rods;
    public BaitData[] baits;

    [Header("Casting")]
    [Tooltip("How far in front of the player the bobber can land (tiles). It lands as far out as the water allows, up to this.")]
    public float castReach = 2.6f;
    [Tooltip("Seconds to wait for a bite (random between the two).")] public Vector2 biteWaitSeconds = new Vector2(2.5f, 6f);
    [Tooltip("About how many times per second the bobber twitches from a nibble while you wait (just for show).")]
    [Range(0f, 2f)] public float nibblesPerSecond = 0.35f;

    [Header("The bite")]
    [Tooltip("Seconds you have to press the action button after the bobber goes under. Miss it and the fish gets away.")]
    [Range(0.2f, 3f)] public float hookWindowSeconds = 1.1f;

    [Header("Junk (pulled out at once, no fight)")]
    [Tooltip("Chance that a bite is junk instead of a fish. 0.15 = 15%.")]
    [Range(0f, 1f)] public float junkChance = 0.15f;
    [Tooltip("Extra junk chance for each level of river pollution (0 clean ... 5 filthy). 0.06 = +6% per level.")]
    [Range(0f, 0.2f)] public float junkChancePerRiverLevel = 0.06f;
    [Tooltip("Junk chance is multiplied by this when you use bait (fish go for the bait). 0.5 = half as much junk.")]
    [Range(0f, 1f)] public float junkWithBaitMultiplier = 0.6f;
    [Tooltip("The junk is never more likely than this.")]
    [Range(0f, 1f)] public float maxJunkChance = 0.75f;
    [Tooltip("Fishing XP for pulling out a piece of junk.")]
    [Min(0)] public int junkXp = 1;
    [Tooltip("What can come up. Leave empty to use the waste items the game already has (Old Boot, Crushed Can, Trash Bag, ...).")]
    public List<FishingJunk> junk = new List<FishingJunk>();

    [Header("Sounds (empty = the built-in sounds)")]
    public AudioClip biteSound;
    public AudioClip landSound;
    public AudioClip catchSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    [Header("Fight buttons")]
    public Key action1Key = Key.Space;
    public Key action2Key = Key.K;

    /// <summary>The junk used when the "Junk" list is empty: item asset names and weights.</summary>
    private static readonly KeyValuePair<string, float>[] DefaultJunk =
    {
        new KeyValuePair<string, float>("OldBoot", 20f),
        new KeyValuePair<string, float>("CrushedCan", 25f),
        new KeyValuePair<string, float>("TrashBag", 15f),
        new KeyValuePair<string, float>("Debris", 15f),
        new KeyValuePair<string, float>("Scrap", 10f),
        new KeyValuePair<string, float>("TarLump", 6f),
        new KeyValuePair<string, float>("ToxicGoo", 6f),
        new KeyValuePair<string, float>("SludgeBarrel", 3f),
    };

    private static BaitData lastBait;        // the bait used last time, tried first next time
    private FishingRig rig;
    private bool tripHoldsLocks;             // true while THIS manager has locked input and paused the clock for a trip

    // The project turns off "domain reload" when pressing Play, so static values survive from the last test.
    // Without this, stopping Play in the middle of a trip left IsFishing stuck on (the rod stayed hidden and casting did nothing).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        IsFishing = false;
        Instance = null;
        lastBait = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindAnyObjectByType<FishingManager>() == null) new GameObject("FishingManager").AddComponent<FishingManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        rig = GetComponent<FishingRig>();
        if (rig == null) rig = gameObject.AddComponent<FishingRig>();
    }

    private void OnDestroy()
    {
        // If the manager goes away in the middle of a trip (for example the scene changes), the trip never
        // reaches its normal end, so give the player back their movement and the clock here.
        ReleaseNow();
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- starting
    /// <summary>Called by PlayerActions when a fishing rod is used.</summary>
    public static void TryStart(PlayerActions player, ItemData rodItem)
    {
        if (IsFishing) return;
        if (Instance == null) AutoCreate();
        Instance.Begin(player, rodItem);
    }

    private void Begin(PlayerActions player, ItemData rodItem)
    {
        // Look for water from the farthest point the rod reaches back toward the player: the bobber lands as far out as it can.
        FishingWater water = null;
        Vector2 landAt = Vector2.zero;
        Vector2 origin = (Vector2)player.transform.position + Vector2.up * 0.3f;
        for (float d = castReach; d >= 0.8f && water == null; d -= 0.2f)
        {
            Vector2 p = origin + player.Facing * d;
            water = FishingWater.At(p);
            if (water != null) landAt = p;
        }

        if (water == null)
        {
            GameEvents.ShowToast(FishingWater.All.Count == 0 ? "There's no fishing water set up in this area." : "Face the water to fish.");
            return;
        }
        StartCoroutine(Run(player, rodItem, water, landAt));
    }

    private FishingRodData FindRod(ItemData rodItem)
    {
        if (rods != null) foreach (FishingRodData r in rods) if (r != null && r.rodItem == rodItem) return r;
        return null;
    }

    // ---------------------------------------------------------------- the whole trip
    private IEnumerator Run(PlayerActions player, ItemData rodItem, FishingWater water, Vector2 landAt)
    {
        IsFishing = true;
        tripHoldsLocks = true;
        GameState.LockInput();
        if (TimeManager.Instance != null) TimeManager.Instance.Pause();
        FishingUI ui = null;

        try
        {
            FishingRodData rod = FindRod(rodItem);
            FishingBonuses bonus = FishingBonuses.Compute(rod);
            Inventory inv = player.Inventory;

            // ---- bait goes on by itself (the one used last time, or the first one in the backpack) ----
            BaitData bait = PickBait(inv);
            if (bait != null)
            {
                inv.Remove(bait.baitItem, 1);
                lastBait = bait;
                GameEvents.ShowToast("Baited with " + bait.baitItem.itemName + " (" + inv.CountOf(bait.baitItem) + " left).");
            }

            // ---- 1. cast ----
            yield return rig.Cast(player, rodItem, landAt);
            FishingSfx.Play(landSound != null ? landSound : FishingSfx.Plop, soundVolume);

            // ---- 2. wait for a bite (press the action button or Esc to reel in early) ----
            float wait = Random.Range(biteWaitSeconds.x, biteWaitSeconds.y) * (1f - Mathf.Clamp(bonus.biteSpeed, 0f, 0.8f));
            yield return null;                                  // the press that cast the line must not also reel it in
            for (float t = 0f; t < wait; t += Time.deltaTime)
            {
                if (player.ActionPressedThisFrame || EscPressed())
                {
                    yield return rig.Reel(null);
                    GameEvents.ShowToast("You reeled in.");
                    yield break;
                }
                if (t < wait - 0.6f && Random.value < nibblesPerSecond * Time.deltaTime) rig.Nibble();
                yield return null;
            }

            // ---- what bit? junk or a fish ----
            float hour = TimeManager.Instance != null ? TimeManager.Instance.HoursDecimal % 24f : 12f;
            FishData fish = water.PickFish(hour, bait, bonus.luck, bonus.baitExtra);
            ItemData junkItem = Random.value < JunkChance(bait != null) || fish == null ? PickJunk() : null;
            if (fish == null && junkItem == null)
            {
                yield return rig.Reel(null);
                GameEvents.ShowToast("Nothing is biting here right now.");
                yield break;
            }

            // ---- 3. the bite: ding, the bobber goes under, press now! ----
            rig.Bite();
            FishingSfx.Play(biteSound != null ? biteSound : FishingSfx.Ding, soundVolume);
            bool hooked = false;
            for (float t = 0f; t < hookWindowSeconds; t += Time.deltaTime)
            {
                if (player.ActionPressedThisFrame) { hooked = true; break; }
                yield return null;
            }
            if (!hooked)
            {
                rig.Resurface();
                float pause = 0f;
                while (pause < 0.5f) { pause += Time.deltaTime; yield return null; }
                yield return rig.Reel(null);
                GameEvents.ShowToast(junkItem != null ? "Whatever it was, it slipped off the hook." : "Too slow... it got away.");
                yield break;
            }

            // ---- 4a. junk: straight out of the water, no fight ----
            if (junkItem != null)
            {
                FishingSfx.Play(catchSound != null ? catchSound : FishingSfx.Splash, soundVolume);
                yield return rig.Reel(junkItem.icon);
                int left = inv != null ? inv.Add(junkItem, 1) : 1;
                if (left > 0) ItemPickup.Spawn(junkItem, left, player.transform.position + Vector3.up * 0.3f);
                GameState.AddCounter("junk_fished");
                if (junkXp > 0) FishingTalents.AddXp(junkXp);
                GameEvents.ShowToast("You fished up " + AOrAn(junkItem.itemName) + ". Yuck." + (left > 0 ? " (Your backpack is full: it's on the ground.)" : ""));
                yield break;
            }

            // ---- 4b. a fish: the fight ----
            rig.SetTight(true);
            ui = FishingUI.Get();
            ui.Begin();
            bool caught = false;
            yield return ui.Minigame(fish, bonus, c => caught = c);
            ui.End();

            if (!caught)
            {
                GameState.AddCounter("fish_escaped");
                rig.Resurface();
                yield return rig.Reel(null);
                GameEvents.ShowToast("The " + fish.fishName + " got away...");
                yield break;
            }

            bool first = !GameState.HasFlag("fish_" + fish.name);
            GameState.SetFlag("fish_" + fish.name);
            GameState.AddCounter("fish_caught");
            GameState.AddCounter("caught_" + fish.name);
            int leftover = fish.item != null && inv != null ? inv.Add(fish.item, 1) : 0;
            if (leftover > 0 && fish.item != null) ItemPickup.Spawn(fish.item, leftover, player.transform.position + Vector3.up * 0.3f);
            FishingTalents.AddXp(fish.xp);

            FishingSfx.Play(catchSound != null ? catchSound : FishingSfx.Splash, soundVolume);
            yield return rig.Reel(fish.item != null ? fish.item.icon : null);

            ui.Begin();
            yield return ui.Result(fish, true, first, fish.xp);
        }
        finally
        {
            if (ui != null) ui.End();
            if (rig != null) rig.Hide();
            // Normally wait a moment (the Space that closed the screen must not cast again).
            // If this object is being switched off or destroyed, a coroutine can't run, so release at once.
            if (isActiveAndEnabled) StartCoroutine(ReleaseSoon());
            else ReleaseNow();
        }
    }

    private IEnumerator ReleaseSoon()
    {
        float t = 0f;
        while (t < 0.35f) { t += Time.unscaledDeltaTime; yield return null; }
        ReleaseNow();
    }

    /// <summary>Ends the trip's hold on the game: unlocks input, restarts the clock and clears IsFishing. Safe to call twice.</summary>
    private void ReleaseNow()
    {
        if (!tripHoldsLocks) return;
        tripHoldsLocks = false;
        GameState.UnlockInput();
        if (TimeManager.Instance != null) TimeManager.Instance.Resume();
        IsFishing = false;
    }

    // ---------------------------------------------------------------- bait and junk
    /// <summary>The bait used last time if there is still some, otherwise the first bait (in the list order) that is in the backpack.</summary>
    private BaitData PickBait(Inventory inv)
    {
        if (inv == null || baits == null) return null;
        if (lastBait != null && lastBait.baitItem != null && inv.CountOf(lastBait.baitItem) > 0) return lastBait;
        foreach (BaitData b in baits)
            if (b != null && b.baitItem != null && inv.CountOf(b.baitItem) > 0) return b;
        return null;
    }

    /// <summary>The chance (0 to 1) that a bite is junk right now.</summary>
    private float JunkChance(bool usingBait)
    {
        float c = junkChance + RiverPollution.Level * junkChancePerRiverLevel;
        if (usingBait) c *= junkWithBaitMultiplier;
        return Mathf.Clamp(c, 0f, maxJunkChance);
    }

    /// <summary>Picks a piece of junk by weight (from the Junk list, or the built-in list if that is empty). Null if there is none.</summary>
    private ItemData PickJunk()
    {
        var options = new List<FishingJunk>();
        if (junk != null) foreach (FishingJunk j in junk) if (j != null && j.item != null && j.weight > 0f) options.Add(j);
        if (options.Count == 0)
            foreach (var d in DefaultJunk)
            {
                ItemData item = SaveCatalog.Current.FindItem(d.Key);
                if (item != null) options.Add(new FishingJunk { item = item, weight = d.Value });
            }
        if (options.Count == 0) return null;

        float total = 0f;
        foreach (FishingJunk j in options) total += j.weight;
        float roll = Random.value * total;
        foreach (FishingJunk j in options) { roll -= j.weight; if (roll <= 0f) return j.item; }
        return options[options.Count - 1].item;
    }

    private static string AOrAn(string name)
    {
        if (string.IsNullOrEmpty(name)) return "something";
        return ("AEIOUaeiou".IndexOf(name[0]) >= 0 ? "an " : "a ") + name;
    }

    private static bool EscPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
        return Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;
    }

    // ---------------------------------------------------------------- buttons (lane 0 Up, 1 Down, 2 Left, 3 Right, 4 Action 1, 5 Action 2)
    public bool LanePressed(int lane)
    {
        Keyboard kb = Keyboard.current;
        Gamepad gp = Gamepad.current;
        switch (lane)
        {
            case 0: return (kb != null && (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)) || (gp != null && gp.dpad.up.wasPressedThisFrame);
            case 1: return (kb != null && (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)) || (gp != null && gp.dpad.down.wasPressedThisFrame);
            case 2: return (kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)) || (gp != null && gp.dpad.left.wasPressedThisFrame);
            case 3: return (kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)) || (gp != null && gp.dpad.right.wasPressedThisFrame);
            case 4: return (kb != null && kb[action1Key].wasPressedThisFrame) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            case 5: return (kb != null && kb[action2Key].wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);
        }
        return false;
    }

    public string LaneKeyLabel(int lane)
    {
        switch (lane)
        {
            case 0: return "W";
            case 1: return "S";
            case 2: return "A";
            case 3: return "D";
            case 4: return action1Key.ToString();
            default: return action2Key.ToString();
        }
    }
}
