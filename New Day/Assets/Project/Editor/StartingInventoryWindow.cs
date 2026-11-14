using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Farm > Debug > Starting Inventory
/// Edit what the player starts the game with, without hunting for the list in the Inspector.
///   - Top: the 20 backpack slots as they will look at the start (bottom row = hotbar, slots 1-10).
///   - Middle: the starting list. Drag the handles to reorder, change counts, press - to remove.
///   - Bottom: every item in the project with a search box and type filter. Press "Add" to put it in the list.
/// It edits the 'Starting items' list on the Player's Inventory component in the OPEN scene (or on the Player prefab if no scene has one).
/// Do it while NOT playing (changes made in Play mode are thrown away when you stop). Save the scene afterwards (Ctrl+S).
/// Must live in a folder named "Editor".
/// </summary>
public class StartingInventoryWindow : EditorWindow
{
    private Inventory target;
    private string targetLabel = "";
    private SerializedObject so;
    private SerializedProperty list;
    private ReorderableList rl;

    private readonly List<ItemData> all = new List<ItemData>();
    private string search = "";
    private int typeFilter;
    private int addAmount = 1;
    private Vector2 scrollList, scrollAll;

    [MenuItem("Farm/Debug/Starting Inventory")]
    public static void Open()
    {
        var w = GetWindow<StartingInventoryWindow>("Starting Inventory");
        w.minSize = new Vector2(380f, 560f);
    }

    private void OnEnable() { ReloadItems(); FindTarget(); }
    private void OnProjectChange() { ReloadItems(); Repaint(); }
    private void OnHierarchyChange() { if (target == null) { FindTarget(); Repaint(); } }

    // ---------------------------------------------------------------- finding things
    private void ReloadItems()
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

