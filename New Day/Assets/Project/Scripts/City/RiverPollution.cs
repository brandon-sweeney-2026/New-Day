using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// How dirty the river is, and what that does downstream. The river runs past the city, then north of the suburbs, the farm and the forest.
///
/// It gets dirtier when:  the player throws waste in it (counter river_dumped), and a little every day because of the Sterling tower.
/// It gets cleaner when:  the player takes waste to the treatment plant (trash_treated) or pays for river improvements at City Hall (river_cleanups).
/// The numbers are on the Soil Care Settings asset, under "River pollution".
///
/// RiverPollution.Level is 0 (clean) to 5 (filthy). When the Forest or the Farm scene loads with a dirty river, this script
/// paints pollution onto that scene's PollutionMap: patches all over the forest, and the rows of the farm nearest the river.
/// Nothing to set up: it creates itself.
/// </summary>
public class RiverPollution : MonoBehaviour
{
    public const string DumpedKey = "river_dumped";
    public const string TreatedKey = "trash_treated";
    public const string CleanupsKey = "river_cleanups";

    /// <summary>The "dirt points" in the river right now (never below 0).</summary>
    public static float Points
    {
        get
        {
            SoilCareSettings s = SoilCareSettings.Current;
            int days = TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 1;
            float p = GameState.GetCounter(DumpedKey) * s.pointsPerItemDumped
                    + days * s.pointsPerDayFromTower
                    - GameState.GetCounter(TreatedKey) * s.pointsCleanedPerItemTreated
                    - GameState.GetCounter(CleanupsKey) * s.pointsCleanedPerRiverImprovement;
            return Mathf.Max(0f, p);
        }
    }

    /// <summary>0 = clean ... 5 = filthy.</summary>
    public static int Level
    {
        get { return Mathf.Clamp(Mathf.FloorToInt(Points / Mathf.Max(1, SoilCareSettings.Current.pointsPerLevel)), 0, PollutionSettings.MaxLevel); }
    }

    public static string Describe(int level)
    {
        switch (level)
        {
            case 0: return "The river runs clear.";
            case 1: return "There is a faint film on the river.";
            case 2: return "The river smells of chemicals.";
            case 3: return "The river is cloudy and nothing swims near the surface.";
            case 4: return "The river is a sick purple-green.";
            default: return "The river is poison.";
        }
    }

    // ------------------------------------------------------------------ creating itself, once per scene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() { Make(SceneManager.GetActiveScene().name); }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Make(scene.name); }

    private static void Make(string sceneName)
    {
        if (sceneName != "Forest" && sceneName != "Farm") return;      // the only places the river's dirt lands
        if (FindAnyObjectByType<RiverPollution>() != null) return;
        new GameObject("RiverPollution").AddComponent<RiverPollution>().scene = sceneName;
    }

    private string scene;

    private void Start()
    {
        int level = Level;
        PollutionMap map = PollutionMap.Instance;
        if (level <= 0 || map == null) return;

        // the size of this scene's ground decides where the dirt can land
        Tilemap ground = null;
        foreach (Tilemap t in FindObjectsByType<Tilemap>()) if (t.name == "Ground") { ground = t; break; }
        if (ground == null) return;
        ground.CompressBounds();
        BoundsInt b = ground.cellBounds;
        if (b.size.x <= 0 || b.size.y <= 0) return;

        SoilCareSettings s = SoilCareSettings.Current;
        // the same patches all day, different ones tomorrow
        var rng = new System.Random((TimeManager.Instance != null ? TimeManager.Instance.TotalDays : 1) * 31 + scene.Length);

        if (scene == "Farm")
        {
            // the river is north of the farm: the top rows of the map soak it up
            for (int row = 0; row < s.farmRowsReached; row++)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    var cell = new Vector3Int(x, b.yMax - 1 - row, 0);
                    int here = Mathf.Max(1, level - row);
                    if (ground.GetTile(cell) != null && map.GetLevel(cell) < here) map.SetLevel(cell, here);
                }
        }
        else
        {
            int patches = level * s.forestPatchesPerLevel;
            for (int i = 0; i < patches; i++)
            {
                var cell = new Vector3Int(rng.Next(b.xMin, b.xMax), rng.Next(b.yMin, b.yMax), 0);
                if (ground.GetTile(cell) != null) map.PaintCircle(cell, 1 + rng.Next(2), Mathf.Min(level, PollutionSettings.MaxLevel));
            }
        }
        GameEvents.ShowToast(Describe(level) + (scene == "Farm" ? " It is seeping into the north of your farm." : " The forest is suffering for it."));
    }
}
