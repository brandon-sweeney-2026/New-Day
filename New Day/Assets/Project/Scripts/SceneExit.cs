using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A doorway to another scene: put it on an empty GameObject with a BoxCollider2D (it makes the collider a trigger).
/// When the player walks into it, the next scene loads. Place it at the edge of the farm, and another one
/// in the forest / city / mountain scene to lead back.
///
///  - Scene Name: the scene to load. It must be added in File > Build Profiles (Scenes in Build).
///  - Arrive At: the 'Spawn Id' of a SceneSpawnPoint in the new scene, so the player appears at the right spot.
///  - Require Interact: if ticked the player must press the action button instead of just walking in.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class SceneExit : MonoBehaviour
{
    [Header("Where it leads")]
    [SerializeField] private string sceneName = "";
    [Tooltip("Spawn Id of the SceneSpawnPoint to appear at in the next scene (leave empty to keep the scene's default).")]
    [SerializeField] private string arriveAt = "";

    [Header("How it works")]
    [Tooltip("Tick to make the player press the action button (shows a prompt) instead of walking in.")]
    [SerializeField] private bool requireInteract = false;
    [SerializeField] private string promptText = "Travel";

    /// <summary>Which spawn point the next scene should use. Read by SceneSpawnPoint.</summary>
    public static string NextSpawnId = "";

    private bool leaving;

    public string PromptText { get { return promptText; } }

    private void Reset()
    {
        var box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(1f, 1f);
    }

    private void Awake()
    {
        var box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        // Only walk-through doors must stay invisible to the action button, so the helper is added just when needed.
        if (requireInteract && GetComponent<SceneExitInteract>() == null) gameObject.AddComponent<SceneExitInteract>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (requireInteract) return;
        if (other.GetComponentInParent<PlayerController>() != null) Go();
    }

    public void Go()
    {
        if (leaving || string.IsNullOrEmpty(sceneName)) return;
        leaving = true;
        NextSpawnId = arriveAt;
        SceneManager.LoadScene(sceneName);
    }
}
