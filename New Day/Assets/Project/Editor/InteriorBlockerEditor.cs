using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

/// <summary>Drag the white handles in the Scene view to size an Interior Blocker (snaps to whole tiles). Must live in an Editor folder.</summary>
[CustomEditor(typeof(InteriorBlocker))]
public class InteriorBlockerEditor : Editor
{
    private readonly BoxBoundsHandle handle = new BoxBoundsHandle();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Move/size the box, then run Farm > Set Up > Rebuild Selected Interior to fill it with wall.", MessageType.None);
    }

    private void OnSceneGUI()
    {
        var b = (InteriorBlocker)target;
        handle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
        handle.center = b.transform.position;
        handle.size = new Vector3(b.size.x, b.size.y, 0f);
        handle.handleColor = Color.white; handle.wireframeColor = Color.clear;
        EditorGUI.BeginChangeCheck();
        handle.DrawHandle();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(b, "Resize blocker"); Undo.RecordObject(b.transform, "Resize blocker");
            b.size = new Vector2(Mathf.Max(1f, Mathf.Round(handle.size.x)), Mathf.Max(1f, Mathf.Round(handle.size.y)));
            b.transform.position = new Vector3(Mathf.Round(handle.center.x * 2f) / 2f, Mathf.Round(handle.center.y * 2f) / 2f, b.transform.position.z);
        }
    }
}
