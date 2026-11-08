using UnityEngine;

/// <summary>Added automatically by SceneExit when 'Require Interact' is ticked. Lets the action button use the door.</summary>
public class SceneExitInteract : MonoBehaviour, IInteractable
{
    public string InteractPrompt
    {
        get { var e = GetComponent<SceneExit>(); return e != null ? e.PromptText : "Travel"; }
    }

    public void Interact(PlayerActions player)
    {
        var e = GetComponent<SceneExit>();
        if (e != null) e.Go();
    }
}
