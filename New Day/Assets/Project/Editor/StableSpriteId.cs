using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Gives every sprite cut from a sheet an ID that NEVER changes, so prefabs, tiles and animation clips keep their pictures
/// when a setup menu is run again.
///
/// Why this matters: Unity remembers "which sprite" by an ID, not by name. If a setup menu cuts a sheet again and hands out
/// brand-new random IDs, everything that pointed at the old sprites loses its art (this is how the Bat and Bug went blank).
///
/// How to use it when cutting a sheet in an editor script (never use GUID.Generate() for sprites):
///     rect.spriteID = StableSpriteId.For(provider, baseName, rect.name);
///
/// What it does:
///   1. if the sheet already has a sprite with this name, it KEEPS that sprite's ID  (nothing that uses it breaks);
///   2. otherwise it makes an ID from the sheet name + sprite name, which comes out the same every time.
/// Must live in a folder named "Editor".
/// </summary>
public static class StableSpriteId
{
    // The sprites the sheet had before we started re-cutting it (looked up once per sheet, not once per sprite).
    private static ISpriteEditorDataProvider cachedProvider;
    private static readonly Dictionary<string, GUID> cachedIds = new Dictionary<string, GUID>();

    /// <summary>The ID to give the sprite called 'spriteName' on the sheet called 'textureName'.</summary>
    public static GUID For(ISpriteEditorDataProvider provider, string textureName, string spriteName)
    {
        if (provider != null && !ReferenceEquals(provider, cachedProvider))
        {
            cachedProvider = provider;
            cachedIds.Clear();
            SpriteRect[] existing = provider.GetSpriteRects();
            if (existing != null)
                foreach (SpriteRect r in existing)
                    if (r != null && !string.IsNullOrEmpty(r.name) && !r.spriteID.Empty())
                        cachedIds[r.name] = r.spriteID;
        }

        GUID kept;
        if (provider != null && cachedIds.TryGetValue(spriteName, out kept)) return kept;
        return FromName(textureName, spriteName);
    }

    /// <summary>An ID worked out from the two names alone: the same names always give the same ID.</summary>
    public static GUID FromName(string textureName, string spriteName)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("sprite:" + textureName + ":" + spriteName));
            var sb = new System.Text.StringBuilder();
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return new GUID(sb.ToString());
        }
    }
}
