using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// A command-line check that the Healing Forest really heals (no menu item; run it with Unity closed):
///   Unity.exe -batchmode -projectPath "..." -executeMethod ForestHealPlayCheck.RunBatch -logFile Logs/forest_check.log
/// (no -quit and no -nographics: it presses Play, waits, and quits by itself).
/// It opens the Forest scene, presses Play (title screen skipped), makes every zone sick, waits for the shadow animals,
/// heals the Trailhead, waits for the wave, and writes FORESTCHECK| lines to the log: healthy tiles, trees that changed,
/// whether the bramble opened, and how many shadow / friendly animals are there. It also saves pictures of the Trailhead
/// before and after to Logs/forest_heal_before.png and Logs/forest_heal_after.png.
/// Must live in a folder named "Editor".
/// </summary>
[InitializeOnLoad]
public static class ForestHealPlayCheck
{
    private const string StepKey = "ForestHealCheck_Step";
    private const string ScenePath = "Assets/Project/Scenes/Forest.unity";
    private static double waitUntil;

    static ForestHealPlayCheck()
    {
        // (survives a script reload: the step is kept in SessionState)
        if (SessionState.GetInt(StepKey, 0) > 0) { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
    }

    public static void RunBatch()
    {
        SessionState.SetInt("ForestHealCheck_OldSkip", PlayerPrefs.GetInt(TitleScreen.SkipInEditorKey, 0));
        PlayerPrefs.SetInt(TitleScreen.SkipInEditorKey, 1);                 // no title screen during the check
        // -checkScene <path> plays another scene instead (to compare: errors that also happen there are not the forest's)
        string scene = ScenePath;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-checkScene") scene = args[i + 1];
        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        // Play is pressed only once Unity has completely finished compiling and importing (step 5). Pressing it too early
        // lets a leftover script reload happen DURING play, which empties every script's unsaved lists (the fog list,
        // the backpack, the music players) and gives false errors.
        AssetDatabase.Refresh();
        SessionState.SetInt(StepKey, 5);
        waitUntil = EditorApplication.timeSinceStartup + 5;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        int step = SessionState.GetInt(StepKey, 0);
        if (step == 0) { EditorApplication.update -= Tick; return; }
        if (step == 4) { Finish(0); return; }
        if (step == 5)
        {
            // waiting (in real time) for Unity to settle before pressing Play
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { waitUntil = EditorApplication.timeSinceStartup + 3; return; }
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            SessionState.SetInt(StepKey, 1);
            waitUntil = -1;
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!EditorApplication.isPlaying) return;
        // waits are counted in GAME time (Time.time): in batch mode the game can run slower than the real clock
        double now = Time.time;
        if (now < waitUntil) return;

        ForestZone zone = Zone("trailhead");
        if (step == 1)
        {
            if (waitUntil <= 0) { waitUntil = now + 3; return; }                     // let the scene start
            if (zone == null) { Debug.Log("FORESTCHECK|FAIL|no 'trailhead' zone in the scene"); Next(4); EditorApplication.ExitPlaymode(); return; }
            foreach (ForestZone z in ForestZone.All) z.ResetToSick();
            waitUntil = now + 2;                                                      // the player stands in the Trailhead: shadow animals appear
            Next(2);
        }
        else if (step == 2)
        {
            Report("before", zone);
            Picture(zone, "Logs/forest_heal_before.png");
            zone.Heal();
            waitUntil = now + ForestHealSettings.Current.waveSeconds + 3;             // the wave, the blocker, the animals
            Next(3);
        }
        else if (step == 3)
        {
            Report("after", zone);
            Picture(zone, "Logs/forest_heal_after.png");
            ForestZone next = ForestZone.NextToHeal();
            Debug.Log("FORESTCHECK|next zone to heal: " + (next != null ? next.displayName : "none"));
            Next(4);
            EditorApplication.ExitPlaymode();
        }
    }

    private static void Next(int step) { SessionState.SetInt(StepKey, step); }

    private static void Finish(int code)
    {
        if (EditorApplication.isPlaying) return;                                       // wait until play mode has really stopped
        SessionState.SetInt(StepKey, 0);
        EditorApplication.update -= Tick;
        PlayerPrefs.SetInt(TitleScreen.SkipInEditorKey, SessionState.GetInt("ForestHealCheck_OldSkip", 0));
        PlayerPrefs.Save();
        Debug.Log("FORESTCHECK|done");
        EditorApplication.Exit(code);
    }

    private static ForestZone Zone(string id)
    {
        foreach (ForestZone z in ForestZone.All) if (z != null && z.id == id) return z;
        return null;
    }

    /// <summary>One FORESTCHECK line: the zone's state, its floor, its scenery, its blocker and its animals.</summary>
    private static void Report(string when, ForestZone zone)
    {
        ForestHealSettings set = ForestHealSettings.Current;
        int sickTiles = 0, healthyTiles = 0;
        Vector2 c = zone.transform.position, h = zone.size * 0.5f;
        foreach (Tilemap map in Object.FindObjectsByType<Tilemap>())
        {
            Vector3Int a = map.WorldToCell(c - h), b = map.WorldToCell(c + h);
            for (int y = a.y; y <= b.y; y++)
                for (int x = a.x; x <= b.x; x++)
                {
                    TileBase t = map.GetTile(new Vector3Int(x, y, 0));
                    if (t == null || !zone.Contains(map.GetCellCenterWorld(new Vector3Int(x, y, 0)))) continue;
                    if (set.HealthyFor(t) != null) sickTiles++;
                    else if (set.SickFor(t) != null) healthyTiles++;
                }
        }
        int swaps = 0, healthySwaps = 0;
        foreach (HealSwap s in HealSwap.All) if (s != null && s.Zone == zone) { swaps++; if (s.IsHealthy) healthySwaps++; }
        string blockers = "";
        foreach (ZoneBlocker b in ZoneBlocker.All) if (b != null && b.Zone == zone) blockers += b.kind + (b.IsOpen ? " open " : " closed ");
        int shadows = 0, critters = 0;
        foreach (ShadowCreature sc in Object.FindObjectsByType<ShadowCreature>()) if (!sc.IsDead && zone.Contains(sc.transform.position)) shadows++;
        foreach (ForestCritter fc in Object.FindObjectsByType<ForestCritter>()) if (zone.Contains(fc.transform.position)) critters++;
        int fogOn = 0, fogAll = 0;
        foreach (Transform child in zone.transform)
            if (child.name == "Fog") { fogAll++; var fsr = child.GetComponent<SpriteRenderer>(); if (fsr != null && fsr.enabled && fsr.color.a > 0.001f) fogOn++; }
        var listField = typeof(ForestZone).GetField("fogPatches", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var list = listField != null ? listField.GetValue(zone) as System.Collections.IList : null;
        Debug.Log("FORESTCHECK|" + when + "|zone's own fog list: " + (list != null ? list.Count.ToString() : "?") + "|zone components on that object: " + zone.GetComponents<ForestZone>().Length);
        Debug.Log("FORESTCHECK|" + when + "|fog patches visible " + fogOn + "/" + fogAll + "|weather: " + WeatherManager.Current + "|big see-through sprites: " + BigTranslucent(zone));
        Debug.Log("FORESTCHECK|" + when + "|healed=" + zone.IsHealed + "|tiles sick=" + sickTiles + " healthy=" + healthyTiles +
                  "|scenery healthy " + healthySwaps + "/" + swaps + "|blocker: " + blockers.Trim() + "|shadows=" + shadows + " friendly=" + critters +
                  "|zones healed counter=" + GameState.GetCounter(ForestZone.HealedCounter));
    }

    /// <summary>Names of see-through sprites at least 3 tiles wide over the zone (to find what is tinting it), grouped and counted.</summary>
    private static string BigTranslucent(ForestZone zone)
    {
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>())
        {
            if (!sr.enabled || sr.color.a <= 0.01f || sr.color.a >= 0.9f || sr.bounds.size.x < 3f) continue;
            if (!zone.Contains(sr.bounds.center)) continue;
            string key = sr.gameObject.name + " (parent " + (sr.transform.parent != null ? sr.transform.parent.name : "-") + ", order " + sr.sortingOrder + ")";
            int n; counts.TryGetValue(key, out n); counts[key] = n + 1;
        }
        var parts = new System.Collections.Generic.List<string>();
        foreach (var kv in counts) parts.Add(kv.Value + "x " + kv.Key);
        return parts.Count > 0 ? string.Join("; ", parts) : "none";
    }

    /// <summary>Renders the zone's box from above into a picture.</summary>
    private static void Picture(ForestZone zone, string path)
    {
        try
        {
            var camGo = new GameObject("CheckCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = zone.size.y * 0.5f;
            cam.aspect = zone.size.x / zone.size.y;
            cam.transform.position = new Vector3(zone.transform.position.x, zone.transform.position.y, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0f, 1f, 0f);
            var rt = new RenderTexture(Mathf.RoundToInt(zone.size.x * 16), Mathf.RoundToInt(zone.size.y * 16), 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
        catch (System.Exception e) { Debug.Log("FORESTCHECK|no picture (" + e.Message + ")"); }
    }
}
