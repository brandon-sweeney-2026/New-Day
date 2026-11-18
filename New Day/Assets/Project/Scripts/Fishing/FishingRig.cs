using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rod, the fishing line and the bobber, drawn in the game world (no screen UI) while the player fishes.
///
///   Cast()      - the player pulls the rod back over their shoulder, flicks it forward, and the bobber flies out in an arc and plops into the water.
///   (floating)  - the bobber bobs on the water and sends out a ripple now and then. Nibble() makes it twitch.
///   Bite()      - the bobber is pulled under, the water swirls, the rod bends and a "!" pops up over the player.
///   Resurface() - the bobber pops back up (the fish got away).
///   Reel()      - the bobber is pulled back to the rod; if a picture is given (a fish or a piece of junk) it comes out on the line
///                 and the player holds it up for a moment.
///   Hide()      - puts it all away.
///
/// FishingManager uses this; you don't need to call it. It is added to the FishingManager object by itself.
/// To tune the numbers in the Inspector, add a FishingRig to the FishingManager object in the scene yourself; it will use that one.
/// The rod picture is the rod item's icon, held by the handle (icons are drawn pointing up-right, like the other tools).
/// </summary>
public class FishingRig : MonoBehaviour
{
    [Header("Rod in the hand (relative to the player's feet; facing right, mirrored for left)")]
    [SerializeField] private Vector2 handSide = new Vector2(0.28f, 0.5f);
    [SerializeField] private Vector2 handDown = new Vector2(0.26f, 0.45f);
    [SerializeField] private Vector2 handUp = new Vector2(0.26f, 0.6f);
    [Tooltip("Size of the rod picture (1 = the icon's own size).")]
    [SerializeField] [Min(0.1f)] private float rodScale = 1f;

    [Header("Cast")]
    [Tooltip("Seconds spent pulling the rod back over the shoulder.")]
    [SerializeField] [Min(0.05f)] private float windUpSeconds = 0.38f;
    [Tooltip("Seconds for the fast flick forward.")]
    [SerializeField] [Min(0.03f)] private float flickSeconds = 0.13f;
    [Tooltip("Seconds the bobber is in the air.")]
    [SerializeField] [Min(0.1f)] private float flightSeconds = 0.55f;
    [Tooltip("How high the bobber flies (world units, at the top of its arc).")]
    [SerializeField] [Min(0f)] private float arcHeight = 1.1f;

    [Header("Floating")]
    [SerializeField] [Min(0f)] private float bobAmount = 0.025f;
    [SerializeField] [Min(0f)] private float bobSpeed = 2.6f;
    [Tooltip("Seconds between the small ripples around the floating bobber.")]
    [SerializeField] [Min(0.2f)] private float rippleEvery = 1.5f;

    [Header("Line")]
    [SerializeField] private Color lineColor = new Color(0.95f, 0.95f, 0.9f, 0.8f);
    [Tooltip("How many straight pieces the line is drawn from (more = smoother curve).")]
    [SerializeField] [Range(4, 30)] private int lineSegments = 14;
    [Tooltip("How much the line droops between the rod and the bobber when it is slack.")]
    [SerializeField] [Min(0f)] private float lineSag = 0.4f;
    [Tooltip("Thickness of the line in pixels (16 pixels = 1 tile).")]
    [SerializeField] [Min(0.5f)] private float lineThickness = 1f;

    [Header("Bite")]
    [Tooltip("How high above the player's feet the '!' appears.")]
    [SerializeField] private float exclaimHeight = 1.55f;

    [Header("Reel in")]
    [SerializeField] [Min(0.05f)] private float reelSeconds = 0.35f;
    [Tooltip("Seconds the player holds up what they caught.")]
    [SerializeField] [Min(0f)] private float showOffSeconds = 0.8f;

    /// <summary>What the bobber is doing right now (drawn by Update).</summary>
    private enum Mode { Hidden, InHand, Flying, Floating, Nibble, Sunk, Reeling }

    private Mode mode = Mode.Hidden;
    private PlayerActions player;
    private SpriteRenderer body;
    private Transform rodPivot;
    private SpriteRenderer rodSr, bobber, exclaim, prize;
    private readonly List<SpriteRenderer> line = new List<SpriteRenderer>();
    private float rodAngle;             // where the rod tip points, in degrees (0 = right, 90 = up)
    private Vector3 bobberRest;          // where the bobber floats
    private Vector3 bobberPos;           // where it is drawn this frame
    private float slack = 1f;            // 1 = the line droops fully, 0 = pulled tight
    private float modeTime, nextRipple, exclaimPop;

