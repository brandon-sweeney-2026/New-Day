using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The saved furnishing of one room: its floor, its wall covering, the floor of each patch (kitchen, bathroom...)
/// and every piece with its place and facing. One asset per room in Assets/Farm/Interiors/Layouts (named after the room id,
/// e.g. H3 for suburb house 3, KimApartment, Farmhouse).
///
/// The Interior Decorator window writes this every time you change the room, and the scene builders read it when they
/// rebuild a room, so your redecorating survives a rebuild of the Suburbs scene. Delete the asset to get the starting
/// layout from the code back.
/// </summary>
[CreateAssetMenu(fileName = "NewInteriorLayout", menuName = "Farm/Interior Layout")]
public class InteriorLayout : ScriptableObject
{
    /// <summary>One placed piece. Position is the bottom-left corner of its floor space, in tiles from the room's corner.</summary>
    [Serializable]
    public class Entry
    {
        public DecorItem item;
        public Vector2 position;
        public DecorFacing facing = DecorFacing.Front;
    }

    /// <summary>The floor of one floor patch, matched by its label on the room's InteriorStyle (e.g. "Kitchen").</summary>
    [Serializable]
    public class ZoneFloor
    {
        public string zoneLabel;
        public DecorItem floor;
    }

    [Tooltip("The theme the room was furnished in (only a note; the decorator's Restyle button changes it).")]
    public string theme = "any";

    [Header("Room surfaces (empty = keep what the room has)")]
    public DecorItem floor;
    public DecorItem wallCovering;
    public List<ZoneFloor> zoneFloors = new List<ZoneFloor>();

    [Header("Pieces")]
    public List<Entry> pieces = new List<Entry>();
}
