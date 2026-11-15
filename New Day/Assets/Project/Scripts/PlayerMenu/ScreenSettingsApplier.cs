using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes the UI Size and Brightness settings (see GameSettings) actually happen.
///
///  - UI size: every screen in this game is a Canvas with a CanvasScaler. Shrinking the scaler's
///    "reference resolution" makes everything on that canvas bigger, so this script does that for all of them.
///  - Brightness: a see-through sheet is laid over the whole screen. Black darkens it, white brightens it.
///
/// It creates itself when the game starts and stays alive in every scene, so there is nothing to set up.
/// </summary>
public class ScreenSettingsApplier : MonoBehaviour
{
    [Header("UI size")]
    [Tooltip("How often (seconds) to look for new canvases, for example after a scene change.")]
    [Min(0.1f)] [SerializeField] private float rescanSeconds = 0.5f;

    [Header("Brightness")]
    [Tooltip("How dark the screen gets at the lowest brightness (0 = no change, 1 = fully black).")]
    [Range(0f, 1f)] [SerializeField] private float darkestAlpha = 0.6f;
    [Tooltip("How washed-out the screen gets at the highest brightness (0 = no change, 1 = fully white).")]
    [Range(0f, 1f)] [SerializeField] private float brightestAlpha = 0.25f;

    private static ScreenSettingsApplier instance;

    // The size each canvas was designed for, remembered the first time we see it.
    private readonly Dictionary<CanvasScaler, Vector2> designSizes = new Dictionary<CanvasScaler, Vector2>();
    private readonly List<CanvasScaler> deadScalers = new List<CanvasScaler>();
    private Image overlay;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSelf()
    {
        if (instance != null) return;
        var go = new GameObject("ScreenSettingsApplier");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ScreenSettingsApplier>();
    }

    private void Awake()
    {
        BuildOverlay();
        GameSettings.OnChanged += Apply;
        Apply();
    }

    private void OnDestroy()
    {
        GameSettings.OnChanged -= Apply;
        if (instance == this) instance = null;
    }

    private void Update()
    {
        // Scenes make new canvases (the HUD, for one), so check again every so often.
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + rescanSeconds;
        ApplyUiScale();
    }

    private void Apply()
    {
        ApplyUiScale();
        ApplyBrightness();
    }

    private void ApplyUiScale()
    {
        float scale = GameSettings.UiScale;

        // Forget canvases that were destroyed with their scene.
        deadScalers.Clear();
        foreach (var pair in designSizes) if (pair.Key == null) deadScalers.Add(pair.Key);
        foreach (CanvasScaler dead in deadScalers) designSizes.Remove(dead);

        foreach (CanvasScaler scaler in FindObjectsByType<CanvasScaler>())
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;

            Vector2 design;
            if (!designSizes.TryGetValue(scaler, out design))
            {
                design = scaler.referenceResolution;
                designSizes[scaler] = design;
            }
            // A smaller reference resolution = bigger UI.
            scaler.referenceResolution = design / scale;
        }
    }

    private void ApplyBrightness()
    {
        if (overlay == null) return;
        float b = GameSettings.Brightness;

        if (b < 1f)
        {
            // 1 -> no black, MinBrightness -> darkestAlpha
            float t = Mathf.InverseLerp(1f, GameSettings.MinBrightness, b);
            overlay.color = new Color(0f, 0f, 0f, t * darkestAlpha);
        }
        else
        {
            float t = Mathf.InverseLerp(1f, GameSettings.MaxBrightness, b);
            overlay.color = new Color(1f, 1f, 1f, t * brightestAlpha);
        }
        overlay.enabled = overlay.color.a > 0.001f;
    }

    private void BuildOverlay()
    {
        // Its own canvas, drawn on top of everything else (the menus use 100-200).
        var canvasGo = new GameObject("Brightness Overlay");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        var imageGo = new GameObject("Sheet", typeof(RectTransform), typeof(Image));
        imageGo.transform.SetParent(canvasGo.transform, false);
        overlay = imageGo.GetComponent<Image>();
        overlay.raycastTarget = false;          // never blocks mouse clicks
        overlay.rectTransform.anchorMin = Vector2.zero;
        overlay.rectTransform.anchorMax = Vector2.one;
        overlay.rectTransform.offsetMin = Vector2.zero;
        overlay.rectTransform.offsetMax = Vector2.zero;
    }
}
