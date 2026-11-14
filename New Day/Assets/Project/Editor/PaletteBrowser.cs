using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// The palette grid used by the Farm Level Editor and the Forest Level Editor: a SEARCH box plus the palette split into collapsible groups.
///
/// Groups come from the asset's NAME, the part before the first underscore:  AsphaltRoad_Fill_1 -> group "AsphaltRoad";  Dock_Crate -> "Dock".
/// Assets with no underscore go in "Other". A group with more than 24 things is split again by the second part of the name
/// (AsphaltRoad_Wide_Top -> "Wide", AsphaltRoad_Narrow_NS -> "Narrow"). So name your own assets "Group_Thing" to sort them into their own section.
/// Type in the search box to see every match from every group in one flat list. Click a group's title to open or close it.
/// Must live in a folder named "Editor".
/// </summary>
public static class PaletteBrowser
{
    private const float Cell = 56f;
    private const int SplitOver = 24;

    private static readonly Dictionary<string, string> searches = new Dictionary<string, string>();
    private static readonly Dictionary<string, bool> open = new Dictionary<string, bool>();

    /// <summary>Draws the search box and the groups. 'selected' is the index into 'items' (unchanged by the grouping). Returns true if the selection changed.</summary>
    public static bool Draw(List<Object> items, ref int selected, string key, float windowWidth)
    {
        bool changed = false;
        string search;
        if (!searches.TryGetValue(key, out search)) search = "";

        EditorGUILayout.BeginHorizontal();
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(44f))) { search = ""; GUI.FocusControl(null); }
        EditorGUILayout.EndHorizontal();
        searches[key] = search;
        string s = search.Trim().ToLowerInvariant();

        int columns = Mathf.Max(1, Mathf.FloorToInt((windowWidth - 30f) / (Cell + 6f)));
        bool loading = false;

        if (s.Length > 0)
        {
            var hits = new List<int>();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].name.ToLowerInvariant().Contains(s)) hits.Add(i);
            EditorGUILayout.LabelField(hits.Count + " match(es)", EditorStyles.miniLabel);
            changed |= DrawGrid(items, hits, ref selected, columns, ref loading);
        }
        else
        {
            // collect groups in order of first appearance (the list is already sorted by name)
            var names = new List<string>();
            var members = new Dictionary<string, List<int>>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;
                string g = GroupOf(items[i].name);
                if (!members.ContainsKey(g)) { members[g] = new List<int>(); names.Add(g); }
                members[g].Add(i);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(names.Count + " group(s), " + items.Count + " items", EditorStyles.miniLabel);
            if (GUILayout.Button("Open all", EditorStyles.miniButton, GUILayout.Width(62f))) foreach (string n in names) open[key + "|" + n] = true;
            if (GUILayout.Button("Close all", EditorStyles.miniButton, GUILayout.Width(62f))) foreach (string n in names) open[key + "|" + n] = false;
            EditorGUILayout.EndHorizontal();

            foreach (string g in names)
            {
                List<int> idx = members[g];
                string fk = key + "|" + g;
                bool isOpen;
                if (!open.TryGetValue(fk, out isOpen)) isOpen = names.Count == 1 || idx.Contains(selected);       // first time: open if it's the only group or holds the selection
                bool hasSel = idx.Contains(selected);
                isOpen = EditorGUILayout.Foldout(isOpen, g + "  (" + idx.Count + ")" + (hasSel ? "  *" : ""), true, EditorStyles.foldoutHeader);
                open[fk] = isOpen;
                if (!isOpen) continue;

                EditorGUI.indentLevel++;
                if (idx.Count > SplitOver)
                {
                    var subNames = new List<string>();
                    var sub = new Dictionary<string, List<int>>();
                    foreach (int i in idx)
                    {
                        string sg = SubGroupOf(items[i].name);
                        if (!sub.ContainsKey(sg)) { sub[sg] = new List<int>(); subNames.Add(sg); }
                        sub[sg].Add(i);
                    }
                    foreach (string sg in subNames)
                    {
                        string sk = fk + "|" + sg;
                        bool so;
                        if (!open.TryGetValue(sk, out so)) so = sub[sg].Contains(selected);
                        so = EditorGUILayout.Foldout(so, sg + "  (" + sub[sg].Count + ")", true);
                        open[sk] = so;
                        if (so) changed |= DrawGrid(items, sub[sg], ref selected, columns, ref loading);
                    }
                }
                else changed |= DrawGrid(items, idx, ref selected, columns, ref loading);
                EditorGUI.indentLevel--;
            }
        }

        if (selected >= 0 && selected < items.Count && items[selected] != null)
            EditorGUILayout.LabelField("Selected: " + items[selected].name, EditorStyles.miniBoldLabel);
        if (loading) EditorApplication.QueuePlayerLoopUpdate();
        return changed;
    }

    private static bool DrawGrid(List<Object> items, List<int> idx, ref int selected, int columns, ref bool loading)
    {
        bool changed = false;
        int n = 0;
        while (n < idx.Count)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 12f);
            for (int c = 0; c < columns && n < idx.Count; c++, n++)
            {
                int i = idx[n];
                Object asset = items[i];
                Rect r = GUILayoutUtility.GetRect(Cell, Cell, GUILayout.Width(Cell), GUILayout.Height(Cell));
                bool on = selected == i;
                bool pressed = GUI.Toggle(r, on, new GUIContent("", asset != null ? asset.name : "(empty)"), "Button");
                if (pressed && !on) { selected = i; changed = true; }
                if (Event.current.type == EventType.Repaint && asset != null) DrawIcon(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), asset, ref loading);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        return changed;
    }

    private static string GroupOf(string name)
    {
        int u = name.IndexOf('_');
        return u > 0 ? name.Substring(0, u) : "Other";
    }

    private static string SubGroupOf(string name)
    {
        string[] p = name.Split('_');
        return p.Length >= 3 ? p[1] : "General";
    }

    public static Sprite SpriteOf(Object asset)
    {
        var autoTile = asset as AutoTile;
        if (autoTile != null) return autoTile.previewSprite;
        var randomTile = asset as RandomGroundTile;
        if (randomTile != null) return randomTile.previewSprite;
        var pathTile = asset as AutoPathTile;
        if (pathTile != null && pathTile.sprites != null && pathTile.sprites.Length == 16)
            return pathTile.sprites[10] != null ? pathTile.sprites[10] : pathTile.sprites[0];
        var tile = asset as Tile;
        if (tile != null) return tile.sprite;
        var go = asset as GameObject;
        if (go != null)
        {
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) return sr.sprite;
        }
        return null;
    }

    private static void DrawIcon(Rect area, Object asset, ref bool loading)
    {
        Sprite sprite = SpriteOf(asset);
        if (sprite != null && sprite.texture != null)
        {
            Texture2D tex = sprite.texture;
            Rect tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            float scale = Mathf.Min(area.width / tr.width, area.height / tr.height);
            float w = tr.width * scale, h = tr.height * scale;
            GUI.DrawTextureWithTexCoords(new Rect(area.x + (area.width - w) * 0.5f, area.y + (area.height - h) * 0.5f, w, h), tex, uv);
            return;
        }
        Texture preview = AssetPreview.GetAssetPreview(asset);
        if (preview == null) { loading = true; preview = AssetPreview.GetMiniThumbnail(asset); }       // the real preview is still being made; draw again soon
        if (preview != null) GUI.DrawTexture(area, preview, ScaleMode.ScaleToFit);
    }
}
