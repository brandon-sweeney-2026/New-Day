using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lets the player use farming tools on the tile they are facing.
/// Attach to the Player (next to PlayerController) and assign the FarmManager.
///
/// Default controls (all editable in the Inspector / the code below):
///   1 = Hoe, 2 = Watering Can, Space or E = use the selected tool.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class FarmToolController : MonoBehaviour
{
    public enum Tool { Hoe, WateringCan }

    [Header("References")]
    [SerializeField] private FarmManager farm;
    [Tooltip("Optional: a SpriteRenderer (e.g. a square outline) that shows which tile you're targeting.")]
    [SerializeField] private SpriteRenderer targetHighlight;

    [Header("Tool Settings")]
    [SerializeField] private Tool currentTool = Tool.Hoe;
    [Tooltip("How far in front of the player (in world units) the target tile is.")]
    [SerializeField] private float reachDistance = 0.9f;
    [Tooltip("Offset from the player's pivot. Raise this if your pivot is at the feet.")]
    [SerializeField] private Vector2 originOffset = Vector2.zero;
    [Tooltip("Seconds between tool uses, so holding the key doesn't spam.")]
    [SerializeField] private float useCooldown = 0.25f;

    [Header("Highlight Colors")]
    [SerializeField] private Color validColor = new Color(1f, 1f, 1f, 0.6f);

    private PlayerController player;
    private float nextUseTime;
    private Vector3Int targetCell;

    /// <summary>The tool the player currently has selected. Your hotbar UI can read this later.</summary>
    public Tool CurrentTool => currentTool;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (farm == null) return;

        UpdateTargetCell();
        HandleToolSwitching();
        HandleToolUse();
    }

    private void UpdateTargetCell()
    {
        Vector3 origin = transform.position + (Vector3)originOffset;
        Vector3 targetPos = origin + (Vector3)(player.FacingDirection * reachDistance);
        targetCell = farm.WorldToCell(targetPos);

        if (targetHighlight != null)
        {
            targetHighlight.transform.position = farm.CellToWorldCenter(targetCell);
            targetHighlight.color = validColor;
        }
    }

    private void HandleToolSwitching()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Add more tools by extending the Tool enum and adding keys here.
        if (kb.digit1Key.wasPressedThisFrame) currentTool = Tool.Hoe;
        if (kb.digit2Key.wasPressedThisFrame) currentTool = Tool.WateringCan;
    }

    private void HandleToolUse()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool usePressed = kb.spaceKey.isPressed || kb.eKey.isPressed;
        if (!usePressed || Time.time < nextUseTime) return;

        bool changed = false;

        switch (currentTool)
        {
            case Tool.Hoe:
                changed = farm.TryTill(targetCell);
                break;
            case Tool.WateringCan:
                changed = farm.TryWater(targetCell);
                break;
        }

        // Cooldown applies whether or not it worked, so mashing doesn't feel jittery.
        nextUseTime = Time.time + useCooldown;

        if (changed)
        {
            // Hook for juice later: play a sound, spawn a dust particle, trigger an animation.
            // Example: AudioSource.PlayClipAtPoint(tillSound, farm.CellToWorldCenter(targetCell));
        }
    }
}
