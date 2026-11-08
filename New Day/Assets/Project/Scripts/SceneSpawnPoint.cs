using UnityEngine;

/// <summary>
/// Where the player appears when a scene loads. Put one on an empty GameObject.
/// If a SceneExit sent the player here with a matching 'Spawn Id', the player is moved to this spot.
/// Tick 'Is Default' on one point so the scene also works when you press Play directly.
/// </summary>
public class SceneSpawnPoint : MonoBehaviour
{
    [Tooltip("Name that a SceneExit's 'Arrive At' refers to, e.g. FromFarm.")]
    [SerializeField] private string spawnId = "Default";
    [Tooltip("Used when no id was given (pressing Play in this scene).")]
    [SerializeField] private bool isDefault = false;
    [Tooltip("Direction the player faces on arrival.")]
    [SerializeField] private Vector2 faceDirection = Vector2.down;

    private void Start()
    {
        string wanted = SceneExit.NextSpawnId;
        bool match = !string.IsNullOrEmpty(wanted) ? wanted == spawnId : isDefault;
        if (!match) return;

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        player.transform.position = transform.position;
        player.FaceDirection(faceDirection);

        var cam = FindAnyObjectByType<CameraFollow2D>();
        if (cam != null) cam.SnapToTarget();

        SceneExit.NextSpawnId = "";
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
