using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Farm > Debug > Give Items
/// A test tool for Play mode: lists EVERY item in the project (seeds, crops, tools, weapons, spells, resources, food...), with a search box, type filter and amount.
/// Click "Give" to put it in the player's inventory (what doesn't fit drops on the ground at the player's feet).
/// Also: add gold, fill health/magic if your player has them, and "Give ALL" for quick stress tests.
/// Must live in a folder named "Editor".
/// </summary>
public class GiveItemsWindow : EditorWindow
{
    private readonly List<ItemData> all = new List<ItemData>();
    private string search = "";
    private int typeFilter;                         // 0 = all, then ItemType + 1
    private int amount = 10;
    private int goldAmount = 1000;
    private Vector2 scroll;
    private string status = "";

    [MenuItem("Farm/Debug/Give Items")]
    public static void Open()
    {
        var w = GetWindow<GiveItemsWindow>("Give Items");
        w.minSize = new Vector2(320f, 380f);
    }

    private void OnEnable() { Reload(); }
    private void OnProjectChange() { Reload(); Repaint(); }

    private void Reload()
    {
        all.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (it != null) all.Add(it);
        }
        all.Sort((a, b) =>
        {
            int c = a.type.CompareTo(b.type);
            return c != 0 ? c : string.Compare(a.itemName, b.itemName, System.StringComparison.OrdinalIgnoreCase);
        });
    }

    private void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Press Play first - items go into the running player's inventory.", MessageType.Info);
        }

        // ---- top controls ----
        EditorGUILayout.BeginHorizontal();
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(55f))) Reload();
        EditorGUILayout.EndHorizontal();

        var typeNames = new List<string> { "All types" };
        foreach (string n in System.Enum.GetNames(typeof(ItemData.ItemType))) typeNames.Add(n);
        typeFilter = GUILayout.Toolbar(typeFilter, typeNames.ToArray(), EditorStyles.miniButton);

        EditorGUILayout.BeginHorizontal();
        amount = Mathf.Max(1, EditorGUILayout.IntField("Amount", amount));
        foreach (int q in new[] { 1, 10, 50, 99 }) if (GUILayout.Button(q.ToString(), GUILayout.Width(34f))) amount = q;
        EditorGUILayout.EndHorizontal();

        // ---- gold ----
        EditorGUILayout.BeginHorizontal();
        goldAmount = EditorGUILayout.IntField("Gold", goldAmount);
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("+ Gold", GUILayout.Width(70f))) { GameState.AddGold(goldAmount); status = "Added " + goldAmount + " gold."; }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        // ---- list ----
        string q2 = (search ?? "").Trim().ToLowerInvariant();
        IEnumerable<ItemData> shown = all.Where(i => i != null);
        if (typeFilter > 0) shown = shown.Where(i => (int)i.type == typeFilter - 1);
        if (q2.Length > 0) shown = shown.Where(i => i.itemName != null && i.itemName.ToLowerInvariant().Contains(q2) || i.name.ToLowerInvariant().Contains(q2));
        var list = shown.ToList();

        EditorGUILayout.LabelField(list.Count + " item" + (list.Count == 1 ? "" : "s"), EditorStyles.miniLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        ItemData.ItemType? lastType = null;
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        foreach (ItemData item in list)
        {
            if (lastType == null || lastType.Value != item.type)
            {
                lastType = item.type;
                EditorGUILayout.LabelField(item.type.ToString(), EditorStyles.boldLabel);
            }
            EditorGUILayout.BeginHorizontal(GUILayout.Height(24f));
            Rect r = GUILayoutUtility.GetRect(22f, 22f, GUILayout.Width(22f), GUILayout.Height(22f));
            DrawIcon(r, item.icon);
            if (GUILayout.Button(new GUIContent(item.itemName, item.name + "  (click to select the asset)"), EditorStyles.label, GUILayout.Height(22f))) { Selection.activeObject = item; EditorGUIUtility.PingObject(item); }
            if (GUILayout.Button("Give", GUILayout.Width(50f), GUILayout.Height(22f))) Give(item, amount);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndScrollView();

        // ---- bottom ----
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(!Application.isPlaying || list.Count == 0);
        if (GUILayout.Button("Give ALL shown (x" + amount + ")") &&
            EditorUtility.DisplayDialog("Give all?", "Give " + amount + " of each of the " + list.Count + " items shown? What doesn't fit drops on the ground.", "Give", "Cancel"))
            foreach (ItemData item in list) Give(item, amount);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
    }

    private static void DrawIcon(Rect r, Sprite s)
    {
        if (s == null || s.texture == null) return;
        Texture2D tex = s.texture;
        Rect tr = s.textureRect;
        var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        float scale = Mathf.Min(r.width / tr.width, r.height / tr.height);
        float w = tr.width * scale, h = tr.height * scale;
        GUI.DrawTextureWithTexCoords(new Rect(r.x + (r.width - w) * 0.5f, r.y + (r.height - h) * 0.5f, w, h), tex, uv);
    }

    private void Give(ItemData item, int n)
    {
        Inventory inv = Inventory.Instance;
        if (inv == null) { status = "No Inventory in the scene."; return; }
        int left = inv.Add(item, n);
        if (left > 0)
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Vector3 at = player != null ? player.transform.position + Vector3.down * 0.5f : Vector3.zero;
            ItemPickup.Spawn(item, left, at);
        }
        status = "Gave " + (n - left) + " x " + item.itemName + (left > 0 ? "  (" + left + " dropped on the ground - inventory full)" : "");
    }
}
