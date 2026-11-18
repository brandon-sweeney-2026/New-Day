using UnityEngine;

/// <summary>
/// Puts the player next to the bed right after a saved game has loaded, then removes itself.
/// SaveSystem creates it; you never add it yourself.
/// It waits two frames so the spawn points and the camera have finished setting themselves up first.
/// </summary>
public class WakeUpAtBed : MonoBehaviour
{
    private int frames;

    private void LateUpdate()
    {
        frames++;
        if (frames < 2) return;

        var player = FindAnyObjectByType<PlayerController>();
        if (player != null && Bed.Main != null)
        {
            Bed.Main.PlaceAtWakePoint(player.transform);
            var cam = FindAnyObjectByType<CameraFollow2D>();
            if (cam != null) cam.SnapToTarget();
        }
        Destroy(gameObject);
    }
}
