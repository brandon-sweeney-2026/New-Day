using UnityEditor;
using UnityEngine;

/// <summary>
/// The Inspector for a DecorPiece: the normal fields, plus Turn buttons and a button that opens the Interior Decorator.
/// Changing Item or Facing here updates the picture straight away. This file must live in a folder named "Editor".
/// </summary>
[CustomEditor(typeof(DecorPiece))]
[CanEditMultipleObjects]
public class DecorPieceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
            foreach (Object t in targets) { var p = (DecorPiece)t; p.Refresh(); SaveRoom(p); }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Turn left")) foreach (Object t in targets) { InteriorDecorTools.Turn((DecorPiece)t, false); SaveRoom((DecorPiece)t); }
            if (GUILayout.Button("Turn right")) foreach (Object t in targets) { InteriorDecorTools.Turn((DecorPiece)t, true); SaveRoom((DecorPiece)t); }
        }
        if (GUILayout.Button("Open Interior Decorator")) InteriorDecoratorWindow.Open();
    }

    private static void SaveRoom(DecorPiece p)
    {
        DecorRoom room = p.GetComponentInParent<DecorRoom>();
        if (room != null) InteriorDecorTools.SaveLayout(room);
    }
}
