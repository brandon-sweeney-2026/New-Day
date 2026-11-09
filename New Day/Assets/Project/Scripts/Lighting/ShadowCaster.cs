using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives a sprite a sun shadow. LightingManager adds this automatically to every sprite in the scene, so you normally never add it by hand.
/// You CAN add it yourself to tweak one object (e.g. make a tree's shadow longer) or add LightingExempt to switch a shadow off.
///
/// How it works: it builds a copy of the sprite's shape, flips it under the object's feet, squashes it and leans it
/// away from the sun. The lean and length come from LightingManager, which follows the in-game clock.
/// </summary>
[DisallowMultipleComponent]
public class ShadowCaster : MonoBehaviour
{
    [Tooltip("Untick to hide this object's shadow without removing the component.")]
    public bool castShadow = true;

    [Tooltip("1 = normal. 2 = shadow twice as long. Handy for tall things like trees and light posts.")]
    public float lengthMultiplier = 1f;

    [Tooltip("Moves the start of the shadow up (+) or down (-) in world units, if the shadow doesn't meet the object's feet.")]
    public float footOffset = 0f;

    private SpriteRenderer src;
    private Transform shadowT;
    private MeshFilter filter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private MaterialPropertyBlock block;

    private Sprite builtSprite;
    private bool builtFlip;
    private float builtSign;
    private int builtVersion = -1;

    private static readonly List<Vector3> vertBuf = new List<Vector3>();
    private static readonly List<Vector2> uvBuf = new List<Vector2>();
    private static readonly List<Color32> colBuf = new List<Color32>();
    private static readonly List<int> triBuf = new List<int>();

    public static int ActiveCount { get; private set; }

    private void Awake()
    {
        src = GetComponent<SpriteRenderer>();
        if (src == null) { Destroy(this); return; }

        var go = new GameObject("Shadow");
        go.transform.SetParent(transform, false);
        shadowT = go.transform;
        filter = go.AddComponent<MeshFilter>();
        meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.sharedMaterial = LightingManager.ShadowMaterial;
        meshRenderer.enabled = false;
        // the shadow itself must never get its own shadow or be darkened
        go.AddComponent<LightingExempt>().skipDarkness = true;

        mesh = new Mesh();
        mesh.name = "ShadowMesh";
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;
        block = new MaterialPropertyBlock();
    }

    private void OnEnable() { ActiveCount++; }
    private void OnDisable() { ActiveCount--; }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (shadowT != null) Destroy(shadowT.gameObject);
    }

    private void LateUpdate()
    {
        if (src == null || meshRenderer == null) return;
        LightingManager m = LightingManager.Instance;
        bool visible = castShadow && m != null && m.ShadowsVisible && src.enabled && src.sprite != null && src.color.a > 0.05f;
        meshRenderer.enabled = visible;
        if (!visible) return;

        float sign = transform.lossyScale.x < 0f ? -1f : 1f;
        if (src.sprite != builtSprite || src.flipX != builtFlip || sign != builtSign || m.ShadowVersion != builtVersion)
            Rebuild(m, sign);

        shadowT.rotation = Quaternion.identity;   // shadows never rotate with the object
    }

    private void Rebuild(LightingManager m, float sign)
    {
        Sprite s = src.sprite;
        builtSprite = s; builtFlip = src.flipX; builtSign = sign; builtVersion = m.ShadowVersion;

        Vector2[] verts = s.vertices;
        Vector2[] uvs = s.uv;
        ushort[] tris = s.triangles;
        float minY = s.bounds.min.y;
        float len = Mathf.Max(0f, lengthMultiplier);
        float yScale = m.ShadowYScale * len;
        float shear = m.ShadowShear * len * sign;
        Color32 col = new Color32(0, 0, 0, (byte)Mathf.Clamp(Mathf.RoundToInt(m.ShadowAlpha * 255f), 0, 255));

        vertBuf.Clear(); uvBuf.Clear(); colBuf.Clear(); triBuf.Clear();
        for (int i = 0; i < verts.Length; i++)
        {
            float x = src.flipX ? -verts[i].x : verts[i].x;
            float h = verts[i].y - minY;                         // height above the feet
            vertBuf.Add(new Vector3(x + shear * h, minY - h * yScale + footOffset, 0f));
            uvBuf.Add(uvs[i]);
            colBuf.Add(col);
        }
        for (int i = 0; i < tris.Length; i++) triBuf.Add(tris[i]);

        mesh.Clear();
        mesh.SetVertices(vertBuf);
        mesh.SetUVs(0, uvBuf);
        mesh.SetColors(colBuf);
        mesh.SetTriangles(triBuf, 0);
        mesh.RecalculateBounds();

        meshRenderer.GetPropertyBlock(block);
        block.SetTexture("_MainTex", s.texture);
        meshRenderer.SetPropertyBlock(block);

        meshRenderer.sortingLayerID = src.sortingLayerID;
        meshRenderer.sortingOrder = m.ShadowSortingOrder;
    }
}
