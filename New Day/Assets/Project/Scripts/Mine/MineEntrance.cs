using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The grand entrance at the top of the mountain. Press the action button in the doorway to go into the mine.
/// Put the name of the mine scene in "Scene Name" (it must be in File > Build Profiles). While that is empty it says the mine isn't open yet.
/// Needs a trigger Collider2D in the doorway (the setup menu adds one).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MineEntrance : MonoBehaviour, IInteractable
{
    [Tooltip("The scene to load. Leave empty until the mine scene exists.")]
    public string sceneName = "";
    [Tooltip("Spawn Id of the SceneSpawnPoint in the mine where the player appears.")]
    public string arriveAt = "FromMountain";
    public string prompt = "Enter the mine";
    [TextArea] public string closedMessage = "A cold breeze drifts out of the dark. The mine isn't open yet.";

    public string InteractPrompt { get { return prompt; } }

    public void Interact(PlayerActions player)
    {
        if (string.IsNullOrEmpty(sceneName)) { GameEvents.ShowToast(closedMessage); return; }
        SceneExit.NextSpawnId = arriveAt;
        SceneManager.LoadScene(sceneName);
    }
}
