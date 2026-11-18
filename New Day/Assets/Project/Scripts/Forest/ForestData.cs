using UnityEngine;

// Small forest types that share this file (see CLAUDE.md, "Conventions"). The forest's settings asset ForestProgression
// lives in its own file, because Unity can only save an asset whose script file matches its class name.

/// <summary>"Bring me 5 Carrots." Used by forest stumps (what they want) and by the Fairy's quests.</summary>
[System.Serializable]
public class ForestRequirement
{
    public ItemData item;
    [Min(1)] public int count = 1;
}

/// <summary>Remembers what has been given to each heart stump (stumps keep your offerings between visits). Saved with the game.</summary>
public static class ForestProgress
{
    /// <summary>How many of a requirement the player has already put on a stump. 'offeringId' is IForestOffering.OfferingId.</summary>
    public static int Given(string offeringId, int index) { return GameState.GetCounter("forest_give_" + offeringId + "_" + index); }
    public static void AddGiven(string offeringId, int index, int n) { GameState.AddCounter("forest_give_" + offeringId + "_" + index, n); }
    /// <summary>Forgets everything given to the first 'count' requirements (used when a zone is reset by the test menu).</summary>
    public static void ResetGiven(string offeringId, int count) { for (int i = 0; i < count; i++) GameState.SetCounter("forest_give_" + offeringId + "_" + i, 0); }
}
