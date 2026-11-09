using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Farm > Lighting > Set Up Lighting      (run once)
/// Farm > Lighting > Drop Sample Lights Near Player   (optional, for testing)
///
/// Set Up Lighting:
///  1. imports the light-object art (copy the "Lights" folder into Assets first)
///  2. creates the three materials the lighting system needs in Assets/Resources/Lighting
///  3. builds prefabs for Torch, Lantern, Light Post, 3 Glowing Mushrooms and 5 Frost Crystals next to the art (in a "Prefabs" folder)
///     and labels them so they show up in the Level Editor's Objects palette
///  4. adds the LightingManager (sun + shadows + night) to the open scene
/// Safe to run again.
/// </summary>
public static class LightingSetupMenu
{
    private class Def
    {
        public string name, art; public int frames;
        public Color color; public float intensity, outer, inner; public Vector2 lightOffset;
        public LightBehaviour behaviour; public float dayStrength, flicker, flickerSpeed, pulse, pulseSpeed;
        public Vector2 colSize, colOffset; public bool collider; public float sorter; public float fps;
    }

    private static List<Def> Defs()
    {
        var l = new List<Def>();
        l.Add(new Def { name = "Torch", art = "torch", frames = 4, fps = 8, color = new Color(1f, 0.62f, 0.22f), intensity = 1.5f, outer = 5.5f, inner = 0.3f,
            lightOffset = new Vector2(0f, 1.75f), behaviour = LightBehaviour.DimmerByDay, dayStrength = 0.25f, flicker = 0.25f, flickerSpeed = 9f,
            collider = true, colSize = new Vector2(0.4f, 0.3f), colOffset = new Vector2(0f, 0.15f), sorter = 0.9f });
        l.Add(new Def { name = "Lantern", art = "lantern", frames = 2, fps = 3, color = new Color(1f, 0.8f, 0.45f), intensity = 1.2f, outer = 4.5f, inner = 0.3f,
            lightOffset = new Vector2(0f, 0.65f), behaviour = LightBehaviour.DuskToDawn, flicker = 0.07f, flickerSpeed = 6f,
            collider = true, colSize = new Vector2(0.5f, 0.3f), colOffset = new Vector2(0f, 0.15f), sorter = 0.4f });
        l.Add(new Def { name = "LightPost", art = "lamp_post", frames = 1, fps = 1, color = new Color(1f, 0.92f, 0.6f), intensity = 1.4f, outer = 7f, inner = 0.6f,
            lightOffset = new Vector2(0f, 3f), behaviour = LightBehaviour.DuskToDawn, flicker = 0.02f, flickerSpeed = 3f,
            collider = true, colSize = new Vector2(0.4f, 0.3f), colOffset = new Vector2(0f, 0.15f), sorter = 1.5f });
        foreach (string c in new[] { "teal", "blue", "violet" })
        {
            Color col = c == "teal" ? new Color(0.3f, 1f, 0.8f) : c == "blue" ? new Color(0.4f, 0.6f, 1f) : new Color(0.75f, 0.45f, 1f);
            l.Add(new Def { name = "GlowMushroom_" + Cap(c), art = "glow_mushroom_" + c, frames = 1, fps = 1, color = col, intensity = 0.9f, outer = 3f, inner = 0.2f,
                lightOffset = new Vector2(0f, 0.5f), behaviour = LightBehaviour.DimmerByDay, dayStrength = 0.3f, flicker = 0f, pulse = 0.2f, pulseSpeed = 1.5f,
                collider = false, sorter = 0.3f });
        }
        foreach (string c in new[] { "blue", "aqua", "violet", "pink", "green" })
        {
            Color col = c == "blue" ? new Color(0.45f, 0.75f, 1f) : c == "aqua" ? new Color(0.3f, 1f, 1f) : c == "violet" ? new Color(0.7f, 0.5f, 1f)
                      : c == "pink" ? new Color(1f, 0.5f, 0.85f) : new Color(0.5f, 1f, 0.6f);
            l.Add(new Def { name = "FrostCrystal_" + Cap(c), art = "frost_crystal_" + c, frames = 1, fps = 1, color = col, intensity = 1.0f, outer = 3.8f, inner = 0.3f,
                lightOffset = new Vector2(0f, 0.8f), behaviour = LightBehaviour.DimmerByDay, dayStrength = 0.35f, flicker = 0f, pulse = 0.15f, pulseSpeed = 1.2f,
                collider = true, colSize = new Vector2(0.8f, 0.4f), colOffset = new Vector2(0f, 0.2f), sorter = 0.5f });
        }
        return l;
    }

    private static string Cap(string s) { return char.ToUpper(s[0]) + s.Substring(1); }

