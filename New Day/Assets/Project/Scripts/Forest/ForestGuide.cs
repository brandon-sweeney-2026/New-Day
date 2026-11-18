using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the sick fairy says when you ask her for help in the Healing Forest. The Forest scene builder puts one object called
/// "ForestGuide" in the scene. It needs no settings: it finds the next sick zone (the lowest Order), its heart stump,
/// and what that stump still wants, and gives directions from where the player stands.
/// </summary>
public class ForestGuide : MonoBehaviour
{
    private static readonly string[] Names = { "north (up)", "east (right)", "south (down)", "west (left)" };

    /// <summary>The pages of dialogue for "Ask for help", based on where the player stands and which zones are still sick.</summary>
    public string[] Describe(Vector2 playerPos)
    {
        var pages = new List<string>();

        ForestGrove grove = FindAnyObjectByType<ForestGrove>();
        if (grove != null && grove.Contains(playerPos))
        {
            pages.Add("You're already in the grove! My sister should be near the fairy ring in the middle. Go and talk to her.");
            pages.Add("When you want to come back to me, the fairy door here will take you straight back to the start of the trail.");
            return pages.ToArray();
        }

        ForestZone next = ForestZone.NextToHeal();
        if (next == null)
        {
            if (ForestZone.All.Count == 0)
            {
                pages.Add("I can feel the paths, but I can't tell which way is right. Keep going deeper.");
                return pages.ToArray();
            }
            pages.Add("Every part of the forest is breathing again... I can feel it in my wings. Thank you.");
            pages.Add(GameState.HasFlag("forest_grove_reached")
                ? "My sister is in the grove at the end of the trail. Go and see her - she'll want to thank you too."
                : "The way to the grove is open now. Follow the trail to the very end, and look for the fireflies.");
            return pages.ToArray();
        }

        // where the next heart stump is (or the middle of its zone, if it has none)
        ForestStump stump = null;
        foreach (ForestStump s in FindObjectsByType<ForestStump>()) if (s != null && s.zone == next) { stump = s; break; }
        Vector2 goal = stump != null ? (Vector2)stump.transform.position : (Vector2)next.transform.position;
        ForestZone here = ForestZone.At(playerPos);
        int tiles = Mathf.RoundToInt(Vector2.Distance(playerPos, goal));

        if (here == next)
            pages.Add("You're in " + next.displayName + " now. Its heart stump is " + (tiles <= 2 ? "right here beside you." : "to the " + Name(Compass(goal - playerPos)) + ", about " + tiles + " steps away."));
        else
            pages.Add("The sickest part of the forest that you can reach is " + next.displayName + ". It lies to the " + Name(Compass(goal - playerPos)) + ", about " + tiles + " steps from here. Follow the trail.");
        pages.Add("Its heart stump wants:\n" + RequestText(next));
        pages.Add("Give it everything and the forest there will heal - and whatever blocks the trail beyond it will clear.");
        return pages.ToArray();
    }

    /// <summary>Lines like "Carrot x5 (2 given, you carry 4)" for what a heart stump wants.</summary>
    public static string RequestText(IForestOffering target)
    {
        if (target == null) return "(nothing I can tell)";
        ForestRequirement[] reqs = target.Requirements;
        const string nothing = "Nothing at all. Just touch it.";
        if (reqs == null || reqs.Length == 0) return nothing;
        Inventory inv = Inventory.Instance;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < reqs.Length; i++)
        {
            ForestRequirement r = reqs[i];
            if (r == null || r.item == null) continue;
            int given = ForestProgress.Given(target.OfferingId, i);
            int have = inv != null ? ForestItemMatch.Count(inv, r.item) : 0;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(r.item.itemName).Append(" x").Append(r.count);
            if (given >= r.count) sb.Append("  (done!)");
            else sb.Append("  (").Append(given).Append(" given, you carry ").Append(have).Append(")");
        }
        return sb.Length > 0 ? sb.ToString() : nothing;
    }

    /// <summary>0 = up, 1 = right, 2 = down, 3 = left: whichever way the arrow mostly points.</summary>
    private static int Compass(Vector2 v)
    {
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x > 0f ? 1 : 3;
        return v.y > 0f ? 0 : 2;
    }

    private static string Name(int dir) { return Names[Mathf.Clamp(dir, 0, 3)]; }
}