    /// <summary>The point where the bobber sits on the water (for effects).</summary>
    public Vector3 BobberPosition { get { return bobberPos; } }

    // ------------------------------------------------------------------ building the pieces
    private SpriteRenderer NewRenderer(string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<LightingExempt>();              // keep the painted sun shadows off these little pieces
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.enabled = false;
        return sr;
    }

    private void EnsureBuilt()
    {
        if (rodPivot != null) return;
        rodPivot = new GameObject("RodPivot").transform;
        rodPivot.SetParent(transform, false);
        rodSr = NewRenderer("Rod", null);
        rodSr.transform.SetParent(rodPivot, false);
        bobber = NewRenderer("Bobber", FishingArt.BobberAir);
        exclaim = NewRenderer("BiteAlert", FishingArt.Exclaim);
        prize = NewRenderer("Catch", null);
    }

    private void EnsureLine()
    {
        while (line.Count < lineSegments) line.Add(NewRenderer("Line", RuntimeSprites.Square));
        for (int i = 0; i < line.Count; i++) if (i >= lineSegments) line[i].enabled = false;
    }

    /// <summary>Gets ready to fish: the rod appears in the player's hand.</summary>
    private void Setup(PlayerActions who, ItemData rodItem)
    {
        EnsureBuilt();
        player = who;
        body = null;
        foreach (SpriteRenderer sr in who.GetComponentsInChildren<SpriteRenderer>())
            if (sr.gameObject.name != "HeldItem") { body = sr; break; }

        rodSr.sprite = rodItem != null ? rodItem.icon : null;
        if (rodSr.sprite != null)
        {
            // Put the handle (the bottom-left corner of the icon, a little way in) on the pivot, so the rod turns in the hand.
            Bounds b = rodSr.sprite.bounds;
            rodSr.transform.localPosition = -(b.min + b.size * 0.1f);
        }
        rodSr.enabled = rodSr.sprite != null;
        rodAngle = HoldAngle();
        bobber.enabled = false;
        exclaim.enabled = false;
        prize.enabled = false;
        slack = 1f;
        mode = Mode.InHand;
        ApplyRod();
    }

    // ------------------------------------------------------------------ the rod's angles for each facing direction
    private Vector2 Facing { get { return player != null ? player.Facing : Vector2.down; } }
    private bool Sideways { get { return Mathf.Abs(Facing.x) > Mathf.Abs(Facing.y); } }
    private bool FacingLeft { get { return Sideways && Facing.x < 0f; } }
    private bool FacingUp { get { return !Sideways && Facing.y > 0f; } }

    /// <summary>Angles are written for facing right and mirrored for facing left.</summary>
    private float Side(float rightAngle) { return FacingLeft ? 180f - rightAngle : rightAngle; }

    private float BackAngle() { return Sideways ? Side(140f) : (FacingUp ? -35f : 105f); }     // pulled back over the shoulder
    private float ForwardAngle() { return Sideways ? Side(22f) : (FacingUp ? 72f : -62f); }    // the end of the flick
    private float HoldAngle() { return Sideways ? Side(38f) : (FacingUp ? 66f : -48f); }       // waiting
    private float BentAngle() { return Sideways ? Side(8f) : (FacingUp ? 76f : -72f); }        // a fish pulling on it

    private Vector3 HandPosition()
    {
        Vector2 hand = Sideways ? handSide : (FacingUp ? handUp : handDown);
        if (FacingLeft) hand.x = -hand.x;
        return player.transform.position + (Vector3)hand;
    }

    private int BodyOrder { get { return body != null ? body.sortingOrder : 10; } }
    private int BodyLayer { get { return body != null ? body.sortingLayerID : 0; } }

    /// <summary>Places the rod picture so its tip points along 'rodAngle'.</summary>
    private void ApplyRod()
    {
        if (player == null) return;
        rodPivot.position = HandPosition();
        // The icon points up-right (45 degrees). Pointing to the left it is mirrored so it is not drawn upside down.
        bool mirror = Mathf.Cos(rodAngle * Mathf.Deg2Rad) < 0f;
        rodPivot.localScale = new Vector3(mirror ? -rodScale : rodScale, rodScale, 1f);
        rodPivot.rotation = Quaternion.Euler(0f, 0f, rodAngle - (mirror ? 135f : 45f));
        rodSr.sortingLayerID = BodyLayer;
        rodSr.sortingOrder = BodyOrder + (FacingUp ? -1 : 1);       // facing away: the rod is in front of the body, so drawn behind it
    }

