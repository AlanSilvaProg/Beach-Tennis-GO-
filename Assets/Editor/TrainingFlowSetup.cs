using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Training flow: the stars at the top of the screen, the trophy path on the win screen and the "+N BONUS" (rewarded ad) button, all made in the
// Gameplay scene (run "Online > Build Online Scene" afterwards to copy them to the Online scene). The white shapes (star, trophies, dot, ring) are drawn
// here and saved in Assets/Sprites/training. Safe to run again: what exists is kept.
// Menu: Tools > Beach Tennis GO > Add Training Stars And Trophies
public static class TrainingFlowSetup
{
    const string ScenePath = "Assets/Scene/Gameplay.unity";
    const string Dir = "Assets/Sprites/training";

    // ---------- shapes (signed distance: negative inside, 0 on the edge, in pixels of a 128 px texture) ----------
    static float SdPolygon(Vector2 p, Vector2[] v)
    {
        float d = Vector2.Dot(p - v[0], p - v[0]), s = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            Vector2 e = v[j] - v[i], w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * Mathf.Sqrt(d);
    }

    static Vector2[] StarPoints(Vector2 c, float outer, float inner)
    {
        var v = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? outer : inner;
            v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return v;
    }

    static readonly Vector2[] StarBig = StarPoints(new Vector2(64f, 60f), 42f, 22f);
    static float StarSd(Vector2 p) { return SdPolygon(p, StarBig) - 10f; }

    static readonly Vector2[] Bowl = { new Vector2(35, 113), new Vector2(93, 113), new Vector2(88, 82), new Vector2(74, 65), new Vector2(54, 65), new Vector2(40, 82) };
    static readonly Vector2[] Stem = { new Vector2(60, 38), new Vector2(68, 38), new Vector2(68, 62), new Vector2(60, 62) };
    static readonly Vector2[] Base = { new Vector2(40, 24), new Vector2(88, 24), new Vector2(88, 28), new Vector2(40, 28) };
    static readonly Vector2[] StarSmall = StarPoints(new Vector2(64f, 92f), 11f, 5.5f);
    static float TrophySd(Vector2 p)
    {
        float d = SdPolygon(p, Bowl) - 5f;
        d = Mathf.Min(d, SdPolygon(p, Stem) - 5f);
        d = Mathf.Min(d, SdPolygon(p, Base) - 5f);
        d = Mathf.Min(d, Mathf.Abs(Vector2.Distance(p, new Vector2(30f, 92f)) - 15f) - 5f);
        d = Mathf.Min(d, Mathf.Abs(Vector2.Distance(p, new Vector2(98f, 92f)) - 15f) - 5f);
        return d;
    }
    static float SmallStarSd(Vector2 p) { return SdPolygon(p, StarSmall) - 3f; }

    // `fill`: solid; otherwise only a line of `width` px inside the edge.
    static float Cover(float sd, bool fill, float width)
    {
        float outer = Mathf.Clamp01(0.5f - sd);
        return fill ? outer : outer - Mathf.Clamp01(0.5f - (sd + width));
    }

    delegate float Shape(Vector2 p);

