/// <summary>
/// Something a forest stump can be "given to": it asks for items, and something happens once it has them all.
/// ForestZone uses it (the zone heals). ForestStumpUI only talks to this, so anything new that a stump should
/// unlock can use the same offering screen by implementing it.
/// </summary>
public interface IForestOffering
{
    /// <summary>A name that is different for every offering. Offerings already given are remembered under it (ForestProgress.Given).</summary>
    string OfferingId { get; }

    /// <summary>What the stump asks for. Empty = nothing, just offer.</summary>
    ForestRequirement[] Requirements { get; }

    /// <summary>True once everything has been given (for a zone: it is healed).</summary>
    bool IsDone { get; }

    /// <summary>Called by the offering screen when the last item has been given.</summary>
    void Fulfil();

    /// <summary>The message shown at the bottom of the screen afterwards, for example "The way forward is open."</summary>
    string DoneMessage { get; }
}