    [MenuItem("Farm/Lighting/Set Up Lighting")]
    public static void Run()
    {
        if (GraphicsSettings.currentRenderPipeline == null)
        {
            EditorUtility.DisplayDialog("Universal Render Pipeline needed",
                "Lights need the Universal Render Pipeline with the 2D Renderer (the 'URP 2D' template). Your project doesn't seem to be using it.\nSee the README for how to switch.", "OK");
            return;
        }

        string dir = FindFolder("torch");
        if (dir == null)
        {
            EditorUtility.DisplayDialog("Light art not found",
                "Couldn't find torch.png in your project.\nCopy the 'Lights' folder into Assets (for example Assets/Art/Lights) and run this again.", "OK");
            return;
        }

        // ---- materials (kept in Resources so they are always included in your build) ----
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Lighting");
        MakeMaterial("Assets/Resources/Lighting/SpriteLit.mat", "Universal Render Pipeline/2D/Sprite-Lit-Default");
        MakeMaterial("Assets/Resources/Lighting/SpriteUnlit.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
        Material lit = MakeMaterial("Assets/Resources/Lighting/ShadowMaterial.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
        Material litSprites = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Lighting/SpriteLit.mat");

        // ---- prefabs ----
        string prefabDir = dir + "/Prefabs";
        EnsureFolder(prefabDir);
        int made = 0;
        foreach (Def d in Defs())
        {
            string basePath = dir + "/" + d.art + ".png";
            string glowPath = dir + "/" + d.art + "_glow.png";
            if (!File.Exists(basePath) || !File.Exists(glowPath)) { Debug.LogWarning("Missing art for " + d.name); continue; }

            ImportSingle(basePath, 16f);
            Sprite baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(basePath);
            Sprite[] glow;
            if (d.frames > 1) glow = SliceStrip(glowPath, d.frames);
            else { ImportSingle(glowPath, 16f); glow = new[] { AssetDatabase.LoadAssetAtPath<Sprite>(glowPath) }; }

            var go = new GameObject(d.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = baseSprite;
            sr.sortingOrder = 10;
            if (litSprites != null) sr.sharedMaterial = litSprites;
            if (d.collider)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = d.colSize; box.offset = d.colOffset;
            }
            var sorter = go.AddComponent<BehindPlayerSorter>();
            sorter.thresholdOffset = d.sorter;

            var ls = go.AddComponent<LightSource>();
            ls.lightColor = d.color; ls.intensity = d.intensity; ls.outerRadius = d.outer; ls.innerRadius = d.inner;
            ls.lightOffset = d.lightOffset; ls.behaviour = d.behaviour; ls.daytimeStrength = d.dayStrength;
            ls.flickerAmount = d.flicker; ls.flickerSpeed = d.flickerSpeed; ls.pulseAmount = d.pulse; ls.pulseSpeed = d.pulseSpeed;
            ls.glowFrames = glow; ls.glowFramesPerSecond = d.fps;
            // trees and posts cast longer shadows
            if (d.name == "LightPost") go.AddComponent<ShadowCaster>().lengthMultiplier = 1.2f;

            string prefabPath = prefabDir + "/" + d.name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SetLabels(prefab, new[] { FarmEditorLabels.Objects });
            made++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ---- the manager in the scene ----
        var mgr = Object.FindAnyObjectByType<LightingManager>();
        if (mgr == null)
        {
            var go = new GameObject("LightingManager");
            Undo.RegisterCreatedObjectUndo(go, "Create LightingManager");
            go.AddComponent<LightingManager>();
        }

        EditorUtility.DisplayDialog("Lighting ready",
            "Done!\n\n- LightingManager added to the scene (sun, shadows, night).\n- " + made + " light prefabs created in " + prefabDir +
            "\n  (they also appear in the Level Editor's Objects palette).\n\nPress Play. To test, select LightingManager, tick 'Use Preview Hour' and drag 'Preview Hour'.", "OK");
    }

    [MenuItem("Farm/Lighting/Drop Sample Lights Near Player")]
    public static void DropSamples()
    {
        var player = Object.FindAnyObjectByType<PlayerController>();
        Vector3 start = player != null ? player.transform.position + new Vector3(-6f, 3f, 0f) : Vector3.zero;
        int i = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (prefab == null || prefab.GetComponent<LightSource>() == null) continue;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(inst, "Drop light");
            inst.transform.position = start + new Vector3((i % 6) * 2.2f, -(i / 6) * 3.5f, 0f);
            i++;
        }
        if (i == 0) EditorUtility.DisplayDialog("No light prefabs", "Run Farm > Lighting > Set Up Lighting first.", "OK");
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private static Material MakeMaterial(string path, string shaderName)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader s = Shader.Find(shaderName);
        if (s == null) { Debug.LogError("Shader not found: " + shaderName + " (is the 2D Renderer / URP installed?)"); return null; }
        var m = new Material(s);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static string FindFolder(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Texture2D"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == fileName) return Path.GetDirectoryName(p).Replace('\\', '/');
        }
        return null;
    }

    private static void ImportSingle(string path, float ppu)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = ppu;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    /// <summary>Cuts a horizontal strip of equally wide frames. Returns the frames left to right.</summary>
    private static Sprite[] SliceStrip(string path, int frames)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cw = tex.width / frames;
        string baseName = Path.GetFileNameWithoutExtension(path);

        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        UnityEditor.U2D.Sprites.ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(imp);
        provider.InitSpriteEditorDataProvider();

        var rects = new List<SpriteRect>();
        for (int c = 0; c < frames; c++)
        {
            var rect = new SpriteRect();
            rect.name = baseName + "_" + c;
            rect.rect = new Rect(c * cw, 0, cw, tex.height);
            rect.alignment = SpriteAlignment.BottomCenter;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.spriteID = GUID.Generate();
            rects.Add(rect);
        }
        provider.SetSpriteRects(rects.ToArray());
        var idProvider = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
        var pairs = new List<SpriteNameFileIdPair>();
        foreach (SpriteRect sr in rects) pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        idProvider.SetNameFileIdPairs(pairs);
        provider.Apply();
        imp.SaveAndReimport();

        var result = new Sprite[frames];
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var s = o as Sprite;
            if (s == null) continue;
            for (int c = 0; c < frames; c++) if (s.name == baseName + "_" + c) result[c] = s;
        }
        return result;
    }
}
