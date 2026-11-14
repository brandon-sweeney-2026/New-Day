using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The fighting styles. Put this on the Player (Farm > Combat > Set Up Weapons does it). PlayerActions hands any Weapon whose
/// "Weapon Kind" is not Generic over to this script when you press the action button.
///
/// Each weapon has its OWN player animation (the pictures come from player_weapon_sheet.png, the timings and hand positions from PlayerWeaponFrames):
///   SWORD   quick and light. The arm snaps back and sweeps through; alternates direction. A whitish-blue after-image follows the blade.
///   DAGGER  a fast straight poke. Dark shadow frames of the player trail behind the lunge so it feels quick.
///   FLAIL   the player heaves the ball back, then turns with it as it circles (1.5 tiles out), slow at first and faster the longer you HOLD.
///           You walk slowly, lean against the pull, kick up dust and shake the screen when it is fast, and are winded afterwards.
///   HAMMER  two hands, lifted slowly over the head (it strains), a heavy slam. Bits of the ground you hit fly up in that ground's colours.
///   BOW     HOLD to draw: the arms really pull the string back (5 steps). RELEASE to shoot. One arrow per shot. Walk slowly while drawing.
///
/// What each weapon does comes from its ItemData (damage, crit chance, swing speed, flail radius, hammer splash, bow draw time...).
/// The numbers under "Feel" are for how strong the effects are. Everything is in the Inspector.
/// </summary>
[RequireComponent(typeof(PlayerActions))]
public class WeaponController : MonoBehaviour
{
    [Header("Animation pictures (set by Farm > Combat > Set Up Weapons)")]
    [SerializeField] private PlayerWeaponAnimSet animSet;

    [Header("Feel")]
    [Tooltip("The tiny freeze when a hit lands (seconds). 0 = off.")]
    [SerializeField] private float hitStop = 0.045f;
    [SerializeField] private bool screenShake = true;
    [SerializeField] private bool damageNumbers = true;
    [Tooltip("After-images, shadow frames, dust and sparks.")]
    [SerializeField] private bool effects = true;

    [Header("Sword")]
    [Tooltip("The glowing after-image that follows the blade.")]
    [SerializeField] private Color swordGlow = new Color(0.72f, 0.9f, 1f, 0.75f);
    [SerializeField] private float swordLunge = 2.6f;

    [Header("Dagger")]
    [Tooltip("The shadow frames left behind by the lunge.")]
    [SerializeField] private Color daggerShadow = new Color(0.1f, 0.1f, 0.25f, 0.6f);
    [SerializeField] private float daggerLunge = 3.4f;
    [Tooltip("Seconds between shadow frames.")]
    [SerializeField] private float shadowInterval = 0.022f;

    [Header("Flail")]
    [Tooltip("Spins at least this many degrees even if you tap the button.")]
    [SerializeField] private float flailMinDegrees = 360f;
    [Tooltip("How hard the screen rumbles at full speed (0 = none).")]
    [SerializeField] private float flailRumble = 0.02f;

    [Header("Hammer")]
    [SerializeField] private float hammerShake = 0.14f;
    [Tooltip("Pieces of ground thrown up on the slam.")]
    [SerializeField] private int groundChunks = 14;
    [SerializeField] private float groundPower = 1.0f;

    [Header("Bow")]
    [Tooltip("Letting go before this much of the draw (0-1) cancels the shot and keeps your arrow.")]
    [SerializeField, Range(0f, 1f)] private float bowMinCharge = 0.2f;

    public bool IsUsing { get; private set; }

    private PlayerActions actions;
    private PlayerController controller;
    private PlayerStats stats;
    private Inventory inventory;
    private SpriteRenderer bodyRenderer;
    private Sprite overrideBody;

    private Transform fx;
    private SpriteRenderer weaponSr, ballSr, bowSr;
    private readonly List<SpriteRenderer> links = new List<SpriteRenderer>();
    private readonly HashSet<IDamageable> hitSet = new HashSet<IDamageable>();
    private readonly Dictionary<IDamageable, float> hitCooldown = new Dictionary<IDamageable, float>();
    private float swordSide = 1f;
    private bool hitStopping;
    private float nextGhost;
    private PWFrame lastFrame;

    // ------------------------------------------------------------------ setup
    private void Awake()
    {
        actions = GetComponent<PlayerActions>();
        controller = GetComponent<PlayerController>();
        stats = GetComponent<PlayerStats>();
        inventory = GetComponent<Inventory>();

        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(true))
        {
            string n = r.gameObject.name;
            if (n.StartsWith("Held") || n.StartsWith("Weapon") || n.Contains("Shadow") || n.StartsWith("Target")) continue;
            bodyRenderer = r; break;
        }