    private void FindTarget()
    {
        target = null; so = null; list = null; rl = null;
        Inventory found = Object.FindAnyObjectByType<Inventory>();
        string label = "";
        if (found != null) label = "Player in the open scene (" + found.gameObject.name + ")";
        else
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var inv = go.GetComponentInChildren<Inventory>(true);
                if (inv != null) { found = inv; label = "Prefab: " + path; break; }
            }
        }
        if (found == null) return;
        target = found;
        targetLabel = label;
        so = new SerializedObject(target);
        list = so.FindProperty("startingItems");
        BuildList();
    }

    private void BuildList()
    {
        rl = new ReorderableList(so, list, true, true, false, false);
        rl.drawHeaderCallback = r => EditorGUI.LabelField(r, "Starting items - placed in order from slot 1");
        rl.elementHeight = 22f;
        rl.drawElementCallback = (Rect r, int index, bool active, bool focused) =>
        {
            SerializedProperty el = list.GetArrayElementAtIndex(index);
            SerializedProperty item = el.FindPropertyRelative("item");
            SerializedProperty count = el.FindPropertyRelative("count");
            r.y += 2f; r.height = 18f;
            var it = item.objectReferenceValue as ItemData;
            DrawIcon(new Rect(r.x, r.y, 18f, 18f), it);
            float x = r.x + 22f;
            EditorGUI.PropertyField(new Rect(x, r.y, r.width - 22f - 112f, r.height), item, GUIContent.none);
            count.intValue = Mathf.Max(1, EditorGUI.IntField(new Rect(r.xMax - 108f, r.y, 52f, r.height), count.intValue));
            if (GUI.Button(new Rect(r.xMax - 52f, r.y, 24f, r.height), "+")) count.intValue = Mathf.Min(count.intValue + 10, 9999);
            if (GUI.Button(new Rect(r.xMax - 26f, r.y, 24f, r.height), "-"))
            {
                list.DeleteArrayElementAtIndex(index);
                GUI.changed = true;
            }
        };
    }

    // ---------------------------------------------------------------- drawing
    private static void DrawIcon(Rect r, ItemData it)
    {
        if (it == null || it.icon == null || it.icon.texture == null) { GUI.Box(r, GUIContent.none); return; }
        Texture2D t = it.icon.texture;
        Rect tr = it.icon.textureRect;
        var uv = new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height);
        GUI.DrawTextureWithTexCoords(r, t, uv);
    }

    /// <summary>Works out roughly where each starting item lands in the 20 slots (stacks fill up to the item's max stack).</summary>
    private void Simulate(ItemData[] slotItem, int[] slotCount)
    {
        for (int i = 0; i < slotItem.Length; i++) { slotItem[i] = null; slotCount[i] = 0; }
        for (int e = 0; e < list.arraySize; e++)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(e);
            var it = el.FindPropertyRelative("item").objectReferenceValue as ItemData;
            int left = el.FindPropertyRelative("count").intValue;
            if (it == null) continue;
            int max = Mathf.Max(1, it.maxStack);
            for (int i = 0; i < slotItem.Length && left > 0; i++)
                if (slotItem[i] == it && slotCount[i] < max) { int add = Mathf.Min(left, max - slotCount[i]); slotCount[i] += add; left -= add; }
            for (int i = 0; i < slotItem.Length && left > 0; i++)
                if (slotItem[i] == null) { int add = Mathf.Min(left, max); slotItem[i] = it; slotCount[i] = add; left -= add; }
        }
    }

    private void OnGUI()
    {
        if (target == null || so == null || list == null)
        {
            EditorGUILayout.HelpBox("No player with an Inventory found. Open the farm scene (the one with your Player) and press Find.", MessageType.Warning);
            if (GUILayout.Button("Find player")) FindTarget();
            return;
        }
        if (Application.isPlaying)
            EditorGUILayout.HelpBox("The game is running. Changes made now are lost when you stop. Stop Play, then edit.", MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(targetLabel, EditorStyles.miniLabel);
        if (GUILayout.Button("Find again", EditorStyles.miniButton, GUILayout.Width(70f))) { FindTarget(); GUIUtility.ExitGUI(); }
        EditorGUILayout.EndHorizontal();

        so.Update();

        // ---- the 20 slots ----
        var slotItem = new ItemData[Inventory.SlotCount];
        var slotCount = new int[Inventory.SlotCount];
        Simulate(slotItem, slotCount);
        EditorGUILayout.LabelField("Backpack at the start (top row = backpack, bottom row = hotbar 1-0)", EditorStyles.boldLabel);
        const float cell = 32f;
        Rect grid = GUILayoutUtility.GetRect(Inventory.Columns * (cell + 2f), 2f * (cell + 2f) + 2f);
        for (int row = 0; row < Inventory.Rows; row++)
            for (int col = 0; col < Inventory.Columns; col++)
            {
                int idx = row * Inventory.Columns + col;
                var r = new Rect(grid.x + col * (cell + 2f), grid.y + (Inventory.Rows - 1 - row) * (cell + 2f), cell, cell);      // slots 1-10 (the hotbar) are drawn on the bottom row
                GUI.Box(r, GUIContent.none);
                if (slotItem[idx] != null)
                {
                    DrawIcon(new Rect(r.x + 2f, r.y + 2f, cell - 4f, cell - 4f), slotItem[idx]);
                    if (slotCount[idx] > 1) GUI.Label(new Rect(r.x, r.yMax - 14f, r.width - 2f, 14f), slotCount[idx].ToString(), new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.LowerRight, normal = { textColor = Color.white } });
                    GUI.Label(r, new GUIContent("", slotItem[idx].itemName));          // hover shows the name
                }
            }

        // ---- the list ----
        EditorGUILayout.Space(4f);
        scrollList = EditorGUILayout.BeginScrollView(scrollList, GUILayout.MinHeight(120f), GUILayout.MaxHeight(220f));
        if (rl != null) rl.DoLayoutList();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear all")) { list.ClearArray(); }
        if (GUILayout.Button("Remove empty rows"))
        {
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == null) list.DeleteArrayElementAtIndex(i);
        }
        EditorGUILayout.EndHorizontal();

        // ---- the item browser ----
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Add an item", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(55f))) ReloadItems();
        EditorGUILayout.EndHorizontal();
        var typeNames = new List<string> { "All" };
        foreach (string n in System.Enum.GetNames(typeof(ItemData.ItemType))) typeNames.Add(n);
        typeFilter = GUILayout.Toolbar(typeFilter, typeNames.ToArray(), EditorStyles.miniButton);
        EditorGUILayout.BeginHorizontal();
        addAmount = Mathf.Max(1, EditorGUILayout.IntField("Amount to add", addAmount));
        foreach (int q in new[] { 1, 10, 50, 99 }) if (GUILayout.Button(q.ToString(), GUILayout.Width(34f))) addAmount = q;
        EditorGUILayout.EndHorizontal();

        scrollAll = EditorGUILayout.BeginScrollView(scrollAll);
        string s = search == null ? "" : search.Trim().ToLowerInvariant();
        int shown = 0;
        foreach (ItemData it in all)
        {
            if (it == null) continue;
            if (typeFilter > 0 && (int)it.type != typeFilter - 1) continue;
            if (s.Length > 0 && (it.itemName == null || !it.itemName.ToLowerInvariant().Contains(s))) continue;
            shown++;
            EditorGUILayout.BeginHorizontal(GUILayout.Height(22f));
            Rect ir = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f));
            DrawIcon(ir, it);
            EditorGUILayout.LabelField(it.itemName, GUILayout.MinWidth(80f));
            EditorGUILayout.LabelField(it.type.ToString(), EditorStyles.miniLabel, GUILayout.Width(64f));
            if (GUILayout.Button("Add", GUILayout.Width(44f))) AddItem(it, addAmount);
            EditorGUILayout.EndHorizontal();
        }
        if (shown == 0) EditorGUILayout.LabelField("No items match.", EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();

        if (so.ApplyModifiedProperties())
        {
            MarkChanged();
            Repaint();
        }
    }

    /// <summary>Adds to the count if the item is already in the list, otherwise a new row.</summary>
    private void AddItem(ItemData it, int amount)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty el = list.GetArrayElementAtIndex(i);
            if (el.FindPropertyRelative("item").objectReferenceValue == it)
            {
                SerializedProperty c = el.FindPropertyRelative("count");
                c.intValue = Mathf.Min(9999, c.intValue + amount);
                return;
            }
        }
        list.arraySize++;
        SerializedProperty n = list.GetArrayElementAtIndex(list.arraySize - 1);
        n.FindPropertyRelative("item").objectReferenceValue = it;
        n.FindPropertyRelative("count").intValue = Mathf.Max(1, amount);
    }

    private void MarkChanged()
    {
        EditorUtility.SetDirty(target);
        if (!EditorUtility.IsPersistent(target))
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        else AssetDatabase.SaveAssets();
    }
}
