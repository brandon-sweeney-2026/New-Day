using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Diagnose > Find Broken Sprites
/// When many things go invisible at once (player, paths, palette pictures) it is almost always because a sprite sheet was re-imported and its sprites got new
/// internal ids, so everything that pointed at the old ones now points at nothing. This report finds that:
///   A. textures whose import settings changed in the last 12 hours (the likely culprits)
///   B. tiles in the level-editor palettes that have missing sprites
///   C. player animation clips that have missing sprites
/// Copies the report to the clipboard. Must live in a folder named "Editor".
/// </summary>
public static class FarmDiagnoseSprites
{
    [MenuItem("Farm/Diagnose/Find Broken Sprites")]
    public static void Run()
    {
        var sb = new StringBuilder();

        // ---------------- A ----------------
        sb.AppendLine("=== A. TEXTURES RE-IMPORTED IN THE LAST 12 HOURS (newest first) ===");
        var rows = new List<KeyValuePair<DateTime, string>>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            string meta = path + ".meta";
            if (!File.Exists(meta)) continue;
            DateTime t = File.GetLastWriteTime(meta);
            if ((DateTime.Now - t).TotalHours > 12) continue;
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            string mode = imp != null ? imp.spriteImportMode.ToString() : "?";
            string ppu = imp != null ? imp.spritePixelsPerUnit.ToString("0.#") : "?";
            int sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count();
            rows.Add(new KeyValuePair<DateTime, string>(t, t.ToString("HH:mm") + "  " + path + "   [" + mode + ", ppu " + ppu + ", " + sprites + " sprites]"));
        }
        foreach (var r in rows.OrderByDescending(x => x.Key).Take(80)) sb.AppendLine(r.Value);
        if (rows.Count == 0) sb.AppendLine("(none)");
        else if (rows.Count > 80) sb.AppendLine("... and " + (rows.Count - 80) + " more");

        // ---------------- B ----------------
        sb.AppendLine();
        sb.AppendLine("=== B. PALETTE TILES WITH MISSING SPRITES ===");
        string[] labels = { FarmEditorLabels.Ground, FarmEditorLabels.Paths, FarmEditorLabels.Soil, FarmEditorLabels.Wet, FarmEditorLabels.Fences, FarmEditorLabels.Pollution };
        foreach (string l in labels)
        {
            int total = 0, bad = 0;
            var examples = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("l:" + l + " t:TileBase"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile == null) continue;
                total++;
                if (HasMissing(tile)) { bad++; if (examples.Count < 4) examples.Add(path); }
            }
            sb.AppendLine(l + ": " + bad + " of " + total + " broken" + (examples.Count > 0 ? "   e.g. " + string.Join(", ", examples) : ""));
        }

        // ---------------- C ----------------
        sb.AppendLine();
        sb.AppendLine("=== C. ANIMATION CLIPS WITH MISSING SPRITES ===");
        int clips = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;
            int keys = 0, nulls = 0;
            foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (b.type != typeof(SpriteRenderer)) continue;
                foreach (ObjectReferenceKeyframe k in AnimationUtility.GetObjectReferenceCurve(clip, b)) { keys++; if (k.value == null) nulls++; }
            }
            if (keys == 0) continue;
            clips++;
            if (nulls > 0) sb.AppendLine(path + ": " + nulls + " of " + keys + " frames are MISSING");
        }
        sb.AppendLine(clips + " sprite animation clips checked.");

        string report = sb.ToString();
        EditorGUIUtility.systemCopyBuffer = report;
        Debug.Log(report);
        EditorUtility.DisplayDialog("Report copied", "The report is in the Console and on your clipboard. Paste it into the chat.", "OK");
    }

    private static bool HasMissing(TileBase asset)
    {
        var autoTile = asset as AutoTile;
        if (autoTile != null) return autoTile.previewSprite == null;
        var randomTile = asset as RandomGroundTile;
        if (randomTile != null) return randomTile.previewSprite == null;
        var pathTile = asset as AutoPathTile;
        if (pathTile != null)
        {
            if (pathTile.sprites == null) return true;
            foreach (Sprite s in pathTile.sprites) if (s == null) return true;
            return false;
        }
        var tile = asset as Tile;
        if (tile != null) return tile.sprite == null;
        return false;
    }
}
