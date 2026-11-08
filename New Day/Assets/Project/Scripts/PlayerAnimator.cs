using UnityEngine;

/// <summary>
/// Feeds the player's movement to the Animator so walk animations play in the right direction.
/// Put this on the Player. The Animator needs these parameters (names must match exactly):
///   MoveX (Float), MoveY (Float), IsMoving (Bool)
/// MoveX/MoveY always hold the direction the player is FACING, so the idle pose faces the right way too.
/// Parameters the Animator doesn't have are skipped, so it is safe to add them one at a time.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimator : MonoBehaviour
{
    [Tooltip("Found automatically if left empty.")]
    [SerializeField] private Animator animator;

    private PlayerController player;
    private bool hasX, hasY, hasMoving;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == MoveX) hasX = true;
            else if (p.nameHash == MoveY) hasY = true;
            else if (p.nameHash == IsMoving) hasMoving = true;
        }
    }

    private void Update()
    {
        if (animator == null) return;

        Vector2 facing = player.FacingDirection;
        if (hasX) animator.SetFloat(MoveX, facing.x);
        if (hasY) animator.SetFloat(MoveY, facing.y);
        if (hasMoving) animator.SetBool(IsMoving, player.IsMoving);
    }
}
