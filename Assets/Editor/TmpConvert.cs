using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Replaces legacy UI Text with TextMeshPro, keeping position, colour, outline, drop shadow and gradient. Used by the setup menus (they build a legacy Text and
// pass it through Convert) and by "Convert All Text To TMP" for a scene that still has legacy Text. Safe to run again.
public static class TmpConvert
{
    const string MaterialFolder = "Assets/Fonts/Materials";

    public static TMP_FontAsset FontAsset(string ttfPath, int samplingSize, string chars)
    {
        string assetPath = ttfPath.Replace(".ttf", " SDF.asset");
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fa != null) return fa;
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        fa = TMP_FontAsset.CreateFontAsset(font, samplingSize, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, false);
        AssetDatabase.CreateAsset(fa, assetPath);
        fa.atlasTexture.name = font.name + " Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        fa.material.name = font.name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.TryAddCharacters(chars);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // Outline / underlay variants of a font's material, shared by every text with the same look.
    static Material Style(TMP_FontAsset fa, Color outline, float outlineWidth, Color underlay, Vector2 underlayOffset)
    {
        bool hasOutline = outlineWidth > 0f, hasUnderlay = underlay.a > 0f;
        if (!hasOutline && !hasUnderlay) return fa.material;
        string key = fa.name + "_o" + (hasOutline ? ColorUtility.ToHtmlStringRGBA(outline) + outlineWidth.ToString("F2") : "0")
                     + "_u" + (hasUnderlay ? ColorUtility.ToHtmlStringRGBA(underlay) + underlayOffset.y.ToString("F2") : "0");
        key = key.Replace(" ", "").Replace("(", "").Replace(")", "");
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Fonts", "Materials");
        string path = MaterialFolder + "/" + key + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(fa.material) { name = key };
        if (hasOutline)
        {
            m.EnableKeyword("OUTLINE_ON");
            m.SetColor("_OutlineColor", outline);
            m.SetFloat("_OutlineWidth", outlineWidth);
        }
        if (hasUnderlay)
        {
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", underlay);
            m.SetFloat("_UnderlayOffsetX", underlayOffset.x);
            m.SetFloat("_UnderlayOffsetY", underlayOffset.y);
            m.SetFloat("_UnderlayDilate", 0.2f);
            m.SetFloat("_UnderlaySoftness", 0.25f);
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    public static TMP_Text Convert(Text t)
    {
        var go = t.gameObject;
        string text = t.text; Color color = t.color; int size = t.fontSize; bool bestFit = t.resizeTextForBestFit;
        int minSize = t.resizeTextMinSize, maxSize = t.resizeTextMaxSize; bool raycast = t.raycastTarget;
        bool digital = t.font != null && t.font.name.ToLowerInvariant().Contains("digital");
        var fa = digital ? FontAsset("Assets/Fonts/digital-7 (mono).ttf", 90, "0123456789 :")
                         : FontAsset("Assets/Fonts/LilitaOne-Regular.ttf", 64, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?-+/()ÁÀÂÃÉÊÍÓÔÕÚÇÑáàâãéêíóôõúçñ¿¡");

        Color outline = Color.clear, underlay = Color.clear; float outlineWidth = 0f; Vector2 underlayOffset = Vector2.zero;
        foreach (var s in go.GetComponents<Shadow>())
        {
            bool isOutline = s is Outline;
            if (isOutline && Mathf.Abs(s.effectDistance.x) > 0.01f && outlineWidth == 0f)
            {
                outline = s.effectColor;
                outlineWidth = Mathf.Clamp(Mathf.Abs(s.effectDistance.x) / Mathf.Max(size, 1) * 3f, 0.08f, 0.3f);
            }
            else if (Mathf.Abs(s.effectDistance.y) >= 2f && underlay.a == 0f)
            {
                underlay = s.effectColor;
                underlayOffset = new Vector2(0f, Mathf.Clamp(s.effectDistance.y / Mathf.Max(size, 1) * 1.5f, -0.5f, 0f));
            }
        }
        var grad = go.GetComponent<UiTextGradient>();
        bool hasGradient = grad != null; // `grad` compares equal to null once destroyed
        Color top = grad != null ? grad.top : Color.white, bottom = grad != null ? grad.bottom : Color.white;

        if (grad != null) Object.DestroyImmediate(grad);
        foreach (var s in go.GetComponents<Shadow>()) Object.DestroyImmediate(s);
        Object.DestroyImmediate(t);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = fa;
        tmp.fontSharedMaterial = Style(fa, outline, outlineWidth, underlay, underlayOffset);
        tmp.text = text; tmp.color = color; tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false; tmp.richText = false; tmp.raycastTarget = raycast;
        if (bestFit) { tmp.enableAutoSizing = true; tmp.fontSizeMin = minSize; tmp.fontSizeMax = maxSize; }
        if (hasGradient && top != bottom) { tmp.enableVertexGradient = true; tmp.colorGradient = new VertexGradient(top, top, bottom, bottom); }
        return tmp;
    }

    [MenuItem("Tools/Beach Tennis GO/Convert All Text To TMP")]
    public static void ConvertScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var all = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var byName = new Dictionary<string, TMP_Text>();
        foreach (var t in all) { var c = Convert(t); byName[Path(c.transform)] = c; }

        var view = Object.FindFirstObjectByType<HudView>();
        if (view != null)
        {
            TMP_Text Get(string suffix) { foreach (var kv in byName) if (kv.Key.EndsWith(suffix)) return kv.Value; return FindTmp(view, suffix); }
            view.playerDigits = new[] { Get("PlayerScore_Tens"), Get("PlayerScore_Units") };
            view.npcDigits = new[] { Get("NpcScore_Tens"), Get("NpcScore_Units") };
            view.timeDigits = new[] { Get("Hours_Tens"), Get("Hours_Units"), Get("Minutes_Tens"), Get("Minutes_Units"), Get("Seconds_Tens"), Get("Seconds_Units") };
            view.restartLabel = Get("Restart/Label"); view.retryLabel = Get("Retry/Label");
            view.winLine1 = Get("WinTitle/Line1"); view.winLine2 = Get("WinTitle/Line2"); view.winHint = Get("WinTitle/Hint");
            view.infoText = Get("HUD/Info"); view.bannerText = Get("HUD/Banner");
            EditorUtility.SetDirty(view);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BeachTennisGO] Converted " + all.Length + " Text to TextMeshPro.");
    }

    static TMP_Text FindTmp(HudView view, string suffix)
    {
        foreach (var t in view.GetComponentsInChildren<TMP_Text>(true)) if (Path(t.transform).EndsWith(suffix)) return t;
        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (Path(t.transform).EndsWith(suffix)) return t;
        return null;
    }

    static string Path(Transform t)
    {
        string p = t.name;
        for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
        return p;
    }
}
