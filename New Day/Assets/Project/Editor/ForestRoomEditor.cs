using UnityEditor;
using UnityEngine;

/// <summary>Adds the "Rebuild Room" button to the ForestRoom Inspector. Must live in an Editor folder.</summary>
[CustomEditor(typeof(ForestRoom))]
public class ForestRoomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Change the sides / style above, then press the button. A gap is 2 tiles wide. The room next door needs a matching gap on the shared side.", MessageType.None);
        if (GUILayout.Button("Rebuild Room", GUILayout.Height(28)))
        {
            foreach (Object t in targets)
            {
                var r = (ForestRoom)t;
                Undo.RegisterFullObjectHierarchyUndo(r.gameObject, "Rebuild room");
                r.Rebuild();
                EditorUtility.SetDirty(r);
            }
        }
    }
}

/// <summary>Drag the white handles in the Scene view to fit the grove box.</summary>
[CustomEditor(typeof(ForestGrove))]
public class ForestGroveEditor : Editor
{
    private readonly UnityEditor.IMGUI.Controls.BoxBoundsHandle handle = new UnityEditor.IMGUI.Controls.BoxBoundsHandle();

    private void OnSceneGUI()
    {
        var g = (ForestGrove)target;
        handle.axes = UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.X | UnityEditor.IMGUI.Controls.PrimitiveBoundsHandle.Axes.Y;
        handle.center = g.transform.position;
        handle.size = new Vector3(g.boxSize.x, g.boxSize.y, 0f);
        handle.handleColor = Color.white;
        handle.wireframeColor = Color.clear;
        EditorGUI.BeginChangeCheck();
        handle.DrawHandle();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(g, "Resize grove");
            Undo.RecordObject(g.transform, "Resize grove");
            g.boxSize = new Vector2(Mathf.Max(4f, Mathf.Round(handle.size.x)), Mathf.Max(4f, Mathf.Round(handle.size.y)));
            g.transform.position = new Vector3(handle.center.x, handle.center.y, g.transform.position.z);
        }
    }
}
