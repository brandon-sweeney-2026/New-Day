using System;
using UnityEngine;

/// <summary>
/// The player's health and magic. Put this on the Player.
///
///  - Enemies (and anything else) hurt the player through IDamageable.TakeDamage().
///  - Spells spend magic with TrySpendMagic(); magic slowly refills by itself.
///  - Dying passes you out: a new day starts and you wake up at the bed (with some health back).
/// </summary>
public class PlayerStats : MonoBehaviour, IDamageable
{
    public static PlayerStats Instance { get; private set; }

    [Header("Health")]
    [Min(1)] public int maxHealth = 100;
    [Tooltip("Seconds of invulnerability after being hit.")]
    [SerializeField] private float invulnerableTime = 0.6f;
    [Tooltip("Health regained per second (0 = none).")]
    [SerializeField] private float healthRegenPerSecond = 0f;

    [Header("Magic")]
    [Min(0)] public float maxMagic = 50f;
    [Tooltip("Magic regained per second.")]
    [SerializeField] private float magicRegenPerSecond = 2f;
    [Tooltip("Seconds after casting before magic starts refilling.")]
    [SerializeField] private float magicRegenDelay = 1.5f;

    [Header("New day")]
    [Tooltip("Fraction of health and magic restored after a normal night's sleep (0-1).")]
    [Range(0f, 1f)] [SerializeField] private float sleepRestore = 1f;
    [Tooltip("Fraction restored if you passed out or died (0-1).")]
    [Range(0f, 1f)] [SerializeField] private float passOutRestore = 0.5f;
    [Tooltip("Gold lost when you die (0-1 of your gold).")]
    [Range(0f, 1f)] [SerializeField] private float goldLostOnDeath = 0.1f;

    [Header("Feedback")]
    [SerializeField] private SpriteRenderer flashRenderer;
    [SerializeField] private Color hurtColor = new Color(1f, 0.4f, 0.4f);

    public float Health { get; private set; }
    public float Magic { get; private set; }
    public bool IsDead { get; private set; }

    public Team DamageTeam { get { return Team.Player; } }

    /// <summary>Raised whenever health or magic changes.</summary>
    public event Action OnChanged;
    public event Action OnDied;
    public event Action<DamageInfo> OnDamaged;

    private float invulnerableUntil;
    private float magicRegenAt;
    private PlayerController controller;
    private Color baseColor = Color.white;

    private void Awake()
    {
        Instance = this;
        Health = maxHealth;
        Magic = maxMagic;
        controller = GetComponent<PlayerController>();
        if (flashRenderer == null) flashRenderer = GetComponentInChildren<SpriteRenderer>();
        if (flashRenderer != null) baseColor = flashRenderer.color;
    }

    private void Start()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnNewDay += HandleNewDay;
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.OnNewDay -= HandleNewDay;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        bool changed = false;

        if (!IsDead && healthRegenPerSecond > 0f && Health < maxHealth)
        {
            Health = Mathf.Min(maxHealth, Health + healthRegenPerSecond * Time.deltaTime);
            changed = true;
        }

        if (Time.time >= magicRegenAt && Magic < maxMagic)
        {
            Magic = Mathf.Min(maxMagic, Magic + magicRegenPerSecond * Time.deltaTime);
            changed = true;
        }

        if (flashRenderer != null)
            flashRenderer.color = Time.time < invulnerableUntil - invulnerableTime * 0.5f ? hurtColor : baseColor;

        if (changed && OnChanged != null) OnChanged();
    }

    // ------------------------------------------------------------------
    // Damage and healing
    // ------------------------------------------------------------------

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || Time.time < invulnerableUntil) return;

        invulnerableUntil = Time.time + invulnerableTime;
        Health = Mathf.Max(0f, Health - info.amount);

        if (controller != null && info.knockback.sqrMagnitude > 0.01f) controller.Knockback(info.knockback);
        if (OnDamaged != null) OnDamaged(info);
        if (OnChanged != null) OnChanged();

        if (Health <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Health = Mathf.Min(maxHealth, Health + amount);
        if (OnChanged != null) OnChanged();
    }

    // ------------------------------------------------------------------
    // Magic
    // ------------------------------------------------------------------

    public bool TrySpendMagic(float cost)
    {
        if (Magic < cost) return false;
        Magic -= cost;
        magicRegenAt = Time.time + magicRegenDelay;
        if (OnChanged != null) OnChanged();
        return true;
    }

    public void RestoreMagic(float amount)
    {
        Magic = Mathf.Min(maxMagic, Magic + amount);
        if (OnChanged != null) OnChanged();
    }

    // ------------------------------------------------------------------
    // Dying and new days
    // ------------------------------------------------------------------

    private void Die()
    {
        IsDead = true;
        if (OnDied != null) OnDied();

        int lost = Mathf.RoundToInt(GameState.Gold * goldLostOnDeath);
        if (lost > 0) GameState.AddGold(-lost);
        GameEvents.ShowToast(lost > 0 ? "You were knocked out and lost " + lost + "g." : "You were knocked out.");

        if (TimeManager.Instance != null) TimeManager.Instance.StartNewDay(true);
        else HandleNewDay();
    }

    private void HandleNewDay()
    {
        bool passedOut = IsDead || (TimeManager.Instance != null && TimeManager.Instance.LastDayWasPassOut);
        float fraction = passedOut ? passOutRestore : sleepRestore;

        Health = Mathf.Max(Health, Mathf.Max(1f, maxHealth * fraction));
        Magic = Mathf.Max(Magic, maxMagic * fraction);
        IsDead = false;
        invulnerableUntil = 0f;

        // Wake up in bed.
        if (Bed.Main != null) Bed.Main.PlaceAtWakePoint(transform);

        if (OnChanged != null) OnChanged();
    }
}
