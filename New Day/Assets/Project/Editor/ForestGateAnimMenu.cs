using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Forest > Add Green Gates
/// Imports the two green gate pictures (gate_across_green.png, gate_updown_green.png - copy them next to your other forest art) and puts them into every Forest Style.
/// The gates use them for the purple -> green colour change when they unlock. The Maze Generator calls this for you.
/// Must live in a folder named "Editor".
/// </summary>
public static class ForestGateAnimMenu
{
    [MenuItem("Farm/Forest/Add Green Gates")]
    public static void Run()
    {
        if (EnsureGreenGates(true)) EditorUtility.DisplayDialog("Done", "Every Forest Style now has green gate pictures. Gates will fade purple to green when they unlock.", "OK");
    }

    public static bool EnsureGreenGates(bool showDialogs)
    {
        string folder = ForestSetupMenu.FindFolder("gate_across_green");
        if (folder == null)
        {
            if (showDialogs) EditorUtility.DisplayDialog("Green gate art not found", "Couldn't find gate_across_green.png in your project.\nCopy gate_across_green.png and gate_updown_green.png into your forest art folder (next to gate_across_locked.png) and try again.", "OK");
            else Debug.LogWarning("Forest: gate_across_green.png not found - gates will use a green tint instead. (Copy the green gate pictures next to your forest art and run Farm > Forest > Add Green Gates.)");
            return false;
        }
        string across = folder + "/gate_across_green.png", updown = folder + "/gate_updown_green.png";
        Import(across); Import(updown);
        AssetDatabase.Refresh();
        var sa = AssetDatabase.LoadAssetAtPath<Sprite>(across);
        var su = AssetDatabase.LoadAssetAtPath<Sprite>(updown);
        if (sa == null || su == null) return false;

        foreach (string guid in AssetDatabase.FindAssets("t:ForestStyle"))
        {
            var style = AssetDatabase.LoadAssetAtPath<ForestStyle>(AssetDatabase.GUIDToAssetPath(guid));
            if (style == null) continue;
            style.gateAcrossGreen = sa; style.gateUpDownGreen = su;
            EditorUtility.SetDirty(style);
        }
        AssetDatabase.SaveAssets();
        return true;
    }

    private static void Import(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;      // same as the other gate pictures
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }
}
