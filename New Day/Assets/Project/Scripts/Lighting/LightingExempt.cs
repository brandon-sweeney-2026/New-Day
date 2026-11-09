using UnityEngine;

/// <summary>
/// Put this on any object (or a parent of it) that the lighting system should leave alone.
///   skipShadow   - no sun shadow is created for it (glow overlays, ground decals, UI-like sprites...)
///   skipDarkness - it is NOT darkened at night and keeps whatever material it has (things that glow on their own)
/// </summary>
public class LightingExempt : MonoBehaviour
{
    public bool skipShadow = true;
    public bool skipDarkness = false;
}