    /// <summary>The tip of the rod in the world (where the line starts).</summary>
    private Vector3 RodTip()
    {
        if (rodSr.sprite == null) return HandPosition() + (Vector3)(Quaternion.Euler(0, 0, rodAngle) * Vector3.right * 0.9f);
        Bounds b = rodSr.sprite.bounds;
        return rodSr.transform.TransformPoint(b.max - b.size * 0.06f);
    }

    // ------------------------------------------------------------------ the steps (FishingManager yields these)
    /// <summary>Wind up, flick, and send the bobber flying to 'target'. Ends the moment it lands.</summary>
    public IEnumerator Cast(PlayerActions who, ItemData rodItem, Vector3 target)
    {
        Setup(who, rodItem);
        float start = HoldAngle(), back = BackAngle(), forward = ForwardAngle();

        // 1. pull the rod back over the shoulder (slowing down at the end)
        for (float t = 0f; t < windUpSeconds; t += Time.deltaTime)
        {
            rodAngle = Mathf.LerpAngle(start, back, EaseOut(t / windUpSeconds));
            ApplyRod();
            yield return null;
        }

        // 2. flick it forward, fast (speeding up)
        TriggerBodyAnimation("UseTool");
        for (float t = 0f; t < flickSeconds; t += Time.deltaTime)
        {
            float k = t / flickSeconds;
            rodAngle = Mathf.LerpAngle(back, forward, k * k);
            ApplyRod();
            yield return null;
        }
        rodAngle = forward;
        ApplyRod();

        // 3. the bobber flies out in an arc, pulling the line along; the rod settles back to its waiting angle
        mode = Mode.Flying;
        Vector3 from = RodTip();
        bobber.sprite = FishingArt.BobberAir;
        bobber.enabled = true;
        bobberRest = target;
        for (float t = 0f; t < flightSeconds; t += Time.deltaTime)
        {
            float k = t / flightSeconds;
            bobberPos = Vector3.Lerp(from, target, k) + Vector3.up * arcHeight * 4f * k * (1f - k);
            bobber.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 2f) * 25f);
            rodAngle = Mathf.LerpAngle(forward, HoldAngle(), EaseOut(k));
            slack = 0.15f;                                  // the line is nearly straight while it is pulled out
            yield return null;
        }

        // 4. plop!
        bobber.transform.rotation = Quaternion.identity;
        bobberPos = target;
        Splash(target, 1f);
        SetMode(Mode.Floating);
        slack = 1f;
    }

    /// <summary>A little twitch, as if a fish is nosing the bait (not a bite yet).</summary>
    public void Nibble()
    {
        if (mode != Mode.Floating) return;
        SetMode(Mode.Nibble);
        Ripple(bobberRest, 0.6f);
    }

    /// <summary>Something has bitten: pull the bobber under and show the "!" over the player's head.</summary>
    public void Bite()
    {
        SetMode(Mode.Sunk);
        bobber.sprite = FishingArt.BobberSunk;
        Splash(bobberRest, 0.7f);
        exclaim.enabled = true;
        exclaimPop = 0f;
        slack = 0f;
    }

    /// <summary>The fish let go: the bobber bobs back up.</summary>
    public void Resurface()
    {
        exclaim.enabled = false;
        bobber.sprite = FishingArt.Bobber;
        Ripple(bobberRest, 1f);
        SetMode(Mode.Floating);
        slack = 1f;
    }

    /// <summary>Holds the line tight (true) during the fight, or lets it droop (false).</summary>
    public void SetTight(bool tight) { slack = tight ? 0f : 1f; }

    /// <summary>
    /// Pulls the bobber back in. If 'caught' is given (a fish or junk picture), it comes up out of the water on the line,
    /// and the player holds it up over their head for a moment.
    /// </summary>
    public IEnumerator Reel(Sprite caught)
    {
        if (player == null || mode == Mode.Hidden) yield break;
        exclaim.enabled = false;
        SetMode(Mode.Reeling);
        Vector3 from = bobberPos;
        if (caught != null) Splash(from, 1.2f);
        bobber.sprite = FishingArt.BobberAir;

        if (caught != null)
        {
            prize.sprite = caught;
            prize.enabled = true;
            prize.transform.localScale = Vector3.one * 0.85f;
            prize.sortingLayerID = BodyLayer;
            prize.sortingOrder = BodyOrder + 3;
        }

        // the rod lifts up as the line comes in, and the bobber (with the catch hanging under it) flies back
        float lift = Sideways ? Side(70f) : (FacingUp ? 85f : 95f);
        float startAngle = rodAngle;
        for (float t = 0f; t < reelSeconds; t += Time.deltaTime)
        {
            float k = t / reelSeconds;
            rodAngle = Mathf.LerpAngle(startAngle, lift, EaseOut(k));
            ApplyRod();
            Vector3 tip = RodTip();
            bobberPos = Vector3.Lerp(from, tip, k * k) + Vector3.up * 0.6f * Mathf.Sin(k * Mathf.PI);
            slack = 0.1f;
            if (prize.enabled)
            {
                prize.transform.position = bobberPos + Vector3.down * 0.35f;
                prize.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 30f) * 20f);     // wriggling on the hook
            }
            yield return null;
        }

        bobber.enabled = false;
        HideLine();
        rodSr.enabled = false;
        mode = Mode.InHand;

        // hold the catch up over the head
        if (prize.enabled && showOffSeconds > 0f)
        {
            TriggerBodyAnimation("UseTool");
            Vector3 top = player.transform.position + Vector3.up * (exclaimHeight - 0.15f);
            Vector3 start = prize.transform.position;
            float startTilt = Mathf.DeltaAngle(0f, prize.transform.eulerAngles.z);
            for (float t = 0f; t < showOffSeconds; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.18f);
                prize.transform.position = Vector3.Lerp(start, top, EaseOut(k)) + Vector3.up * Mathf.Sin(t * 6f) * 0.02f;
                prize.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startTilt, 0f, k));
                prize.transform.localScale = Vector3.one * (0.85f + 0.15f * Mathf.Sin(k * Mathf.PI * 0.5f));
                if (Random.value < Time.deltaTime * 6f)
                    CombatFx.Spawn(RuntimeSprites.Square, top + (Vector3)Random.insideUnitCircle * 0.4f, 45f, Vector2.one * 0.08f, Vector2.zero,
                                   new Color(1f, 0.95f, 0.6f, 0.9f), 0.35f, BodyOrder + 4, BodyLayer);
                yield return null;
            }
        }
        prize.enabled = false;
    }

    /// <summary>Puts the rod, line, bobber and "!" away.</summary>
    public void Hide()
    {
        mode = Mode.Hidden;
        if (rodPivot == null) return;
        rodSr.enabled = false;
        bobber.enabled = false;
        exclaim.enabled = false;
        prize.enabled = false;
        HideLine();
    }

    // ------------------------------------------------------------------ every frame
    private void SetMode(Mode m) { mode = m; modeTime = 0f; nextRipple = rippleEvery * 0.5f; }

    private void LateUpdate()
    {
        if (mode == Mode.Hidden || player == null) return;
        float dt = Time.deltaTime;
        modeTime += dt;

        switch (mode)
        {
            case Mode.Floating:
                bobberPos = bobberRest + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobAmount;
                bobber.sprite = FishingArt.Bobber;
                nextRipple -= dt;
                if (nextRipple <= 0f) { Ripple(bobberRest, 0.45f); nextRipple = rippleEvery * Random.Range(0.8f, 1.3f); }
                break;

            case Mode.Nibble:
                // two quick dips
                bobberPos = bobberRest + Vector3.down * 0.05f * Mathf.Abs(Mathf.Sin(modeTime * Mathf.PI * 6f));
                if (modeTime > 0.33f) SetMode(Mode.Floating);
                break;

            case Mode.Sunk:
                // tugged around under the surface; a swirl of bubbles and ripples
                bobberPos = bobberRest + new Vector3(Mathf.Sin(modeTime * 23f) * 0.04f, -0.02f, 0f);
                rodAngle = Mathf.LerpAngle(HoldAngle(), BentAngle(), 0.6f + 0.4f * Mathf.Sin(modeTime * 18f));
                nextRipple -= dt;
                if (nextRipple <= 0f)
                {
                    Ripple(bobberRest, 0.8f);
                    Bubble(bobberRest);
                    nextRipple = 0.16f;
                }
                // the "!" pops in with a little overshoot, then wobbles
                exclaimPop += dt;
                float pop = exclaimPop < 0.12f ? exclaimPop / 0.12f * 1.25f : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((exclaimPop - 0.12f) / 0.1f));
                exclaim.transform.position = player.transform.position + Vector3.up * exclaimHeight;
                exclaim.transform.localScale = Vector3.one * pop;
                exclaim.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(exclaimPop * 14f) * 6f);
                exclaim.sortingLayerID = BodyLayer;
                exclaim.sortingOrder = BodyOrder + 6;
                break;
        }

        if (mode != Mode.Reeling) ApplyRod();
        if (bobber.enabled)
        {
            bobber.transform.position = bobberPos;
            bobber.sortingLayerID = BodyLayer;
            bobber.sortingOrder = BodyOrder - 1;
            DrawLine(RodTip(), bobberPos + Vector3.up * (mode == Mode.Flying || mode == Mode.Reeling ? 0.3f : 0.42f));
        }
    }

    /// <summary>Draws the line from the rod tip to the bobber as a curve made of thin squares. It droops by 'slack'.</summary>
    private void DrawLine(Vector3 a, Vector3 b)
    {
        EnsureLine();
        float thick = lineThickness / 16f;
        float sag = lineSag * slack * Mathf.Clamp01(Vector3.Distance(a, b) / 2f);
        Vector3 prev = a;
        for (int i = 0; i < lineSegments; i++)
        {
            float k = (i + 1f) / lineSegments;
            // a straight line pulled down in the middle (a parabola) looks like a drooping line
            Vector3 p = Vector3.Lerp(a, b, k) + Vector3.down * sag * 4f * k * (1f - k);
            Vector3 d = p - prev;
            SpriteRenderer s = line[i];
            s.enabled = true;
            s.color = lineColor;
            s.transform.position = (prev + p) * 0.5f;
            s.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            s.transform.localScale = new Vector3(d.magnitude + thick * 0.5f, thick, 1f);
            s.sortingLayerID = BodyLayer;
            s.sortingOrder = BodyOrder + (FacingUp ? -2 : 2);
            prev = p;
        }
    }

    private void HideLine() { foreach (SpriteRenderer s in line) s.enabled = false; }

    // ------------------------------------------------------------------ water effects (reusing the tool and combat effects)
    private void Ripple(Vector3 at, float size)
    {
        CombatFx.Spawn(CombatFx.Ring, at, 0f, new Vector2(0.15f, 0.08f) * size, new Vector2(0.9f, 0.45f) * size,
                       new Color(0.85f, 0.96f, 1f, 0.75f), 0.7f, BodyOrder - 2, BodyLayer);
    }

    private void Bubble(Vector3 at)
    {
        FxFade b = CombatFx.Spawn(RuntimeSprites.Circle, at + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(-0.05f, 0.05f), 0f), 0f,
                                  Vector2.one * 0.05f, Vector2.one * 0.09f, new Color(0.85f, 0.97f, 1f, 0.9f), 0.4f, BodyOrder, BodyLayer);
        b.drift = new Vector2(Random.Range(-0.1f, 0.1f), 0.3f);
    }

    private void Splash(Vector3 at, float strength)
    {
        ToolFx.Splash(at, BodyOrder + 1, BodyLayer);
        Ripple(at, 1f * strength);
        int drops = Mathf.RoundToInt(7 * strength);
        for (int i = 0; i < drops; i++)
        {
            Vector3 to = at + new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(0.05f, 0.45f) * strength, 0f);
            ToolFx.WaterDrop(at, to, BodyOrder + 1, BodyLayer);
        }
    }

    // ------------------------------------------------------------------ helpers
    /// <summary>Plays a body animation trigger (like UseTool) if the player's Animator has it.</summary>
    private void TriggerBodyAnimation(string trigger)
    {
        Animator anim = player != null ? player.BodyAnimator : null;
        if (anim == null || anim.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.name == trigger && p.type == AnimatorControllerParameterType.Trigger) { anim.SetTrigger(trigger); return; }
    }

    private static float EaseOut(float k)
    {
        k = Mathf.Clamp01(k);
        return 1f - (1f - k) * (1f - k);
    }
}
