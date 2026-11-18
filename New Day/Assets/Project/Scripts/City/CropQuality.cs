using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crop quality: 0 Bronze (an ordinary crop), 1 Silver, 2 Gold, 3 Platinum.
/// Quality comes from fertilizer worked into the soil (see SoilCare) and is remembered on the inventory stack (ItemStack.quality).
/// Better crops sell for more, and their icon gets a coloured band so you can tell them apart.
/// The colours and price multipliers are on the Soil Care Settings asset.
/// </summary>
public static class CropQuality
{
    public const int Max = 3;
    private static readonly string[] Names = { "Bronze", "Silver", "Gold", "Platinum" };

    public static string Name(int quality) { return Names[Mathf.Clamp(quality, 0, Max)]; }

    public static Color Color(int quality)
    {
        Color[] c = SoilCareSettings.Current.qualityColors;
        return c != null && c.Length > 0 ? c[Mathf.Clamp(quality, 0, c.Length - 1)] : UnityEngine.Color.white;
    }

    /// <summary>The sale price is multiplied by this (1 for Bronze).</summary>
    public static float PriceMultiplier(int quality)
    {
        float[] m = SoilCareSettings.Current.qualityPriceMultipliers;
        return m != null && m.Length > 0 ? m[Mathf.Clamp(quality, 0, m.Length - 1)] : 1f;
    }

    /// <summary>
    /// Draws (or hides) the coloured band around an item icon. Call it whenever a slot is redrawn.
    /// Only crops get a band. It is Unity's Outline effect: the icon's own shape, drawn again in the quality colour just behind it.
    /// </summary>
    public static void Band(Image icon, ItemStack stack)
    {
        if (icon == null) return;
        var band = icon.GetComponent<Outline>();
        bool show = stack != null && !stack.IsEmpty && stack.item.type == ItemData.ItemType.Crop && stack.item.icon != null
                    && (stack.quality > 0 || SoilCareSettings.Current.showBronzeBand);
        if (!show)
        {
            if (band != null) band.enabled = false;
            return;
        }
        if (band == null)
        {
            band = icon.gameObject.AddComponent<Outline>();
            band.effectDistance = new Vector2(2f, -2f);
            band.useGraphicAlpha = true;
        }
        band.effectColor = Color(stack.quality);
        band.enabled = true;
    }
}
