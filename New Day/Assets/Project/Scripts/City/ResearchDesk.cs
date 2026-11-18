using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The scientist's desk at the university. Two things happen here:
///   1. "Fund research": pay gold once to research something (a sprinkler, a moisture gel, a fertilizer...).
///   2. "Buy supplies": everything already researched can be bought here, as many as you like.
/// The list of projects is the Research Catalog asset. Needs a Collider2D (the desk's own solid one is fine).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ResearchDesk : MonoBehaviour, IInteractable
{
    [SerializeField] private string title = "University Research Lab";
    [SerializeField] private string prompt = "Research";
    [Tooltip("The list of everything that can be researched (Assets/Farm/City/ResearchCatalog.asset).")]
    [SerializeField] private ResearchCatalog catalog;

    private static readonly int[] Quantities = { 1, 5, 10 };
    private int quantityIndex;
    private PlayerActions player;

    public string InteractPrompt { get { return prompt; } }

    public void Interact(PlayerActions p)
    {
        player = p;
        quantityIndex = 0;
        MenuUI.Open(title, BuildTop, "Up/Down: choose     Space: open     Esc: close");
    }

    private List<MenuRow> BuildTop()
    {
        int done = 0, total = 0;
        if (catalog != null) foreach (ResearchProject r in catalog.projects) { if (r == null) continue; total++; if (ResearchCatalog.IsDone(r.id)) done++; }
        return new List<MenuRow>
        {
            new MenuRow { label = "Fund research", right = done + " / " + total, detail = "Pay once and the lab works it out for you. Finished research unlocks new supplies.",
                          onSelect = () => MenuUI.Open(title + " - research", BuildResearchRows, "Up/Down: choose     Space: fund     Esc: back") },
            new MenuRow { label = "Buy supplies", detail = "Sprinklers, soil gels and fertilizers you have already researched.",
                          onSelect = () => MenuUI.Open(title + " - supplies", BuildSupplyRows, "Up/Down: choose     Left/Right: amount     Space: buy     Esc: back") },
            new MenuRow { label = "Close", onSelect = MenuUI.CloseAll }
        };
    }

    // ------------------------------------------------------------------ research
    private List<MenuRow> BuildResearchRows()
    {
        var rows = new List<MenuRow>();
        if (catalog != null)
            foreach (ResearchProject project in catalog.projects)
            {
                if (project == null) continue;
                ResearchProject r = project;
                bool done = ResearchCatalog.IsDone(r.id);
                bool locked = !done && !string.IsNullOrEmpty(r.requires) && !ResearchCatalog.IsDone(r.requires);
                ResearchProject needed = locked ? catalog.Find(r.requires) : null;

                rows.Add(new MenuRow
                {
                    label = r.title,
                    right = done ? "Done" : (locked ? "Locked" : r.goldCost + "g"),
                    enabled = !done && !locked && GameState.Gold >= r.goldCost,
                    color = done ? (Color?)new Color(0.7f, 1f, 0.7f) : null,
                    detail = r.description + (locked ? "\nNeeds first: " + (needed != null ? needed.title : r.requires) : "") + "\nGold: " + GameState.Gold + "g",
                    onSelect = () => Fund(r)
                });
            }
        if (rows.Count == 0) rows.Add(new MenuRow { label = "(nothing to research yet)", enabled = false, detail = "Run Farm > Set Up > City Research and Items, then rebuild the city." });
        return rows;
    }

    private void Fund(ResearchProject r)
    {
        if (ResearchCatalog.IsDone(r.id)) { GameEvents.ShowToast("That research is already finished."); return; }
        if (!string.IsNullOrEmpty(r.requires) && !ResearchCatalog.IsDone(r.requires)) { GameEvents.ShowToast("The lab has to finish the earlier research first."); return; }
        if (!GameState.TrySpendGold(r.goldCost)) { GameEvents.ShowToast("Not enough gold."); return; }

        GameState.SetFlag(ResearchCatalog.FlagOf(r.id));
        GameState.AddCounter("research_done");
        GameEvents.ShowToast("Research finished: " + r.title + "!");
    }

    // ------------------------------------------------------------------ supplies
    private List<MenuRow> BuildSupplyRows()
    {
        Inventory inv = player != null ? player.Inventory : null;
        int qty = Quantities[quantityIndex];
        var rows = new List<MenuRow>();
        if (catalog != null)
            foreach (ResearchProject r in catalog.projects)
            {
                if (r == null || r.unlocks == null || !ResearchCatalog.IsDone(r.id)) continue;
                foreach (ItemData unlocked in r.unlocks)
                {
                    if (unlocked == null) continue;
                    ItemData item = unlocked;
                    int total = item.buyPrice * qty;
                    rows.Add(new MenuRow
                    {
                        label = item.itemName,
                        right = "x" + qty + "   " + total + "g",
                        enabled = GameState.Gold >= total,
                        detail = (string.IsNullOrEmpty(item.description) ? "" : item.description + "\n") + "You have " + (inv != null ? inv.CountOf(item) : 0) + "   -   Gold: " + GameState.Gold + "g",
                        onLeft = () => quantityIndex = Mathf.Max(0, quantityIndex - 1),
                        onRight = () => quantityIndex = Mathf.Min(Quantities.Length - 1, quantityIndex + 1),
                        onSelect = () => Buy(item, qty, inv)
                    });
                }
            }
        if (rows.Count == 0) rows.Add(new MenuRow { label = "(nothing yet)", enabled = false, detail = "Fund some research first. What the lab works out is sold here." });
        return rows;
    }

    private void Buy(ItemData item, int qty, Inventory inv)
    {
        if (inv == null) return;
        int total = item.buyPrice * qty;
        if (GameState.Gold < total) { GameEvents.ShowToast("Not enough gold."); return; }
        if (inv.CanFit(item, qty) < qty) { GameEvents.ShowToast("Your backpack is full."); return; }
        GameState.TrySpendGold(total);
        inv.Add(item, qty);
        GameEvents.ShowToast("Bought " + qty + " " + item.itemName);
    }
}
