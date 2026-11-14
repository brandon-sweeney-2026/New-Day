using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Farm > Diagnose > Check Player and Palettes
/// Prints a report (and copies it to the clipboard) about: how many labelled assets each level-editor palette would find, the open scene's player pictures and
/// anything drawn on top of the player, the camera, and the active Scene-view tool. Paste the report to me if something looks wrong.
/// Must live in a folder named "Editor".
/// </summary>
public static class FarmDiagnoseMenu
{
    [MenuItem("Farm/Diagnose/Check Player and Palettes")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== PALETTES (assets with each label) ===");
        string[] labels = { FarmEditorLabels.Ground, FarmEditorLabels.Paths, FarmEditorLabels.Soil, FarmEditorLabels.Wet, FarmEditorLabels.Fences, FarmEditorLabels.Objects, FarmEditorLabels.Pollution };
        foreach (string l in labels)
        {
            int tiles = AssetDatabase.FindAssets("l:" + l + " t:TileBase").Length;
            int prefabs = AssetDatabase.FindAssets("l:" + l + " t:Prefab").Length;
            int loaded = 0;
            foreach (string g in AssetDatabase.FindAssets("l:" + l + " t:TileBase"))
                if (AssetDatabase.LoadAssetAtPath<TileBase>(AssetDatabase.GUIDToAssetPath(g)) != null) loaded++;
            sb.AppendLine(l + ": " + tiles + " tiles (" + loaded + " load OK), " + prefabs + " prefabs");
        }

        sb.AppendLine();
        sb.AppendLine("=== SCENE VIEW ===");
        sb.AppendLine("Tools.current = " + Tools.current + "   (painting needs Move/Rect/Rotate/Scale, NOT the Hand 'View' tool)");
        sb.AppendLine("Play mode = " + Application.isPlaying + "   (painting is off in Play mode)");
        sb.AppendLine("Open scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        sb.AppendLine();
        sb.AppendLine("=== CAMERA ===");
        foreach (Camera c in Object.FindObjectsByType<Camera>())
            sb.AppendLine(c.name + ": ortho=" + c.orthographic + " size=" + c.orthographicSize + " pos=" + c.transform.position + " cullingMask=" + c.cullingMask + " enabled=" + c.enabled);

        sb.AppendLine();
        sb.AppendLine("=== PLAYER ===");
        var players = Object.FindObjectsByType<PlayerController>();
        if (players.Length == 0) sb.AppendLine("No PlayerController in the open scene.");
        foreach (PlayerController p in players)
        {
            sb.AppendLine(p.name + ": active=" + p.gameObject.activeInHierarchy + " pos=" + p.transform.position + " scale=" + p.transform.lossyScale + " layer=" + LayerMask.LayerToName(p.gameObject.layer));
            var renderers = p.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0) sb.AppendLine("  !! no SpriteRenderer on the player or its children");
            int playerOrder = -9999;
            foreach (SpriteRenderer r in renderers)
            {
                sb.AppendLine("  SpriteRenderer '" + r.name + "': enabled=" + r.enabled + " sprite=" + (r.sprite != null ? r.sprite.name : "NONE") +
                              " color=" + r.color + " layer='" + r.sortingLayerName + "' order=" + r.sortingOrder + " material=" + (r.sharedMaterial != null ? r.sharedMaterial.name : "NONE"));
                if (r.sortingOrder > playerOrder) playerOrder = r.sortingOrder;
            }
            // what is drawn over the player?
            sb.AppendLine("  Drawn ABOVE the player at the player's position:");
            int found = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.transform.IsChildOf(p.transform)) continue;
                if (!(r is SpriteRenderer || r is TilemapRenderer)) continue;
                if (r.sortingOrder < playerOrder) continue;
                Vector3 pos = p.transform.position;
                bool covers = r.bounds.Contains(new Vector3(pos.x, pos.y, r.bounds.center.z));
                if (!covers) continue;
                if (r is TilemapRenderer)
                {
                    var tm = r.GetComponent<Tilemap>();
                    if (tm == null || tm.GetTile(tm.WorldToCell(pos)) == null) continue;
                }
                sb.AppendLine("    - " + Path(r.transform) + " (" + r.GetType().Name + ") layer='" + r.sortingLayerName + "' order=" + r.sortingOrder);
                if (++found >= 12) break;
            }
            if (found == 0) sb.AppendLine("    (nothing found)");
        }

        string report = sb.ToString();
        EditorGUIUtility.systemCopyBuffer = report;
        Debug.Log(report);
        EditorUtility.DisplayDialog("Report copied", "The report is in the Console and copied to your clipboard. Paste it into the chat.", "OK");
    }

    private static string Path(Transform t)
    {
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
