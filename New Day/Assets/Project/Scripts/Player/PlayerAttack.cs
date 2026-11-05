using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plays the sword swing animation and (optionally) hits things in front of the player.
/// Attach to the Player, next to PlayerController. The Player also needs an Animator
/// with an "Attack" trigger parameter (see SWORD_ANIMATION steps).
///
/// Default controls: Left Mouse Button or J.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerAttack : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Animator on the player. Found automatically if left empty.")]
    [SerializeField] private Animator animator;

    [Header("Timing")]
    [Tooltip("Must match your animation clip: frames / sample rate. 13 frames at 12 fps = about 1.08 seconds.")]
    [SerializeField] private float swingDuration = 13f / 12f;
    [Tooltip("Extra pause after the swing before you can swing again.")]
    [SerializeField] private float cooldown = 0.1f;
    [Tooltip("If true, the player can't walk while swinging.")]
    [SerializeField] private bool freezeMovementWhileSwinging = true;

    [Header("Hit Detection")]
    [Tooltip("Layers the sword can hit (create an 'Enemy' layer later).")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [Tooltip("How far in front of the player the hit area is centered.")]
    [SerializeField] private float hitDistance = 0.8f;
    [SerializeField] private float hitRadius = 0.6f;
    [SerializeField] private int damage = 1;

    private PlayerController player;
    private bool isSwinging;
    private float nextSwingTime;

    private static readonly int AttackTrigger = Animator.StringToHash("Attack");

    public bool IsSwinging => isSwinging;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (isSwinging || Time.time < nextSwingTime) return;

        bool pressed =
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame);

        if (pressed) StartCoroutine(Swing());
    }

    private IEnumerator Swing()
    {
        isSwinging = true;
        if (freezeMovementWhileSwinging) player.CanMove = false;

        if (animator != null) animator.SetTrigger(AttackTrigger);

        yield return new WaitForSeconds(swingDuration);

        if (freezeMovementWhileSwinging) player.CanMove = true;
        isSwinging = false;
        nextSwingTime = Time.time + cooldown;
    }

    /// <summary>
    /// Call this from an Animation Event on the frame where the blade should connect
    /// (usually the frame where the sword is fully extended). Without an event, nothing gets hit.
    /// </summary>
    public void SwordHit()
    {
        Vector2 center = (Vector2)transform.position + player.FacingDirection * hitDistance;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, hitRadius, hitLayers);

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue; // don't hit yourself

            // Any script on the target with a method called TakeHit(int) will receive the damage.
            // Replace this with your own health system later.
            hit.SendMessage("TakeHit", damage, SendMessageOptions.DontRequireReceiver);
        }
    }

    // Draws the hit area in the Scene view when the player is selected.
    private void OnDrawGizmosSelected()
    {
        var p = GetComponent<PlayerController>();
        Vector2 facing = p != null ? p.FacingDirection : Vector2.down;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere((Vector2)transform.position + facing * hitDistance, hitRadius);
    }
}
