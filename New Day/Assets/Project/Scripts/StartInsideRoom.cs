using UnityEngine;

/// <summary>
/// Lets the game start with the player already INSIDE the house.
///
/// Normally the camera only gets locked to the room when you walk through the door, so if the player starts indoors the camera
/// stays limited to the farm map and shows the farm. This checks at the start of the game whether the player is standing in a
/// HouseInterior and, if so, enters the room properly (camera limit, camera snap, "indoors" lighting).
///
/// It adds itself when the game starts. If it still doesn't work for you, add this component to any object in the scene
/// and tick "Always Start Inside" - it will then enter the room no matter where the player is standing.
/// Watch the Console: it prints one line saying what it found.
/// </summary>
public class StartInsideRoom : MonoBehaviour
{
    [Tooltip("Enter the house room at the start of the game even if the player is not standing inside its yellow outline.")]
    public bool alwaysStartInside = false;
    [Tooltip("Which HouseInterior to use when 'Always Start Inside' is on and there are several (0 = first).")]
    public int roomIndex = 0;
    [Tooltip("How far outside the room outline still counts as 'inside' (tiles).")]
    public float tolerance = 1.5f;

    private float timeLeft = 1.5f;
    private bool done;
    private bool autoCreated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindAnyObjectByType<StartInsideRoom>() != null) return;
        var go = new GameObject("StartInsideRoom");
        go.AddComponent<StartInsideRoom>().autoCreated = true;
    }

    // Keep trying for a moment: the player, camera and spawn points all finish setting up in the first few frames.
    private void LateUpdate()
    {
        if (done) return;
        timeLeft -= Time.unscaledDeltaTime;
        if (Check()) { done = true; if (autoCreated) Destroy(gameObject); return; }
        if (timeLeft <= 0f)
        {
            done = true;
            var p = FindAnyObjectByType<PlayerController>();
            Debug.Log("StartInsideRoom: the player is not inside any house room (player at " +
                      (p != null ? p.transform.position.ToString() : "not found") + "). Nothing changed.");
            if (autoCreated) Destroy(gameObject);
        }
    }

    private bool Check()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) return false;
        Vector2 p = player.transform.position;

        HouseInterior[] rooms = FindObjectsByType<HouseInterior>();
        if (rooms.Length == 0) return false;

        HouseInterior chosen = null;
        if (alwaysStartInside) chosen = rooms[Mathf.Clamp(roomIndex, 0, rooms.Length - 1)];
        else
        {
            foreach (HouseInterior room in rooms)
            {
                Rect r = room.WorldBounds;
                r.xMin -= tolerance; r.yMin -= tolerance; r.xMax += tolerance; r.yMax += tolerance;
                if (r.Contains(p)) { chosen = room; break; }
            }
        }
        if (chosen == null) return false;

        chosen.EnterRoom();
        // make sure the camera really jumps to the player right now
        var cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.SnapToTarget();
        Debug.Log("StartInsideRoom: entered room '" + chosen.name + "' (player at " + player.transform.position + ").");
        return true;
    }
}
