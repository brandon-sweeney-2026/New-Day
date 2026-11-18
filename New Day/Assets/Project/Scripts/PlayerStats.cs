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

    [Header("Knockback when hurt")]
    [Tooltip("How far (world units = tiles) a normal hit shoves you back.")]
    [Min(0f)] [SerializeField] private float knockbackDistance = 1.2f;
    [Tooltip("How long the shove takes (seconds). Shorter = snappier.")]
    [Range(0.05f, 0.6f)] [SerializeField] private float knockbackSeconds = 0.18f;
    [Tooltip("Seconds you can't walk after a hit, so the shove can't be cancelled by holding a key.")]
    [Range(0f, 0.6f)] [SerializeField] private float hitStunSeconds = 0.2f;
    [Tooltip("An enemy's Contact Knockback of this size gives exactly Knockback Distance. Stronger enemies (the golem uses 9) push farther.")]
    [Min(0.1f)] [SerializeField] private float normalKnockbackForce = 6f;
    [Tooltip("The shove is never more than this many times Knockback Distance.")]
    [Range(1f, 4f)] [SerializeField] private float maxKnockbackScale = 2f;

    [Header("Feedback")]
    [SerializeField] private SpriteRenderer flashRenderer;
    [SerializeField] private Color hurtColor = new Color(1f, 0.4f, 0.4f);
    [Tooltip("Blink while you can't be hurt, so you can see it.")]
    [SerializeField] private bool blinkWhileInvulnerable = true;
    [Tooltip("Blinks per second.")]
    [Range(2f, 20f)] [SerializeField] private float blinkRate = 10f;
    [Tooltip("How much the screen shakes when you are hit (world units; 0 = no shake).")]
    [Range(0f, 0.3f)] [SerializeField] private float hitShake = 0.07f;

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

    // The Inspector values before any perk or equipment bonus is added on top.
    private int baseMaxHealth;
    private float baseMaxMagic;

    /// <summary>
    /// Sets the maximums to the Inspector values plus the bonuses from perks and worn equipment (see PlayerBonuses).
    /// Runs at the start and again whenever a perk is bought or equipment changes.
    /// </summary>
    private void ApplyBonuses()
    {
        maxHealth = Mathf.Max(1, baseMaxHealth + PlayerBonuses.MaxHealth);
        maxMagic = Mathf.Max(0f, baseMaxMagic + PlayerBonuses.MaxMagic);
        // Taking a hat off must not leave you with more health than the new maximum.
        Health = Mathf.Min(Health, maxHealth);
        Magic = Mathf.Min(Magic, maxMagic);
        if (OnChanged != null) OnChanged();
    }

    private void Awake()
    {
        Instance = this;
        baseMaxHealth = maxHealth;
        baseMaxMagic = maxMagic;
        maxHealth = Mathf.Max(1, baseMaxHealth + PlayerBonuses.MaxHealth);
        maxMagic = Mathf.Max(0f, baseMaxMagic + PlayerBonuses.MaxMagic);
        PlayerBonuses.OnChanged += ApplyBonuses;
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
        PlayerBonuses.OnChanged -= ApplyBonuses;
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
        {
            // first half of the safe time: red. Second half: blinking see-through.
            float left = invulnerableUntil - Time.time;
            Color c = baseColor;
            if (left > invulnerableTime * 0.5f) c = hurtColor;
            else if (left > 0f && blinkWhileInvulnerable && Mathf.FloorToInt(left * blinkRate * 2f) % 2 == 0) c.a = baseColor.a * 0.35f;
            flashRenderer.color = c;
        }

        if (changed && OnChanged != null) OnChanged();
    }

    // ------------------------------------------------------------------
    // Damage and healing
    // ------------------------------------------------------------------

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || Time.time < invulnerableUntil) return;

        invulnerableUntil = Time.time + invulnerableTime;
        // Defense (from perks and equipment) takes a little off every hit.
        Health = Mathf.Max(0f, Health - PlayerBonuses.ReduceDamage(info.amount));

        // Shove the player away from what hit them. The hit's knockback sets the direction, and its size scales the distance.
        if (controller != null && info.knockback.sqrMagnitude > 0.01f)
        {
            float scale = Mathf.Min(maxKnockbackScale, info.knockback.magnitude / normalKnockbackForce);
            controller.HitKnockback(info.knockback, knockbackDistance * scale, knockbackSeconds, hitStunSeconds);
        }
        if (hitShake > 0f) CameraFollow2D.Shake(hitShake, 0.15f);
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
