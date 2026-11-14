using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs a fishing trip from start to finish:  cast -> pick bait -> wait for a bite -> rhythm fight -> catch (or escape).
///
/// Put ONE of these in the scene (Farm > Fishing > Set Up Fishing does it) and fill the two lists. PlayerActions calls
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
    [Tooltip("The player looks this far in front of them for a water area.")] public float castReach = 2.6f;
    [Tooltip("Seconds to wait for a bite (random between the two).")] public Vector2 biteWaitSeconds = new Vector2(1.5f, 4f);

    [Header("Fight buttons")]
    public Key action1Key = Key.Space;
    public Key action2Key = Key.K;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindAnyObjectByType<FishingManager>() == null) new GameObject("FishingManager").AddComponent<FishingManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

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
        FishingWater water = null;
        Vector2 origin = (Vector2)player.transform.position + Vector2.up * 0.3f;
        for (float d = 0.8f; d <= castReach && water == null; d += 0.8f)
            water = FishingWater.At(origin + player.Facing * d);

        if (water == null)
        {
            GameEvents.ShowToast(FishingWater.All.Count == 0 ? "There's no fishing water set up in this area." : "Face the water to fish.");
            return;
        }
        StartCoroutine(Run(player, rodItem, water));
    }

    private FishingRodData FindRod(ItemData rodItem)
    {
        if (rods != null) foreach (FishingRodData r in rods) if (r != null && r.rodItem == rodItem) return r;
        return null;
    }

    // ---------------------------------------------------------------- the whole trip
    private IEnumerator Run(PlayerActions player, ItemData rodItem, FishingWater water)
    {
        IsFishing = true;
        GameState.LockInput();
        if (TimeManager.Instance != null) TimeManager.Instance.Pause();
        FishingUI ui = FishingUI.Get();
        ui.Begin();

        try
        {
            FishingRodData rod = FindRod(rodItem);
            FishingBonuses bonus = FishingBonuses.Compute(rod);
            Inventory inv = player.Inventory;

            // ---- bait ----
            var owned = new List<BaitData>();
            if (baits != null && inv != null)
                foreach (BaitData b in baits)
                    if (b != null && b.baitItem != null && inv.CountOf(b.baitItem) > 0) owned.Add(b);

            BaitData bait = null;
            if (owned.Count > 0)
            {
                int choice = -1;
                yield return ui.ChooseBait(owned, inv, c => choice = c);
                if (choice < 0) yield break;                       // cancelled: nothing used up
                if (choice > 0) bait = owned[choice - 1];
            }
            if (bait != null) inv.Remove(bait.baitItem, 1);

            // ---- wait for a bite ----
            bool cancelled = false;
            float wait = Random.Range(biteWaitSeconds.x, biteWaitSeconds.y) * (1f - Mathf.Clamp(bonus.biteSpeed, 0f, 0.8f));
            yield return ui.WaitForBite(water.areaName, wait, bait != null ? bait.baitItem.itemName : null, c => cancelled = c);
            if (cancelled) { GameEvents.ShowToast("You reeled in."); yield break; }

            float hour = TimeManager.Instance != null ? TimeManager.Instance.HoursDecimal % 24f : 12f;
            FishData fish = water.PickFish(hour, bait, bonus.luck, bonus.baitExtra);
            if (fish == null) { GameEvents.ShowToast("Nothing is biting here right now."); yield break; }

            // ---- the fight ----
            bool caught = false;
            yield return ui.Minigame(fish, bonus, c => caught = c);

            // ---- result ----
            bool first = false;
            if (caught)
            {
                first = !GameState.HasFlag("fish_" + fish.name);
                GameState.SetFlag("fish_" + fish.name);
                GameState.AddCounter("fish_caught");
                GameState.AddCounter("caught_" + fish.name);
                int leftover = fish.item != null && inv != null ? inv.Add(fish.item, 1) : 0;
                if (leftover > 0 && fish.item != null) ItemPickup.Spawn(fish.item, leftover, player.transform.position + Vector3.up * 0.3f);
                FishingTalents.AddXp(fish.xp);
            }
            else GameState.AddCounter("fish_escaped");

            yield return ui.Result(fish, caught, first, caught ? fish.xp : 0);
        }
        finally
        {
            ui.End();
            StartCoroutine(ReleaseSoon());      // not instantly: the Space that closed the screen must not cast again
        }
    }

    private IEnumerator ReleaseSoon()
    {
        float t = 0f;
        while (t < 0.35f) { t += Time.unscaledDeltaTime; yield return null; }
        GameState.UnlockInput();
        if (TimeManager.Instance != null) TimeManager.Instance.Resume();
        IsFishing = false;
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
