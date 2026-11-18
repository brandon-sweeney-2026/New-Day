using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Test helpers for the Healing Forest (all under Farm > Test and Debug, Play mode only):
///   Healing Forest - Heal Next Zone            heals the lowest sick zone, with the green wave (stand in it to watch)
///   Healing Forest - Heal All Zones            heals every zone at once
///   Healing Forest - Give Next Stump's Items   puts what the next heart stump still wants into your backpack
///   Healing Forest - Go To Next Heart Stump    moves you next to the next heart stump
///   Healing Forest - Reset All Zones           makes every zone sick again, forgets what was given and resets the fairies (works from any scene)
/// The zone menus need the Forest scene to be playing. Must live in a folder named "Editor".
/// </summary>
public static class ForestHealTestMenu
{
    [MenuItem("Farm/Test and Debug/Healing Forest - Heal Next Zone", false, 335)]
    public static void HealNext()
    {
        if (!InForest()) return;
        ForestZone z = ForestZone.NextToHeal();
        if (z == null) { Debug.Log("Healing Forest: every zone is already healed."); return; }
        z.Heal();
        Debug.Log("Healing Forest: healing zone " + z.order + ", " + z.displayName + ".");
    }

    [MenuItem("Farm/Test and Debug/Healing Forest - Heal All Zones", false, 336)]
    public static void HealAll()
    {
        if (!InForest()) return;
        int n = 0;
        foreach (ForestZone z in new List<ForestZone>(ForestZone.All)) if (z != null && !z.IsHealed) { z.Heal(); n++; }
        Debug.Log("Healing Forest: healing " + n + " zone(s).");
    }

    [MenuItem("Farm/Test and Debug/Healing Forest - Give Next Stump's Items", false, 337)]
    public static void GiveItems()
    {
        if (!InForest()) return;
        ForestZone z = ForestZone.NextToHeal();
        if (z == null) { Debug.Log("Healing Forest: every zone is already healed, no stump wants anything."); return; }
        Inventory inv = Inventory.Instance;
        if (inv == null) { Debug.LogWarning("Healing Forest: no backpack (Inventory) in this scene."); return; }
        var given = new List<string>();
        ForestRequirement[] reqs = z.requirements ?? new ForestRequirement[0];
        for (int i = 0; i < reqs.Length; i++)
        {
            ForestRequirement r = reqs[i];
            if (r == null || r.item == null) continue;
            int still = r.count - ForestProgress.Given(z.OfferingId, i) - ForestItemMatch.Count(inv, r.item);
            if (still <= 0) continue;
            inv.Add(r.item, still);
            given.Add(r.item.itemName + " x" + still);
        }
        Debug.Log("Healing Forest: " + (given.Count > 0 ? "gave " + string.Join(", ", given) : "you already carry everything") + " for " + z.displayName + "'s heart stump.");
    }

    [MenuItem("Farm/Test and Debug/Healing Forest - Go To Next Heart Stump", false, 338)]
    public static void GoToStump()
    {
        if (!InForest()) return;
        ForestZone z = ForestZone.NextToHeal();
        if (z == null) { Debug.Log("Healing Forest: every zone is healed."); return; }
        ForestStump stump = null;
        foreach (ForestStump s in Object.FindObjectsByType<ForestStump>()) if (s.zone == z) { stump = s; break; }
        var player = Object.FindAnyObjectByType<PlayerController>();
        if (stump == null || player == null) { Debug.LogWarning("Healing Forest: couldn't find the stump or the player."); return; }
        player.transform.position = stump.transform.position + new Vector3(0f, -1.2f, 0f);
        player.FaceDirection(Vector2.up);
        var cam = Object.FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.SnapToTarget();
        Debug.Log("Healing Forest: you're at the heart stump of " + z.displayName + ".");
    }

    [MenuItem("Farm/Test and Debug/Healing Forest - Reset All Zones", false, 339)]
    public static void ResetAll()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Play mode needed", "Progress lives in the running game. Press Play first.", "OK"); return; }
        // zones in the open scene go back to sick at once (tiles, trees, blockers, animals)
        foreach (ForestZone z in new List<ForestZone>(ForestZone.All)) if (z != null) z.ResetToSick();
        // ...and in any other scene, the saved progress is simply forgotten
        foreach (string id in ForestSceneBuilder.ZoneIds())
        {
            GameState.SetFlag(ForestZone.FlagFor(id), false);
            ForestProgress.ResetGiven("zone_" + id, 4);
        }
        GameState.SetCounter(ForestZone.HealedCounter, 0);
        GameState.SetFlag("forest_grove_reached", false);
        // forget the old-maze-save upgrade too, or the zones would heal again on the next visit
        GameState.SetFlag(ForestZone.OldSaveCheckedFlag, false);
        GameState.SetFlag(ForestZone.OldSaveGroveFlag, false);
        // the fairies forget you too (their quests start over)
        var fairy = Object.FindAnyObjectByType<FairyNpc>();
        if (fairy != null && fairy.quests != null)
            foreach (FairyQuest q in fairy.quests) GameState.SetCounter("fairy_q_" + q.id, 0);
        GameState.SetFlag("fairy_met", false);
        GameState.SetFlag("sick_fairy_met", false);
        GameState.SetFlag("sick_fairy_promised", false);
        Debug.Log("Healing Forest: every zone is sick again, the stumps have forgotten what they were given, the fairies have forgotten you, and the grove is unvisited.");
    }

    private static bool InForest()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("Play mode needed", "Press Play in the Forest scene first.", "OK"); return false; }
        if (ForestZone.All.Count == 0) { EditorUtility.DisplayDialog("Not in the Healing Forest", "This scene has no forest zones. Walk to the forest (or press Play in the Forest scene) first.", "OK"); return false; }
        return true;
    }
}
