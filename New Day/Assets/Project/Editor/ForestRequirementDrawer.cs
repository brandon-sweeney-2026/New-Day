using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shows every "item + count" (what a stump or the Fairy asks for) as a DROP-DOWN of collectible items, grouped by type
/// (Crop, Resource, Food, Seed), plus a count box. Works everywhere a ForestRequirement appears: the ForestProgression asset's
/// stump list, each ForestDoor's Requirements, and the Fairy's quests. Must live in a folder named "Editor".
/// </summary>
[CustomPropertyDrawer(typeof(ForestRequirement))]
public class ForestRequirementDrawer : PropertyDrawer
{
    private static List<ItemData> items;
    private static string[] labels;

    [InitializeOnLoadMethod]
    private static void Hook() { EditorApplication.projectChanged += () => { items = null; }; }

    private static void Refresh()
    {
        items = new List<ItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            var it = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (it == null) continue;
            // things you can pick up and hand over (not tools, weapons or spells)
            if (it.type == ItemData.ItemType.Tool || it.type == ItemData.ItemType.Weapon || it.type == ItemData.ItemType.Spell) continue;
            items.Add(it);
        }
        items.Sort((a, b) =>
        {
            int c = a.type.CompareTo(b.type);
            return c != 0 ? c : string.Compare(a.itemName, b.itemName, System.StringComparison.OrdinalIgnoreCase);
        });
        labels = new string[items.Count + 1];
        labels[0] = "(none)";
        for (int i = 0; i < items.Count; i++)
            labels[i + 1] = items[i].type + "/" + (string.IsNullOrEmpty(items[i].itemName) ? items[i].name : items[i].itemName);
    }

    public override void OnGUI(Rect pos, SerializedProperty property, GUIContent label)
    {
        if (items == null) Refresh();
        SerializedProperty itemProp = property.FindPropertyRelative("item");
        SerializedProperty countProp = property.FindPropertyRelative("count");

        EditorGUI.BeginProperty(pos, label, property);
        Rect body = EditorGUI.PrefixLabel(pos, label);

        var current = itemProp.objectReferenceValue as ItemData;
        int selected = current == null ? 0 : items.IndexOf(current) + 1;

        float countW = 56f;
        var popupRect = new Rect(body.x, body.y, body.width - countW - 4f, body.height);
        var countRect = new Rect(body.xMax - countW, body.y, countW, body.height);

        if (current != null && selected == 0)
        {
            // an item that isn't a normal collectible (e.g. a tool): still show it so nothing is lost
            EditorGUI.ObjectField(popupRect, itemProp, GUIContent.none);
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUI.Popup(popupRect, selected, labels);
            if (EditorGUI.EndChangeCheck()) itemProp.objectReferenceValue = picked == 0 ? null : items[picked - 1];
        }

        float oldLabel = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 14f;
        countProp.intValue = Mathf.Max(1, EditorGUI.IntField(countRect, new GUIContent("x"), countProp.intValue));
        EditorGUIUtility.labelWidth = oldLabel;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) { return EditorGUIUtility.singleLineHeight; }
}
