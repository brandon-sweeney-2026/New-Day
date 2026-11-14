using UnityEngine;

/// <summary>
/// Makes things that share a sorting order draw in the right order: the one LOWER on the map is drawn IN FRONT.
/// It runs by itself every time a scene loads (no setup needed):
///   - sets every camera to sort by the Y axis (Custom Axis 0,1,0)
///   - makes sprites under the "Objects" parent, and the player, sort by their PIVOT (their feet) instead of their centre,
///     so a tall tree and a short rock overlap correctly.
/// Settings you can change are the two constants below.
/// </summary>
public static class DrawOrderSetup
{
    /// <summary>The scene object that holds your placed objects (the Level Editors use "Objects").</summary>
    public const string ObjectsParentName = "Objects";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Run()
    {
        foreach (Camera cam in Camera.allCameras)
        {
            cam.transparencySortMode = TransparencySortMode.CustomAxis;
            cam.transparencySortAxis = new Vector3(0f, 1f, 0f);
        }

        GameObject objects = GameObject.Find(ObjectsParentName);
        if (objects != null)
            foreach (SpriteRenderer sr in objects.GetComponentsInChildren<SpriteRenderer>(true))
                sr.spriteSortPoint = SpriteSortPoint.Pivot;

        var player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null)
            foreach (SpriteRenderer sr in player.GetComponentsInChildren<SpriteRenderer>(true))
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
    }
}
