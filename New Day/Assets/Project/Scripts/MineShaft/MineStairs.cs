using UnityEngine;

/// <summary>
/// Stairs or a ladder in the mine shaft. Stand on it and press the action button.
///  - Down One Floor: the staircase. Goes from the hub to floor 1, or from floor N to floor N + 1.
///  - Up To Hub: the ladder where the player arrives on a floor. Climbs straight back to the hub (the safe room at the top).
/// Needs a trigger Collider2D (the setup menu adds one).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MineStairs : MonoBehaviour, IInteractable
{
    public enum Direction { DownOneFloor, UpToHub }

    [Tooltip("Down One Floor = the staircase. Up To Hub = the ladder back to the mine entrance.")]
    public Direction direction = Direction.DownOneFloor;
    public string downPrompt = "Go down to floor {0}";
    public string upPrompt = "Climb back up to the mine entrance";

    public string InteractPrompt
    {
        get { return direction == Direction.DownOneFloor ? string.Format(downPrompt, MineShaftManager.CurrentFloor + 1) : upPrompt; }
    }

    public void Interact(PlayerActions player)
    {
        MineShaftManager mine = MineShaftManager.Instance;
        if (mine == null) { GameEvents.ShowToast("These stairs only work in the mine shaft scene."); return; }
        mine.GoToFloor(direction == Direction.DownOneFloor ? MineShaftManager.CurrentFloor + 1 : 0);
    }
}