        var go = new GameObject("WeaponFX");
        go.transform.SetParent(transform, false);
        fx = go.transform;
        weaponSr = MakeRenderer("WeaponSprite");
        ballSr = MakeRenderer("WeaponBall");
        bowSr = MakeRenderer("WeaponBow");
    }

    private SpriteRenderer MakeRenderer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(fx, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.enabled = false;
        go.AddComponent<LightingExempt>();
        return sr;
    }

    private void OnDisable() { ResetEverything(); }

    private void ResetEverything()
    {
        StopAllCoroutines();
        if (hitStopping) { Time.timeScale = 1f; hitStopping = false; }
        if (actions != null && IsUsing) actions.SetBusy(false);
        IsUsing = false;
        overrideBody = null;
        if (controller != null) { controller.CanMove = true; controller.SpeedMultiplier = 1f; }
        HideAll();
    }

    private void HideAll()
    {
        if (weaponSr != null) weaponSr.enabled = false;
        if (ballSr != null) ballSr.enabled = false;
        if (bowSr != null) bowSr.enabled = false;
        foreach (SpriteRenderer l in links) if (l != null) l.enabled = false;
    }

    private void LateUpdate()
    {
        // while a weapon is in use the player's body picture comes from the weapon animation (this runs after the Animator)
        if (IsUsing && overrideBody != null && bodyRenderer != null) bodyRenderer.sprite = overrideBody;
    }

    // ------------------------------------------------------------------ entry
    /// <summary>Called by PlayerActions when the action button is pressed with one of the new weapons equipped.</summary>
    public void Begin(ItemData w)
    {
        if (IsUsing || w == null) return;
        if (animSet == null)
        {
            GameEvents.ShowToast("Run Farm > Combat > Set Up Weapons first (the weapon animations are missing).");
            return;
        }
        switch (w.weaponKind)
        {
            case ItemData.WeaponKind.Sword: StartCoroutine(SwordRoutine(w)); break;
            case ItemData.WeaponKind.Dagger: StartCoroutine(DaggerRoutine(w)); break;
            case ItemData.WeaponKind.Flail: StartCoroutine(FlailRoutine(w)); break;
            case ItemData.WeaponKind.Hammer: StartCoroutine(HammerRoutine(w)); break;
            case ItemData.WeaponKind.Bow: StartCoroutine(BowRoutine(w)); break;
        }
    }

    private void Enter(bool freezeMove)
    {
        IsUsing = true;
        actions.SetBusy(true);
        controller.CanMove = !freezeMove;
        hitSet.Clear();
        hitCooldown.Clear();
    }

    private void Leave(float cooldown)
    {
        IsUsing = false;
        overrideBody = null;
        actions.SetBusy(false);
        controller.CanMove = true;
        controller.SpeedMultiplier = 1f;
        HideAll();
        actions.SetCooldown(cooldown);
    }

    private bool ShouldStop(ItemData w)
    {
        if (stats != null && stats.IsDead) return true;
        if (GameState.InputLocked) return true;
        return inventory != null && inventory.EquippedItem != w;
    }

    // ------------------------------------------------------------------ frames
    private static int FacingIndex(Vector2 f)
    {
        if (Mathf.Abs(f.x) > Mathf.Abs(f.y)) return f.x > 0f ? 3 : 2;
        return f.y > 0f ? 1 : 0;
    }

    private PlayerWeaponFrames.Row RowOf(string anim) { return PlayerWeaponFrames.Get(anim, FacingIndex(actions.Facing)); }

    private PWFrame FrameOf(string anim, int idx)
    {
        PlayerWeaponFrames.Row r = RowOf(anim);
        return r.frames[Mathf.Clamp(idx, 0, r.frames.Length - 1)];
    }

    /// <summary>Where the hand (the weapon's grip) is in the world for a frame.</summary>
    private Vector2 HandWorld(PWFrame fr)
    {
        Vector2 feet = bodyRenderer != null ? (Vector2)bodyRenderer.transform.position : (Vector2)transform.position;
        return feet + new Vector2((fr.hx - PlayerWeaponFrames.CellWidth * 0.5f) / 16f, (PlayerWeaponFrames.CellHeight - fr.hy) / 16f);
    }

    private int BodyOrder { get { return bodyRenderer != null ? bodyRenderer.sortingOrder : 10; } }
    private int SortingLayer() { return bodyRenderer != null ? bodyRenderer.sortingLayerID : 0; }

    /// <summary>Shows one frame: the body picture, and the weapon in the hand at the angle the frame says (or 'angle' if given).</summary>
    private PWFrame Show(string anim, int idx, Sprite weapon, float? angle = null, Vector2 jitter = default(Vector2), float alpha = 1f)
    {
        PlayerWeaponFrames.Row row = RowOf(anim);
        PWFrame fr = row.frames[Mathf.Clamp(idx, 0, row.frames.Length - 1)];
        lastFrame = fr;
        overrideBody = animSet.Get(row.row, fr.col);
        if (weapon != null && !fr.hidden)
        {
            float a = angle.HasValue ? angle.Value : fr.ang;
            weaponSr.enabled = true;
            weaponSr.sprite = weapon;
            weaponSr.color = new Color(1f, 1f, 1f, alpha);
            weaponSr.transform.position = HandWorld(fr) + jitter;
            weaponSr.transform.rotation = Quaternion.Euler(0f, 0f, a - 45f);
            weaponSr.transform.localScale = Vector3.one;
            weaponSr.sortingOrder = BodyOrder + (fr.behind ? -1 : 1);
            weaponSr.sortingLayerID = SortingLayer();
        }
        else weaponSr.enabled = false;
        return fr;
    }

    /// <summary>Holds a frame for its time (divided by 'speed'), calling 'each' every update with 0..1 progress.</summary>
    private IEnumerator Hold(string anim, int idx, Sprite weapon, float speed, System.Action<float> each = null, System.Func<float, float?> angle = null, float fadeFrom = 2f)
    {
        float dur = Mathf.Max(0.01f, FrameOf(anim, idx).dur / Mathf.Max(0.1f, speed));
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float u = t / dur;
            float al = u > fadeFrom ? Mathf.Clamp01(1f - (u - fadeFrom) / (1f - fadeFrom)) : 1f;
            Show(anim, idx, weapon, angle != null ? angle(u) : (float?)null, Vector2.zero, al);
            if (each != null) each(u);
            yield return null;
        }
    }

    private static Sprite HeldPic(ItemData w) { return w.heldSprite != null ? w.heldSprite : w.icon; }

    // ------------------------------------------------------------------ SWORD: quick and light, whitish-blue after-image
    private IEnumerator SwordRoutine(ItemData w)
    {
        Enter(true);
        float sp = Mathf.Max(0.1f, w.swingSpeed);
        string anim = swordSide > 0f ? "sword_a" : "sword_b";
        float side = swordSide; swordSide = -swordSide;                          // alternate: right-to-left, then left-to-right
        Sprite pic = HeldPic(w);
        Vector2 f = actions.Facing;
        float fa = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;

        yield return Hold(anim, 0, pic, sp);
        yield return Hold(anim, 1, pic, sp);

        controller.Knockback(f * swordLunge);
        float prev = FrameOf(anim, 1).ang;
        SlashArc(fa, w.attackRange + 0.35f, side);
        for (int i = 2; i <= 4; i++)
        {
            float from = prev, to = FrameOf(anim, i).ang;
            float last = from;
            yield return Hold(anim, i, pic, sp, u =>
            {
                float a = Mathf.Lerp(from, to, u);
                HitAlong(w, last, a, w.attackRange, w.attackRadius, 1f, false);
                SwordTrail(pic, last, a);
                last = a;
            }, u => Mathf.Lerp(from, to, u));
            prev = to;
        }
        yield return Hold(anim, 5, pic, sp);
        yield return Hold(anim, 6, pic, sp, null, null, 0.3f);
        Leave(w.attackCooldown / sp);
    }

    /// <summary>The whitish-blue after-image: a fan of glowing blade copies that fade, plus a few sparkles off the tip.</summary>
    private void SwordTrail(Sprite pic, float from, float to)
    {
        if (!effects || pic == null) return;
        Sprite white = CombatFx.White(pic);
        Vector2 pos = weaponSr.transform.position;
        for (int k = 0; k < 3; k++)
        {
            float a = Mathf.Lerp(from, to, k / 3f);
            Color c = swordGlow; c.a = swordGlow.a * (0.45f + 0.18f * k);
            CombatFx.Spawn(white, pos, a - 45f, Vector2.one, Vector2.one * 1.04f, c, 0.22f, BodyOrder - 1 + (weaponSr.sortingOrder > BodyOrder ? 2 : 0), SortingLayer());
        }
        Vector2 tip = pos + Dir(to) * 1.35f;
        if (Random.value < 0.8f)
        {
            Color sc = new Color(0.85f, 0.96f, 1f, 0.9f);
            var spark = CombatFx.Spawn(RuntimeSprites.Square, tip + Random.insideUnitCircle * 0.1f, Random.Range(0f, 90f), Vector2.one * 0.09f, Vector2.one * 0.02f, sc, 0.28f, BodyOrder + 3, SortingLayer());
            spark.drift = -Dir(to) * 0.5f + Random.insideUnitCircle * 0.5f;
        }
    }

    // ------------------------------------------------------------------ DAGGER: fast poke with shadow frames
    private IEnumerator DaggerRoutine(ItemData w)
    {
        Enter(true);
        float sp = Mathf.Max(0.1f, w.swingSpeed);
        Sprite pic = HeldPic(w);
        Vector2 f = actions.Facing;
        float fa = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;
        const string anim = "dagger";

        yield return Hold(anim, 0, pic, sp);
        yield return Hold(anim, 1, pic, sp);                                    // coil back

        controller.Knockback(f * daggerLunge);
        if (effects) Streak(fa, w.attackRange);
        nextGhost = 0f;
        System.Action<float> thrust = u =>
        {
            HitPoint(w, actions.HandOrigin + f * w.attackRange, w.attackRadius, 1f, false);
            ShadowFrame();
        };
        yield return Hold(anim, 2, pic, sp, thrust);
        yield return Hold(anim, 3, pic, sp, thrust);
        yield return Hold(anim, 4, pic, sp, u => { HitPoint(w, actions.HandOrigin + f * w.attackRange, w.attackRadius, 1f, false); if (u < 0.5f) ShadowFrame(); });
        yield return Hold(anim, 5, pic, sp, null, null, 0.3f);
        Leave(w.attackCooldown / sp);
    }

    /// <summary>Leaves a dark silhouette of the player (and the dagger) where they were - the faster you move, the longer the trail.</summary>
    private void ShadowFrame()
    {
        if (!effects || Time.time < nextGhost || bodyRenderer == null) return;
        nextGhost = Time.time + shadowInterval;
        Sprite body = bodyRenderer.sprite;
        if (body != null)
            CombatFx.Spawn(CombatFx.White(body), bodyRenderer.transform.position, 0f, Vector2.one, Vector2.one, daggerShadow, 0.24f, BodyOrder - 1, SortingLayer());
        if (weaponSr.enabled && weaponSr.sprite != null)
            CombatFx.Spawn(CombatFx.White(weaponSr.sprite), weaponSr.transform.position, weaponSr.transform.eulerAngles.z, Vector2.one, Vector2.one, daggerShadow, 0.2f, BodyOrder - 1, SortingLayer());
    }

    // ------------------------------------------------------------------ FLAIL: heavy, spools up
    private IEnumerator FlailRoutine(ItemData w)
    {
        Enter(false);
        controller.SpeedMultiplier = Mathf.Max(0.1f, w.moveSpeedWhileUsing);
        Vector2 f = actions.Facing;
        float ang = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg + 180f;                // the ball starts behind
        Sprite handle = HeldPic(w);
        Sprite ball = w.ballSprite != null ? w.ballSprite : RuntimeSprites.Circle;
        float radius = Mathf.Max(0.8f, w.flailRadius);
        ballSr.sprite = ball;

        // wind-up: heave the ball back (slow, it's heavy)
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            yield return Hold("flail_wind", idx, handle, 1f, u => DrawFlail(ang, Mathf.Lerp(0.7f, 1.0f, (idx + u) / 3f), 0f), u => ang);
        }

        // spin: starts slowly, picks up speed the longer you hold; the body turns with the ball
        float spinT = 0f, traveled = 0f, omega = w.flailStartSpin;
        float maxT = Mathf.Max(0.5f, w.flailMaxSeconds);
        float nextScuff = 0f, nextRumble = 0f;
        while (true)
        {
            if (ShouldStop(w)) break;
            bool wantMore = actions.ActionHeld && spinT < maxT;
            if (!wantMore && traveled >= flailMinDegrees) break;
            float k = Mathf.Clamp01(spinT / Mathf.Max(0.2f, w.flailSpinUpTime));
            omega = Mathf.Lerp(w.flailStartSpin, w.flailMaxSpin, k * k);
            float step = omega * Time.deltaTime;
            ang -= step; traveled += step; spinT += Time.deltaTime;
            float r = Mathf.Lerp(1.0f, radius, Mathf.Clamp01(spinT / 0.6f));

            int frameIdx = Mathf.RoundToInt(Mathf.Repeat(ang, 360f) / 45f) % 8;          // which of the 8 turning poses
            Show("flail_spin", frameIdx, handle, ang);
            Vector2 bp = DrawFlail(ang, r, k);
            if (effects && k > 0.25f && Time.frameCount % 2 == 0) GhostBall(ball, bp);
            if (effects && k > 0.45f && Time.time >= nextScuff)
            {
                nextScuff = Time.time + Mathf.Lerp(0.22f, 0.08f, k);
                GroundSampler.Scuff((Vector2)bodyRenderer.transform.position + new Vector2(0f, 0.05f), BodyOrder - 1, SortingLayer());
            }
            if (screenShake && flailRumble > 0f && k > 0.55f && Time.time >= nextRumble)
            {
                nextRumble = Time.time + 0.1f;
                CameraFollow2D.Shake(flailRumble * k, 0.12f);
            }
            HitBall(w, bp, k);
            yield return null;
        }

        // wind down: it slows, sags and drops
        float startOmega = omega;
        for (float t = 0f; t < 0.55f; t += Time.deltaTime)
        {
            float u = t / 0.55f;
            omega = Mathf.Lerp(startOmega, 0f, 1f - (1f - u) * (1f - u));
            ang -= omega * Time.deltaTime;
            int frameIdx = Mathf.RoundToInt(Mathf.Repeat(ang, 360f) / 45f) % 8;
            Show("flail_spin", frameIdx, handle, ang);
            Vector2 bp = DrawFlail(ang, Mathf.Lerp(radius, 0.8f, u), 0.3f * (1f - u));
            HitBall(w, bp, 0.2f);
            yield return null;
        }

        // winded: the player stops moving and catches their breath
        controller.SpeedMultiplier = 1f;
        controller.CanMove = false;
        yield return Hold("flail_end", 0, handle, 1f, u => { DrawFlail(ang, Mathf.Lerp(0.8f, 0.45f, u), 0f); SetAlpha(ballSr, 1f - u * 0.4f); }, u => ang);
        yield return Hold("flail_end", 1, null, 1f, u =>
        {
            ballSr.enabled = false;
            foreach (SpriteRenderer l in links) l.enabled = false;
        });
        Leave(Mathf.Max(0.15f, w.attackCooldown));
    }

    /// <summary>Draws the chain and ball for a ball angle and distance. Returns where the ball is (for hit checks).</summary>
    private Vector2 DrawFlail(float ang, float r, float speed01)
    {
        Vector2 hand = HandWorld(lastFrame);
        Vector2 d = Dir(ang);
        int count = 7;
        EnsureLinks(count);
        float lag = 28f * speed01;
        float chainStart = 0.6f;
        for (int i = 0; i < count; i++)
        {
            float f01 = (i + 1f) / (count + 1f);
            float a = ang + lag * (1f - f01) * (1f - f01);                         // links trail behind (the spin is clockwise)
            SpriteRenderer l = links[i];
            l.enabled = true;
            l.sprite = RuntimeSprites.Circle;
            l.color = i % 2 == 0 ? new Color(0.72f, 0.72f, 0.8f) : new Color(0.5f, 0.5f, 0.6f);
            l.transform.position = hand + Dir(a) * Mathf.Lerp(chainStart, r, f01);
            l.transform.localScale = Vector3.one * 0.14f;
            l.sortingOrder = BodyOrder + (Dir(a).y > 0.35f ? -1 : 1);
            l.sortingLayerID = SortingLayer();
        }
        Sprite ballPic = ballSr.sprite != null ? ballSr.sprite : null;
        ballSr.enabled = true;
        if (ballPic == null) ballSr.sprite = RuntimeSprites.Circle;
        ballSr.color = Color.white;
        ballSr.transform.position = hand + d * r;
        ballSr.transform.rotation = Quaternion.Euler(0f, 0f, -ang * 1.5f);
        ballSr.transform.localScale = Vector3.one * (ballSr.sprite == RuntimeSprites.Circle ? 0.5f : 1f);
        ballSr.sortingOrder = BodyOrder + (d.y > 0.35f ? -1 : 1);
        ballSr.sortingLayerID = SortingLayer();
        return hand + d * r - new Vector2(0f, 0.35f);                              // the hit ring is at the player's body, not the raised hand
    }

    private void EnsureLinks(int n)
    {
        while (links.Count < n)
        {
            var go = new GameObject("WeaponLink");
            go.transform.SetParent(fx, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = false;
            go.AddComponent<LightingExempt>();
            links.Add(sr);
        }
    }

    private void HitBall(ItemData w, Vector2 pos, float speed01)
    {
        float mult = Mathf.Lerp(0.6f, 1.2f, speed01);                       // faster = harder
        foreach (Collider2D c in Physics2D.OverlapCircleAll(pos, 0.45f))
        {
            if (c == null || c.transform.IsChildOf(transform)) continue;
            var t = c.GetComponentInParent<IDamageable>();
            if (t == null || t.DamageTeam == Team.Player) continue;
            float until;
            if (hitCooldown.TryGetValue(t, out until) && Time.time < until) continue;
            hitCooldown[t] = Time.time + 0.4f;
            Vector2 center = c.bounds.center;
            Vector2 push = (center - actions.HandOrigin).normalized * w.knockbackForce * Mathf.Lerp(0.7f, 1.1f, speed01);
            DamageTarget(w, t, center, push, mult, false);
        }
    }

    // ------------------------------------------------------------------ HAMMER: heavy, ground flies up
    private IEnumerator HammerRoutine(ItemData w)
    {
        Enter(true);
        float sp = Mathf.Max(0.1f, w.swingSpeed);
        Sprite pic = HeldPic(w);
        Vector2 f = actions.Facing;
        const string anim = "hammer";

        yield return Hold(anim, 0, pic, sp);
        yield return Hold(anim, 1, pic, sp);
        yield return Hold(anim, 2, pic, sp);
        // the strain at the top: it shakes
        yield return Hold(anim, 3, pic, sp, u => { });
        float t0 = 0f;
        yield return Hold(anim, 4, pic, sp, u =>
        {
            t0 += Time.deltaTime;
            float jit = Mathf.Round(Mathf.Sin(t0 * 80f)) * 1f / 16f;
            weaponSr.transform.position += new Vector3(jit, 0f, 0f);
        });

        // slam: a smear of copies from the top to the ground
        float up = FrameOf(anim, 4).ang, down = FrameOf(anim, 6).ang, mid = FrameOf(anim, 5).ang;
        if (effects)
        {
            Sprite white = CombatFx.White(pic);
            Vector2 hp = HandWorld(FrameOf(anim, 5));
            for (int k = 0; k < 4; k++)
                CombatFx.Spawn(white, hp, Mathf.Lerp(up, down, k / 3f) - 45f, Vector2.one, Vector2.one, new Color(1f, 1f, 1f, 0.35f), 0.14f, BodyOrder + 2, SortingLayer());
        }
        yield return Hold(anim, 5, pic, sp);

        // impact
        Vector2 impact = actions.HandOrigin + f * w.attackRange;
        SplashHit(w, impact);
        if (screenShake) CameraFollow2D.Shake(hammerShake, 0.22f);
        if (effects)
        {
            Color[] pal = GroundSampler.Palette(impact);
            Color ringColor = Color.Lerp(pal[2], Color.white, 0.4f); ringColor.a = 0.9f;
            CombatFx.Spawn(CombatFx.Ring, impact, 0f, Vector2.one * 0.3f, Vector2.one * w.splashRadius * 2f, ringColor, 0.28f, BodyOrder - 2, SortingLayer());
            GroundSampler.Burst(impact, w.splashRadius, groundChunks, groundPower, BodyOrder + 2, SortingLayer());
        }
        if (hitStop > 0f) StartCoroutine(HitStop(hitStop * 1.5f));
        yield return Hold(anim, 6, pic, sp);
        yield return Hold(anim, 7, pic, sp);                                    // stuck in the ground
        yield return Hold(anim, 8, pic, sp, null, null, 0.5f);                  // drag it back up
        Leave(w.attackCooldown / sp);
    }

    private void SplashHit(ItemData w, Vector2 impact)
    {
        foreach (Collider2D c in Physics2D.OverlapCircleAll(impact, Mathf.Max(w.splashRadius, w.attackRadius)))
        {
            if (c == null || c.transform.IsChildOf(transform)) continue;
            var t = c.GetComponentInParent<IDamageable>();
            if (t == null || t.DamageTeam == Team.Player || !hitSet.Add(t)) continue;
            Vector2 center = c.bounds.center;
            bool direct = Vector2.Distance(center, impact) <= w.attackRadius + 0.2f;
            Vector2 away = (center - impact);
            if (away.sqrMagnitude < 0.01f) away = actions.Facing;
            Vector2 push = away.normalized * w.knockbackForce * (direct ? 1f : 0.6f);
            DamageTarget(w, t, center, push, direct ? 1f : w.splashDamagePercent, true);
        }
    }

    // ------------------------------------------------------------------ BOW: the arms draw it back
    private IEnumerator BowRoutine(ItemData w)
    {
        if (w.ammo != null && inventory != null && inventory.CountOf(w.ammo) <= 0)
        {
            GameEvents.ShowToast("No " + w.ammo.itemName + " left.");
            actions.SetCooldown(0.35f);
            yield break;
        }

        Enter(false);
        controller.SpeedMultiplier = Mathf.Max(0.05f, w.moveSpeedWhileUsing);
        float drawTime = Mathf.Max(0.1f, w.chargeSeconds / Mathf.Max(0.1f, w.swingSpeed));
        float charge = 0f;
        bool ready = false, cancelled = false;
        Sprite[] frames = w.drawFrames;
        float shakeT = 0f;

        while (true)
        {
            if (ShouldStop(w)) { cancelled = true; break; }
            if (!actions.ActionHeld) break;
            charge = Mathf.Clamp01(charge + Time.deltaTime / drawTime);
            Vector2 f = actions.Facing;
            int idx = Mathf.Min(4, Mathf.FloorToInt(charge * 5f));               // the arm poses (0..4)
            int art = idx == 0 ? 0 : idx <= 2 ? 1 : 2;                           // the bow's own picture (relaxed, half, full)
            shakeT += Time.deltaTime;
            Vector2 jitter = charge >= 1f ? new Vector2(Mathf.Round(Mathf.Sin(shakeT * 60f)), Mathf.Round(Mathf.Cos(shakeT * 47f))) * (1f / 16f) : Vector2.zero;
            PWFrame fr = Show("bow", idx, null);
            PlaceBow(frames, art, HandWorld(fr) + jitter, f, charge >= 1f);
            if (charge >= 1f && !ready)
            {
                ready = true;                                                    // a flash when fully drawn
                if (effects)
                    CombatFx.Spawn(CombatFx.Ring, HandWorld(fr), 0f, Vector2.one * 0.2f, Vector2.one * 0.9f, new Color(1f, 1f, 0.8f, 0.8f), 0.22f, BodyOrder + 2, SortingLayer());
            }
            yield return null;
        }

        Vector2 face = actions.Facing;
        bool shoot = !cancelled && charge >= bowMinCharge && inventory != null && (w.ammo == null || inventory.Remove(w.ammo, 1));
        if (!cancelled && charge >= bowMinCharge && !shoot) GameEvents.ShowToast("No " + (w.ammo != null ? w.ammo.itemName : "arrows") + " left.");

        if (shoot)
        {
            controller.CanMove = false;
            PWFrame rel = Show("bow", 5, null);                                  // the drawing hand lets go
            Vector2 hand = HandWorld(rel);
            PlaceBow(frames, 0, hand, face, false);                              // the string snaps back
            float power = Mathf.Lerp(0.35f, 1f, charge);
            Vector2 origin = hand + face * 0.5f;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(w.damage * power));
            float range = Mathf.Max(4f, w.projectileLifetime * Mathf.Max(1f, w.projectileSpeed));
            float speed = Mathf.Max(4f, w.projectileSpeed) * Mathf.Lerp(0.6f, 1f, charge);
            Projectile.Spawn(origin, face, speed, range / speed, dmg, w.knockbackForce * power, Team.Player, gameObject,
                             w.projectileSprite, Color.white, 0.4f);
            if (effects)
            {
                var puff = CombatFx.Spawn(RuntimeSprites.Circle, origin, 0f, Vector2.one * 0.15f, Vector2.one * 0.3f, new Color(1f, 1f, 1f, 0.6f), 0.12f, BodyOrder + 2, SortingLayer());
                puff.drift = face * 2f;
            }
            if (screenShake && charge >= 1f) CameraFollow2D.Shake(0.04f, 0.08f);
            for (float t = 0f; t < 0.05f; t += Time.deltaTime) { Show("bow", 5, null); PlaceBow(frames, 0, HandWorld(rel), face, false); yield return null; }
            for (float t = 0f; t < 0.09f; t += Time.deltaTime) { PWFrame fl = Show("bow", 6, null); PlaceBow(frames, 0, HandWorld(fl), face, false); yield return null; }
        }
        Leave(shoot ? Mathf.Max(0.05f, w.attackCooldown / Mathf.Max(0.1f, w.swingSpeed)) : 0.1f);
    }

    private void PlaceBow(Sprite[] frames, int art, Vector2 pos, Vector2 f, bool full)
    {
        if (frames == null || frames.Length == 0) { bowSr.enabled = false; return; }
        bowSr.enabled = true;
        bowSr.sprite = frames[Mathf.Clamp(art, 0, frames.Length - 1)];
        bowSr.transform.position = pos;
        bowSr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg);
        bowSr.transform.localScale = Vector3.one;
        bowSr.color = full ? Color.Lerp(Color.white, new Color(1f, 1f, 0.7f), 0.5f + 0.5f * Mathf.Sin(Time.time * 18f)) : Color.white;
        bowSr.sortingOrder = BodyOrder + 1;
        bowSr.sortingLayerID = SortingLayer();
    }

    // ------------------------------------------------------------------ hitting things
    private void HitAlong(ItemData w, float fromAngle, float toAngle, float range, float radius, float mult, bool big)
    {
        // check a few spots along the arc so a fast swing can't skip over an enemy
        for (int i = 0; i <= 2; i++)
        {
            float a = Mathf.Lerp(fromAngle, toAngle, i / 2f);
            HitPoint(w, actions.HandOrigin + Dir(a) * range, radius, mult, big);
        }
    }

    private void HitPoint(ItemData w, Vector2 center, float radius, float mult, bool big)
    {
        foreach (Collider2D c in Physics2D.OverlapCircleAll(center, radius))
        {
            if (c == null || c.transform.IsChildOf(transform)) continue;
            var t = c.GetComponentInParent<IDamageable>();
            if (t == null || t.DamageTeam == Team.Player || !hitSet.Add(t)) continue;
            Vector2 pos = c.bounds.center;
            Vector2 push = (pos - actions.HandOrigin).normalized * w.knockbackForce;
            DamageTarget(w, t, pos, push, mult, big);
        }
    }

    private void DamageTarget(ItemData w, IDamageable t, Vector2 pos, Vector2 push, float mult, bool big)
    {
        bool crit = Random.value < w.critChance;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(w.damage * mult * (crit ? w.critMultiplier : 1f)));
        var info = new DamageInfo(dmg, crit ? push * 1.25f : push, Team.Player, gameObject);
        info.crit = crit;
        t.TakeDamage(info);
        if (damageNumbers) CombatFx.Popup(pos, crit ? dmg + "!" : dmg.ToString(), crit);
        if (screenShake && crit) CameraFollow2D.Shake(0.07f, 0.12f);
        if (hitStop > 0f) StartCoroutine(HitStop(crit ? hitStop * 2f : big ? hitStop * 1.3f : hitStop));
        if (effects)
        {
            var spark = CombatFx.Spawn(RuntimeSprites.Circle, pos, 0f, Vector2.one * 0.2f, Vector2.one * (crit ? 0.7f : 0.45f), crit ? new Color(1f, 0.9f, 0.3f, 0.9f) : new Color(1f, 1f, 1f, 0.8f), 0.14f, 150, SortingLayer());
            spark.drift = Vector2.zero;
        }
    }

    private IEnumerator HitStop(float seconds)
    {
        if (hitStopping || Time.timeScale < 0.5f) yield break;
        hitStopping = true;
        Time.timeScale = 0.04f;
        yield return new WaitForSecondsRealtime(seconds);
        Time.timeScale = 1f;
        hitStopping = false;
    }

    // ------------------------------------------------------------------ small effects
    private void GhostBall(Sprite pic, Vector2 pos)
    {
        bool circle = pic == RuntimeSprites.Circle;
        CombatFx.Spawn(pic, pos + new Vector2(0f, 0.35f), 0f, Vector2.one * (circle ? 0.5f : 1f), Vector2.one * (circle ? 0.4f : 0.8f), new Color(1f, 1f, 1f, 0.3f), 0.14f, ballSr.sortingOrder - 1, SortingLayer());
    }

    private void SlashArc(float facingAngle, float range, float side)
    {
        if (!effects) return;
        float s = Mathf.Max(0.5f, range) / 1.35f;
        // the crescent is drawn pointing right; flip it for the left-to-right swing so the bright end trails the blade
        Vector2 from = new Vector2(s * 0.7f, s * 0.7f * (side > 0 ? 1f : -1f));
        Vector2 to = new Vector2(s * 1.05f, s * 1.05f * (side > 0 ? 1f : -1f));
        Color c = swordGlow; c.a = 0.85f;
        CombatFx.Spawn(CombatFx.Arc, actions.HandOrigin + new Vector2(0f, 0.2f), facingAngle, from, to, c, 0.2f, BodyOrder + 2, SortingLayer());
    }

    private void Streak(float angle, float length)
    {
        Vector2 d = Dir(angle);
        CombatFx.Spawn(RuntimeSprites.Square, actions.HandOrigin + d * (length * 0.5f), angle, new Vector2(length * 0.9f, 0.09f), new Vector2(length * 1.1f, 0.03f), new Color(1f, 1f, 1f, 0.9f), 0.1f, BodyOrder + 2, SortingLayer());
    }

    private void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c;
    }

    private static Vector2 Dir(float deg) { float r = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }
}