    static Texture2D Draw(int size, System.Func<Vector2, float> coverage)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        float scale = 128f / size;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = coverage(new Vector2((x + 0.5f) * scale, (y + 0.5f) * scale));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        tex.SetPixels32(px);
        return tex;
    }

    static Sprite Save(Texture2D tex, string name)
    {
        System.IO.Directory.CreateDirectory(Dir);
        string path = Dir + "/" + name + ".png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = 128; imp.npotScale = TextureImporterNPOTScale.None;
        imp.textureCompression = TextureImporterCompression.Compressed; imp.crunchedCompression = false;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite[] MakeSprites()
    {
        const float W = 9f;
        var star = Save(Draw(128, p => Cover(StarSd(p), true, 0)), "star-fill");
        var starLine = Save(Draw(128, p => Cover(StarSd(p), false, W)), "star-outline");
        var trophy = Save(Draw(128, p => Cover(TrophySd(p), true, 0)), "trophy-fill");
        var trophyLine = Save(Draw(128, p => Cover(TrophySd(p), false, W)), "trophy-outline");
        var finalFill = Save(Draw(128, p => Cover(Mathf.Max(TrophySd(p), -SmallStarSd(p)), true, 0)), "trophy-final-fill");
        var finalLine = Save(Draw(128, p => Mathf.Max(Cover(TrophySd(p), false, W), Cover(SmallStarSd(p) + 3f, false, 5f))), "trophy-final-outline");
        var dot = Save(Draw(32, p => Cover(Vector2.Distance(p, new Vector2(64f, 64f)) - 60f, true, 0)), "dot");
        var ring = Save(Draw(128, p => Cover(Vector2.Distance(p, new Vector2(64f, 64f)) - 60f, false, 8f)), "ring");
        return new[] { star, starLine, trophy, trophyLine, finalFill, finalLine, dot, ring };
    }

    // ---------- scene ----------
    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.raycastTarget = false; img.preserveAspect = true;
        return img;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    static void Soft(Image img, float distance)
    {
        var sh = img.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.4f); sh.effectDistance = new Vector2(0f, -distance);
    }

    static TMP_Text CopyLabel(TMP_Text source, string name, Transform parent)
    {
        var go = Object.Instantiate(source.gameObject, parent);
        go.name = name;
        return go.GetComponent<TMP_Text>();
    }

    [MenuItem("Tools/Beach Tennis GO/Add Training Stars And Trophies")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[BeachTennisGO] Leave Play mode first."); return; }
        var active = EditorSceneManager.GetActiveScene();
        var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath);
        var gm = Object.FindFirstObjectByType<GameManager>();
        var hud = GameObject.Find("HUD").transform;
        var winTitle = hud.Find("WinTitle");
        var stack = winTitle.Find("Stack");
        var panel = Object.FindFirstObjectByType<WinRewardPanel>(FindObjectsInactive.Include);
        var layout = Object.FindFirstObjectByType<WinScreenLayout>(FindObjectsInactive.Include);
        var continueBtn = stack.Find("Continue");
        var s = MakeSprites();
        Sprite starFill = s[0], starLine = s[1], trFill = s[2], trLine = s[3], finFill = s[4], finLine = s[5], dot = s[6], ring = s[7];

        // --- stars at the top of the screen ---
        var meter = hud.Find("StarMeter") != null ? hud.Find("StarMeter").GetComponent<StarMeter>() : null;
        if (meter == null)
        {
            var root = NewRect("StarMeter", hud);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetSiblingIndex(hud.Find("ServeHint").GetSiblingIndex() + 1);
            meter = root.gameObject.AddComponent<StarMeter>();
            var row = NewRect("Row", root);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f); row.pivot = new Vector2(0.5f, 0.5f);
            row.anchoredPosition = new Vector2(0f, -122f); row.sizeDelta = new Vector2(300f, 58f);
            var template = NewRect("Template", row);
            template.sizeDelta = new Vector2(58f, 58f);
            var outline = NewImage("Outline", template, starLine); Stretch((RectTransform)outline.transform); Soft(outline, 3f); outline.color = Color.black;
            var fill = NewImage("Fill", template, starFill); Stretch((RectTransform)fill.transform); Soft(fill, 3f); fill.color = new Color(1f, 0.82f, 0.1f);
            var spark = NewRect("Sparkles", root);
            Stretch(spark);
            var sp = spark.gameObject.AddComponent<UiSparkles>();
            sp.sparkle = starFill; sp.ring = ring;
            meter.row = row; meter.starTemplate = template; meter.sparkles = sp;
            template.gameObject.SetActive(false);
            row.gameObject.SetActive(false);
        }

        // --- trophy path on the win screen ---
        var path = winTitle.Find("TrophyPath") != null ? winTitle.Find("TrophyPath").GetComponent<TrophyPath>() : null;
        var label = continueBtn.Find("Label").GetComponent<TMP_Text>();
        if (path == null)
        {
            var root = NewRect("TrophyPath", winTitle);
            Stretch(root);
            root.SetSiblingIndex(0); // behind the stack
            path = root.gameObject.AddComponent<TrophyPath>();
            var cap = CopyLabel(label, "Caption", root);
            var crt = (RectTransform)cap.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1f); crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(360f, 34f); crt.anchoredPosition = new Vector2(0f, -92f);
            cap.enableAutoSizing = true; cap.fontSizeMin = 14; cap.fontSizeMax = 30; cap.alignment = TextAlignmentOptions.Center; cap.text = "LEVEL 1";
            var row = NewRect("Row", root);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f); row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -130f); row.sizeDelta = new Vector2(330f, 84f);
            var template = NewRect("Template", row);
            template.sizeDelta = new Vector2(54f, 54f);
            var outline = NewImage("Outline", template, trLine); Stretch((RectTransform)outline.transform); Soft(outline, 2.5f); outline.color = Color.black;
            var fill = NewImage("Fill", template, trFill); Stretch((RectTransform)fill.transform); Soft(fill, 2.5f); fill.color = new Color(1f, 0.82f, 0.1f);
            var num = CopyLabel(label, "Number", template);
            var nrt = (RectTransform)num.transform;
            nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(1f, 0f); nrt.pivot = new Vector2(0.5f, 1f);
            nrt.sizeDelta = new Vector2(0f, 26f); nrt.anchoredPosition = new Vector2(0f, 2f);
            num.enableAutoSizing = false; num.fontSize = 22; num.alignment = TextAlignmentOptions.Center; num.text = "1";
            var spark = NewRect("Sparkles", root);
            Stretch(spark);
            var sp = spark.gameObject.AddComponent<UiSparkles>();
            sp.sparkle = starFill; sp.ring = ring; sp.pieceSize = 20f; sp.count = 16; sp.speed = 150f;
            path.row = row; path.trophyTemplate = template; path.caption = cap; path.sparkles = sp;
            path.normalOutline = trLine; path.normalFill = trFill; path.finalOutline = finLine; path.finalFill = finFill; path.dot = dot;
            template.gameObject.SetActive(false);
            row.gameObject.SetActive(false);
        }

        // --- "+N BONUS" button (rewarded ad), a copy of CONTINUE in pink ---
        if (stack.Find("DoubleCoins") == null)
        {
            var go = Object.Instantiate(continueBtn.gameObject, stack);
            go.name = "DoubleCoins";
            var rt = (RectTransform)go.transform;
            var btn = go.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = new Color(0.93f, 0.30f, 0.60f); cb.highlightedColor = new Color(1f, 0.45f, 0.70f); cb.pressedColor = new Color(0.72f, 0.22f, 0.46f);
            cb.selectedColor = cb.normalColor; cb.disabledColor = new Color(0.93f, 0.30f, 0.60f, 0.45f);
            btn.colors = cb;
            var lab = go.transform.Find("Label").GetComponent<TMP_Text>();
            var lrt = (RectTransform)lab.transform;
            lrt.anchorMin = new Vector2(0.26f, 0f); lrt.anchorMax = new Vector2(0.77f, 1f); lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            lab.text = "+100 BONUS";
            var coin = NewImage("Coin", go.transform, AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/rally-coin.png"));
            var c = (RectTransform)coin.transform; c.anchorMin = new Vector2(0.02f, 0.06f); c.anchorMax = new Vector2(0.24f, 0.94f); c.offsetMin = c.offsetMax = Vector2.zero;
            Sprite ad = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/racket-icons.png")) if (o is Sprite sp && sp.name == "ad") ad = sp;
            var adImg = NewImage("AdIcon", go.transform, ad);
            var a = (RectTransform)adImg.transform; a.anchorMin = new Vector2(0.79f, 0.16f); a.anchorMax = new Vector2(0.96f, 0.84f); a.offsetMin = a.offsetMax = Vector2.zero;
            rt.SetSiblingIndex(continueBtn.GetSiblingIndex());
            panel.doubleButton = go.GetComponent<Button>();
            panel.doubleLabel = lab;
            layout.doubleButton = rt;
            go.SetActive(false);
        }

        // --- GameManager ---
        gm.stars = meter; gm.trophies = path;
        gm.starGained = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/success-feedback.mp3");
        gm.starLost = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/star-lost.mp3");

        // --- settings: points / sets no longer matter (training goes by level, online is fixed) ---
        var rows = hud.Find("SettingsWindow/Rows");
        if (rows != null && rows.Find("PointsRow") != null && rows.Find("PointsRow").gameObject.activeSelf)
        {
            rows.Find("PointsRow").gameObject.SetActive(false);
            rows.Find("SetsRow").gameObject.SetActive(false);
            foreach (var n in new[] { "VoiceRow", "MusicRow", "SfxRow" })
            {
                var r = (RectTransform)rows.Find(n);
                r.anchoredPosition += new Vector2(0f, 156f);
            }
            var rr = (RectTransform)rows;
            rr.sizeDelta = new Vector2(rr.sizeDelta.x, rr.sizeDelta.y - 156f);
        }

        foreach (var o in new Object[] { gm, panel, layout }) EditorUtility.SetDirty(o);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeachTennisGO] Training stars, trophy path and double-coins button added.");
    }
}
