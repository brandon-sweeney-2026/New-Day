using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cooking recipe. Create more: right-click > Create > Farm > Recipe, then add it to a CookingStation's list.
///
/// An ingredient is either one specific item (e.g. Tomato) or "any crop" (any harvested vegetable).
/// The dish remembers the average pollution of its ingredients, so food made from polluted crops sells for less.
/// </summary>
[CreateAssetMenu(fileName = "NewRecipe", menuName = "Farm/Recipe")]
public class Recipe : ScriptableObject
{
    [Serializable]
    public class Ingredient
    {
        [Tooltip("The exact item needed. Ignored when 'Any Crop' is ticked.")]
        public ItemData item;
        [Tooltip("Accept any item of type Crop (whatever the player grew).")]
        public bool anyCrop;
        [Min(1)] public int count = 1;
    }

    public string recipeName = "New Recipe";
    [TextArea] public string description;
    public List<Ingredient> ingredients = new List<Ingredient>();

    [Header("Result")]
    public ItemData result;
    [Min(1)] public int resultCount = 1;
    [Tooltip("In-game minutes that pass while cooking.")]
    [Min(0)] public int cookMinutes = 30;

    // ------------------------------------------------------------------

    private static bool IsCrop(ItemData i) { return i.type == ItemData.ItemType.Crop; }

    public bool CanCook(Inventory inv)
    {
        if (inv == null || result == null) return false;

        int cropsReserved = 0;
        int anyCropNeeded = 0;

        foreach (Ingredient ing in ingredients)
        {
            if (ing.anyCrop) { anyCropNeeded += ing.count; continue; }
            if (ing.item == null || inv.CountOf(ing.item) < ing.count) return false;
            if (IsCrop(ing.item)) cropsReserved += ing.count;
        }

        return inv.CountMatching(IsCrop) - cropsReserved >= anyCropNeeded;
    }

    /// <summary>"Tomato 2/2, Any crop 1/3"</summary>
    public string IngredientText(Inventory inv)
    {
        var parts = new List<string>();
        foreach (Ingredient ing in ingredients)
        {
            string name = ing.anyCrop ? "Any crop" : (ing.item != null ? ing.item.itemName : "?");
            int have = inv == null ? 0 : (ing.anyCrop ? inv.CountMatching(IsCrop) : inv.CountOf(ing.item));
            parts.Add(name + " " + Mathf.Min(have, 999) + "/" + ing.count);
        }
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Uses up the ingredients and gives the dish. 'bonusChance' (0-1) is the kitchen's chance of one extra serving.
    /// </summary>
    public bool TryCook(Inventory inv, Vector3 dropPosition, float bonusChance)
    {
        if (!CanCook(inv)) return false;

        int pollutionSum = 0;
        int units = 0;

        // Specific ingredients first, so "any crop" only uses what is left over.
        foreach (Ingredient ing in ingredients)
        {
            if (ing.anyCrop) continue;
            ItemData wanted = ing.item;
            int p;
            inv.TryConsume(i => i == wanted, ing.count, out p);
            pollutionSum += p;
            units += ing.count;
        }
        foreach (Ingredient ing in ingredients)
        {
            if (!ing.anyCrop) continue;
            int p;
            inv.TryConsume(IsCrop, ing.count, out p);
            pollutionSum += p;
            units += ing.count;
        }

        int level = units > 0 ? Mathf.Clamp(Mathf.RoundToInt((float)pollutionSum / units), 0, PollutionSettings.MaxLevel) : 0;
        int amount = resultCount + (UnityEngine.Random.value < bonusChance ? 1 : 0);

        int leftover = inv.Add(result, amount, level);
        if (leftover > 0) ItemPickup.Spawn(result, leftover, dropPosition, level);

        GameState.AddCounter("meals_cooked", amount);
        GameState.AddCounter("cooked_" + recipeName.ToLowerInvariant().Replace(' ', '_'), amount);

        string note = level > 0 ? " (" + PollutionSettings.Current.Label(level) + " pollution)" : "";
        GameEvents.ShowToast("Cooked " + amount + " " + result.itemName + note);

        if (cookMinutes > 0 && TimeManager.Instance != null) TimeManager.Instance.AddMinutes(cookMinutes);
        return true;
    }
}
