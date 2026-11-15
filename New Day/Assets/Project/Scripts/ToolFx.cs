using UnityEngine;

/// <summary>
/// The little effects that go with the farm tools: dirt for the hoe, water drops and a splash for the watering can,
/// wood chips for the axe, stone chips for the pickaxe, and a sweep with flying leaves for the scythe.
/// No art files needed: everything is made from the tiny generated shapes in RuntimeSprites and CombatFx.
/// PlayerActions calls these; you don't add this to anything. Change the colours and amounts below to taste.
/// </summary>
public static class ToolFx
{
    // ---- colours (change these to restyle the effects) ----
    private static readonly Color WaterLight = new Color(0.62f, 0.85f, 1f, 0.95f);
    private static readonly Color WaterDark = new Color(0.30f, 0.60f, 0.95f, 0.95f);
    private static readonly Color WoodLight = new Color(0.82f, 0.62f, 0.38f);
    private static readonly Color WoodDark = new Color(0.50f, 0.33f, 0.18f);
    private static readonly Color StoneLight = new Color(0.80f, 0.80f, 0.84f);
    private static readonly Color StoneDark = new Color(0.45f, 0.45f, 0.52f);
    private static readonly Color LeafLight = new Color(0.62f, 0.86f, 0.36f);
    private static readonly Color LeafDark = new Color(0.25f, 0.55f, 0.22f);
    private static readonly Color SweepColor = new Color(0.90f, 1f, 0.85f, 0.85f);

    /// <summary>Hoe / shovel: a few clods and a puff of dust in the colour of the ground that was dug.</summary>
    public static void Dirt(Vector3 pos, int order, int sortingLayerId)
    {
        GroundSampler.Burst(pos, 0.35f, 4, 0.6f, order, sortingLayerId);
    }

    /// <summary>Watering can: one drop that flies from the spout to a spot on the ground and fades as it lands.</summary>
    public static void WaterDrop(Vector3 from, Vector3 to, int order, int sortingLayerId)
    {
        const float life = 0.22f;
        Color c = Random.value < 0.5f ? WaterLight : WaterDark;
        FxFade drop = CombatFx.Spawn(RuntimeSprites.Square, from, 0f, Vector2.one * 0.09f, Vector2.one * 0.06f, c, life, order, sortingLayerId);
        drop.drift = (Vector2)(to - from) / life;      // FxFade moves it by 'drift' units each second, so it arrives as it fades
    }

    /// <summary>Watering can: a ring that spreads out on a tile that just got wet.</summary>
    public static void Splash(Vector3 pos, int order, int sortingLayerId)
    {
        // CombatFx.Ring is a thin circle 1 unit wide; squash it so it looks like it lies on the ground.
        CombatFx.Spawn(CombatFx.Ring, pos, 0f, new Vector2(0.2f, 0.12f), new Vector2(0.85f, 0.5f), WaterLight, 0.35f, order - 3, sortingLayerId);
    }

    /// <summary>Axe: wood chips fly off the trunk.</summary>
    public static void WoodChips(Vector3 pos, int order, int sortingLayerId)
    {
        Chips(pos, WoodLight, WoodDark, 6, 1f, order, sortingLayerId);
    }

    /// <summary>Pickaxe: bits of stone and a spark-coloured flash.</summary>
    public static void StoneChips(Vector3 pos, int order, int sortingLayerId)
    {
        Chips(pos, StoneLight, StoneDark, 6, 1.1f, order, sortingLayerId);
        CombatFx.Spawn(RuntimeSprites.Circle, pos, 0f, Vector2.one * 0.12f, Vector2.one * 0.5f, new Color(1f, 0.95f, 0.7f, 0.8f), 0.12f, order, sortingLayerId);
    }

    /// <summary>Scythe: cut leaves fly up from a harvested tile.</summary>
    public static void Leaves(Vector3 pos, int order, int sortingLayerId)
    {
        Chips(pos, LeafLight, LeafDark, 5, 0.8f, order, sortingLayerId);
    }

    /// <summary>Scythe: a pale crescent that sweeps out in front of the player.</summary>
    public static void Sweep(Vector3 origin, Vector2 facing, int order, int sortingLayerId)
    {
        // CombatFx.Arc is a crescent that points right, so turn it to face the way the player faces.
        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        CombatFx.Spawn(CombatFx.Arc, origin, angle, Vector2.one * 0.55f, Vector2.one * 0.95f, SweepColor, 0.18f, order, sortingLayerId);
    }

    /// <summary>Small squares that pop up, fall, bounce once and fade (uses the GroundChunk mover from CombatFx.cs).</summary>
    private static void Chips(Vector3 pos, Color light, Color dark, int count, float power, int order, int sortingLayerId)
    {
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            var go = new GameObject("ToolChip");
            go.transform.position = pos + new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.05f, 0.1f), 0f);
            go.AddComponent<LightingExempt>();              // effects make their own "light": keep the sun-shadow system off them

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RuntimeSprites.Square;
            sr.color = Color.Lerp(dark, light, Random.value);
            sr.sortingOrder = order;
            sr.sortingLayerID = sortingLayerId;

            float size = Random.Range(0.06f, 0.12f);
            go.transform.localScale = new Vector3(size, size, 1f);

            var chunk = go.AddComponent<GroundChunk>();
            chunk.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * Random.Range(0.6f, 1.4f) * power;   // sideways speed
            chunk.vh = Random.Range(2f, 3.6f) * power;                                                       // upward speed
        }
    }
}
