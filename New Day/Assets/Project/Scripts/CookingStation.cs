using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A stove. Press the action button in front of it to cook with the ingredients in your backpack.
/// The kitchen you have installed (see HouseManager) can give a chance of an extra serving.
/// Needs a Collider2D (a trigger works well). Fill 'recipes' with Recipe assets.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CookingStation : MonoBehaviour, IInteractable
{
    [SerializeField] private List<Recipe> recipes = new List<Recipe>();
    [Tooltip("Where the cooked food lands if the backpack is full.")]
    [SerializeField] private Vector2 dropOffset = new Vector2(0f, -0.6f);

    /// <summary>The recipes on this stove (Mitzi reads these to teach crafting).</summary>
    public IReadOnlyList<Recipe> Recipes { get { return recipes; } }

    public string InteractPrompt { get { return "Cook"; } }

    private PlayerActions player;

    public void Interact(PlayerActions p)
    {
        player = p;
        MenuUI.Open("Kitchen", BuildRows, "Up/Down: choose     Space: cook     Esc: close");
    }

    private List<MenuRow> BuildRows()
    {
        Inventory inv = player != null ? player.Inventory : null;
        float bonus = HouseManager.Instance != null ? HouseManager.Instance.FoodBonus : 0f;
        var rows = new List<MenuRow>();

        foreach (Recipe recipe in recipes)
        {
            if (recipe == null || recipe.result == null) continue;
            Recipe r = recipe;
            bool can = r.CanCook(inv);

            string effects = "";
            if (r.result.energyRestored > 0) effects += "  +" + r.result.energyRestored + " HP";
            if (r.result.magicRestored > 0) effects += "  +" + r.result.magicRestored + " MP";

            rows.Add(new MenuRow
            {
                label = r.recipeName,
                right = can ? "Ready" : "Missing",
                enabled = can,
                color = can ? (Color?)new Color(0.7f, 1f, 0.7f) : null,
                detail = r.description + "\nNeeds: " + r.IngredientText(inv) + "\nMakes: " + r.resultCount + " " + r.result.itemName + effects +
                         "   (" + r.cookMinutes + " min)" + (bonus > 0f ? "\nYour kitchen: " + Mathf.RoundToInt(bonus * 100f) + "% chance of an extra serving" : ""),
                onSelect = () =>
                {
                    if (!r.TryCook(inv, transform.position + (Vector3)dropOffset, bonus))
                        GameEvents.ShowToast("You don't have the ingredients.");
                }
            });
        }

        if (rows.Count == 0) rows.Add(new MenuRow { label = "(no recipes yet)", enabled = false });
        return rows;
    }
}
