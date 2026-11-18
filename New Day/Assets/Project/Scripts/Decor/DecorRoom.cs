using UnityEngine;

/// <summary>
/// Marks a room (an object with a HouseInterior) as decoratable. Sits next to the HouseInterior.
/// The pieces live under a child called "Decor"; the room frame and wall shadows under "RoomFrame".
/// Use Farm > Level Editors > Interior Decorator to change it. Everything here is set up by the editor tools.
/// </summary>
[RequireComponent(typeof(HouseInterior))]
[DisallowMultipleComponent]
public class DecorRoom : MonoBehaviour
{
    [Tooltip("Name of this room's layout asset (H1 ... H10, KimApartment, Farmhouse).")]
    public string roomId = "Room";
    [Tooltip("Where this room's furnishing is saved. The Interior Decorator keeps it up to date.")]
    public InteriorLayout layout;
    [Tooltip("ON for the farmhouse: its floor and walls are chosen at the design table (HouseManager), so the decorator leaves them alone.")]
    public bool surfacesFromHouseManager = false;

    /// <summary>The child that holds the pieces (made if missing).</summary>
    public Transform PiecesRoot
    {
        get
        {
            Transform t = transform.Find("Decor");
            if (t == null) { t = new GameObject("Decor").transform; t.SetParent(transform, false); }
            return t;
        }
    }
}
