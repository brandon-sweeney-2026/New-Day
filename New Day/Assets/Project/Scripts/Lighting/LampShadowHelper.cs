using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Gives a wall or cliff a Unity "Shadow Caster 2D" whose shape is taken from its COLLIDER, so lamps, torches,
/// mushrooms and crystals cannot shine through it (their light stops at the wall and a shadow falls behind it).
///
/// Why a helper is needed: when Unity adds a Shadow Caster 2D to an object that has a SpriteRenderer it uses the
/// picture's outline. The forest walls are one small picture TILED along a long strip, so that outline would be
/// one tile big. The collider is exactly the size of the wall, so we tell the caster to use that instead.
/// Unity only offers that choice in the Inspector ("Casting Source: Collider 2D"), not in code, so this script
/// sets the same hidden settings by name. If a future Unity renames them, you get one warning and the caster
/// keeps Unity's own choice (the game still runs).
/// </summary>
public static class LampShadowHelper
{
    private static bool looked, warned;
    private static PropertyInfo sourceProp, componentProp, providerProp;
    private static FieldInfo rebuildField;
    private static Type colliderProviderType;

    /// <summary>
    /// Makes sure the collider's object has a Shadow Caster 2D shaped like the collider. Keeps an existing caster
    /// (and any Inspector changes on it) and only points it at the collider. Returns the caster.
    /// </summary>
    public static ShadowCaster2D CastFromCollider(Collider2D collider)
    {
        if (collider == null) return null;
        var caster = collider.GetComponent<ShadowCaster2D>();
        if (caster == null) caster = collider.gameObject.AddComponent<ShadowCaster2D>();
        if (caster == null) return null;

        // The wall itself stays lit by a lamp in front of it; only the ground BEHIND it gets the shadow.
        caster.castingOption = ShadowCaster2D.ShadowCastingOptions.CastShadow;

        if (!FindHiddenSettings()) return caster;
        try
        {
            if (ReferenceEquals(componentProp.GetValue(caster), collider)) return caster;   // already set up

            // switch it off and on again around the change, so Unity forgets the old shape and listens to the new one
            bool wasEnabled = caster.enabled;
            caster.enabled = false;
            object provider = Activator.CreateInstance(colliderProviderType, true);
            componentProp.SetValue(caster, collider);
            providerProp.SetValue(caster, provider);
            sourceProp.SetValue(caster, Enum.ToObject(sourceProp.PropertyType, 2));      // 2 = "ShapeProvider" (use a component's shape)
            if (rebuildField != null) rebuildField.SetValue(caster, true);
            caster.enabled = wasEnabled;
        }
        catch (Exception e) { WarnOnce("could not point the Shadow Caster 2D at the collider (" + e.Message + ")."); }
        return caster;
    }

    // Finds Unity's hidden Shadow Caster 2D settings once.
    private static bool FindHiddenSettings()
    {
        if (!looked)
        {
            looked = true;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            Type t = typeof(ShadowCaster2D);
            sourceProp = t.GetProperty("shadowCastingSource", flags);
            componentProp = t.GetProperty("shadowShape2DComponent", flags);
            providerProp = t.GetProperty("shadowShape2DProvider", flags);
            rebuildField = t.GetField("m_ForceShadowMeshRebuild", flags);
            colliderProviderType = t.Assembly.GetType("UnityEngine.Rendering.Universal.ShadowShape2DProvider_Collider2D");
        }
        bool ok = sourceProp != null && componentProp != null && providerProp != null && colliderProviderType != null && sourceProp.PropertyType.IsEnum;
        if (!ok) WarnOnce("this Unity version names the Shadow Caster 2D settings differently. Set 'Casting Source' to 'Collider 2D' by hand on the walls if their lamp shadows look wrong.");
        return ok;
    }

    private static void WarnOnce(string msg)
    {
        if (warned) return;
        warned = true;
        Debug.LogWarning("LampShadowHelper: " + msg);
    }
}
