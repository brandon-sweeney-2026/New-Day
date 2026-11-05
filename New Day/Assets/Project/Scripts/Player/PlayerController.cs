using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Top-down player movement (WASD / arrow keys / gamepad stick).
/// Attach to your Player GameObject along with a Rigidbody2D and a Collider2D.
///
/// UPDATED: added CanMove so other scripts (like PlayerAttack) can freeze movement during a swing.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;

    private Rigidbody2D rb;
    private InputAction moveAction;
    private Vector2 moveInput;

    /// <summary>Set to false to freeze the player (swinging a sword, in a menu, etc.).</summary>
    public bool CanMove { get; set; } = true;

    /// <summary>Last cardinal direction the player moved in. Tools use this to pick the target tile.</summary>
    public Vector2 FacingDirection { get; private set; } = Vector2.down;

    /// <summary>True while the player is moving. Handy for animation later.</summary>
    public bool IsMoving => moveInput.sqrMagnitude > 0.01f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Top-down game: no gravity, and don't let physics spin the player.
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        // Build the input action in code so there's no Input Actions asset to set up.
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");
    }

    private void OnEnable() => moveAction.Enable();
    private void OnDisable() => moveAction.Disable();

    private void Update()
    {
        // While frozen, ignore input entirely so the facing direction can't change mid-swing.
        if (!CanMove)
        {
            moveInput = Vector2.zero;
            return;
        }

        // ClampMagnitude stops diagonal movement from being faster than straight movement.
        moveInput = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);

        if (IsMoving)
        {
            // Snap facing to one of four directions (up/down/left/right).
            if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
                FacingDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
            else
                FacingDirection = new Vector2(0f, Mathf.Sign(moveInput.y));
        }
    }

    private void FixedUpdate()
    {
        // MovePosition respects colliders and works in every Unity version.
        rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }
}
