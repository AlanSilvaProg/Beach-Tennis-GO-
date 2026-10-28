using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ShotQuality { Weak, Normal, Strong, VeryGood, Perfect }

// The look of a racket hit: a flash, a shock ring and sparks on the contact point (bigger and more colourful the harder the shot) and, for the
// very good and perfect shots, a big animated word on screen (see ShotAnnouncer for the voice and RallyRewardConfig for the bonus).
// Everything is made at run time on its own overlay canvas (generated sprites, the HUD's font), so there is nothing to wire in the scene.
// Quality is read from the ball speed after the hit and, for the top two grades, from where the ball lands: a perfect shot is fast AND
// crosses the court to the far end.
public class ShotFx : MonoBehaviour
{
    [Header("CPU shot grades only (ball speed after the hit). The player's shots are graded by the swipe: see PlayerRacketController, Shot grades)")]
    public float strongSpeed = 9.2f;
    public float veryGoodSpeed = 9.8f;
    public float perfectSpeed = 10.4f;
    [Tooltip("Very good and perfect shots must land at least this fraction of the way to the far baseline (and inside the court).")]
    [Range(0f, 1f)] public float veryGoodDepth = 0.6f;
    [Range(0f, 1f)] public float perfectDepth = 0.85f;
    public bool logShots;

    [Header("Effect")]
    [Tooltip("Size of the burst of the strongest shot, in canvas units (the canvas is 1920 x 1080).")]
    public float maxBurstSize = 190f;
    public float popupHeight = 0.72f;
    public TMP_FontAsset font;

    // Per grade (Weak, Normal, Strong, VeryGood, Perfect): size as a fraction of maxBurstSize, colour and number of sparks.
    static readonly float[] TierSize = { 0.2f, 0.36f, 0.55f, 0.78f, 1f };
    static readonly Color[] TierColor =
    {
        new Color(1f, 1f, 1f),            // white
        new Color(0.72f, 0.9f, 1f),       // ice
        new Color(0.25f, 0.55f, 1f),      // blue
        new Color(0.7f, 0.4f, 1f),        // violet
        new Color(1f, 0.8f, 0.2f),        // gold
    };
    static readonly int[] TierSparks = { 3, 5, 8, 13, 20 };

    class Piece { public RectTransform rt; public Image img; public Vector2 vel; public float age, life, spin, size, grow; public int kind; public bool live; public Color color; }
    readonly List<Piece> pieces = new List<Piece>();
    RectTransform root, popup;
    TMP_Text text, subtitle;
    Sprite glow, ring, star, streak;
    Camera cam;
    float popAge = 99f, popLife, popPower;
    int popGrade;
    Vector3 popBase;

    // `material` is one made for the font (the win title's: it already has an outline); the texts are given it as they are.
    public static ShotFx Create(GameObject host, TMP_FontAsset font, Material material)
    {
        var fx = host.AddComponent<ShotFx>();
        fx.font = font; // Awake has already run: give the font to the texts it made
        if (font != null)
            foreach (var t in new[] { fx.text, fx.subtitle })
            {
                t.font = font;
                t.fontSharedMaterial = material != null ? material : font.material;
            }
        return fx;
    }

    void Awake()
    {
        var go = new GameObject("ShotFx Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)go.transform;

        glow = MakeSprite(64, (x, y, r) => Mathf.Pow(Mathf.Clamp01(1f - r), 2f));
        ring = MakeSprite(128, (x, y, r) => Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.1f));
        star = MakeSprite(64, (x, y, r) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            return Mathf.Clamp01(1f - (ax * ay * 9f + Mathf.Max(ax, ay) * 1.1f)) * (1f - r * 0.3f);
        });
        streak = MakeSprite(64, (x, y, r) => Mathf.Clamp01(1f - Mathf.Abs(y) * 2.2f) * Mathf.Clamp01(1f - Mathf.Abs(x) * 0.9f) * Mathf.Clamp01(1f - Mathf.Abs(x)));

        var popGo = new GameObject("Popup", typeof(RectTransform));
        popup = (RectTransform)popGo.transform;
        popup.SetParent(root, false);
        popup.sizeDelta = new Vector2(700f, 140f);
        text = MakeText("Word", 96f);
        subtitle = MakeText("Sub", 38f);
        ((RectTransform)subtitle.transform).anchoredPosition = new Vector2(0f, -62f);
        popup.gameObject.SetActive(false);
    }

    TMP_Text MakeText(string name, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(popup, false);
        rt.sizeDelta = new Vector2(700f, 140f);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    // Generates a white sprite from f(x, y, radius) -> alpha, x and y in -1..1.
    static Sprite MakeSprite(int n, System.Func<float, float, float, float> f)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float x = (i + 0.5f) / n * 2f - 1f, y = (j + 0.5f) / n * 2f - 1f;
                byte a = (byte)(Mathf.Clamp01(f(x, y, Mathf.Sqrt(x * x + y * y))) * 255f);
                px[j * n + i] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    void OnDestroy()
    {
        foreach (var s in new[] { glow, ring, star, streak }) if (s != null) { Destroy(s.texture); Destroy(s); }
    }

    // ---------- quality ----------
    // 0 = the softest hit, 1 = the strongest.
    public float Power(float speed) { return Mathf.Clamp01((speed - 6f) / (perfectSpeed + 0.4f - 6f)); }

    // `player` is the human's controller: its shots are graded by how fast the swipe was (the thresholds are on the controller, in the Inspector).
    // Without it (the CPU) the grade comes from the ball speed and depth below.
    public ShotQuality Evaluate(Ball ball, Court court, Vector3 contact, Vector3 outgoing, PlayerRacketController player = null)
    {
        if (player != null)
        {
            Vector2 l = ShotAnnouncer.Landing(ball, court, contact, outgoing);
            bool inCourt = Mathf.Abs(l.x) <= court.halfWidth && Mathf.Abs(l.y) <= court.halfLength;
            float s = player.LastSwipeSpeed;
            if (logShots) Debug.Log($"[ShotFx] swipe {s:F1} m/s land x {l.x:F1} z {l.y:F1} inside {inCourt}");
            if (inCourt && s >= player.perfectSwipeSpeed) return ShotQuality.Perfect;
            if (inCourt && s >= player.greatSwipeSpeed) return ShotQuality.VeryGood;
            if (s >= player.strongSwipeSpeed) return ShotQuality.Strong;
            return s >= 0.8f ? ShotQuality.Normal : ShotQuality.Weak;
        }
        float speed = outgoing.magnitude;
        Vector2 land = ShotAnnouncer.Landing(ball, court, contact, outgoing);
        bool inside = Mathf.Abs(land.x) <= court.halfWidth && Mathf.Abs(land.y) <= court.halfLength;
        float reach = Mathf.Abs(land.y) / court.halfLength;
        if (logShots) Debug.Log($"[ShotFx] speed {speed:F1} land x {land.x:F1} z {land.y:F1} reach {reach:F2} inside {inside}");
        if (inside && speed >= perfectSpeed && reach >= perfectDepth) return ShotQuality.Perfect;
        if (inside && speed >= veryGoodSpeed && reach >= veryGoodDepth) return ShotQuality.VeryGood;
        if (speed >= strongSpeed) return ShotQuality.Strong;
        return speed >= 7.5f ? ShotQuality.Normal : ShotQuality.Weak;
    }

    // ---------- burst ----------
    // `word` is shown for the very good and perfect shots (the player's); the burst plays for every hit.
    public void Hit(Vector3 contact, ShotQuality quality, float speed, Vector3 direction, bool showWord)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(contact);
        if (sp.z <= 0f) return;
        Vector2 at;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out at);
        Vector3 d = cam.WorldToScreenPoint(contact + direction.normalized) - sp;
        Vector2 dir = ((Vector2)d).sqrMagnitude > 0.0001f ? ((Vector2)d).normalized : Vector2.up;

        // Each grade has its own size, colour and amount of sparks: white and tiny for the softest, then ice, blue, violet, and gold for the perfect shot (full size).
        int g = (int)quality;
        float size = TierSize[g] * maxBurstSize;
        Color hot = TierColor[g], light = Color.Lerp(hot, Color.white, 0.55f);
        float spread = Mathf.Lerp(1.4f, 6.2f, g / 4f);

        Spawn(0, at, Vector2.zero, size * 0.9f, 0.16f, 0f, hot);                         // flash
        Spawn(1, at, Vector2.zero, size * 0.35f, 0.2f + g * 0.04f, size * 1.1f, light);  // shock ring
        for (int i = 0; i < TierSparks[g]; i++)
        {
            // Sparks fly mostly the way the ball goes, with a spread that opens for the big shots.
            float a = (Random.value - 0.5f) * spread + Mathf.Atan2(dir.y, dir.x);
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.5f, 1.25f) * size * Mathf.Lerp(1.4f, 2.4f, g / 4f);
            Spawn(2, at, v, Random.Range(0.08f, 0.15f) * size + 5f, Random.Range(0.25f, 0.5f), 0f, Random.value < 0.4f ? light : hot);
        }
        if (g >= (int)ShotQuality.Strong)
        {
            int stars = g == 2 ? 3 : g == 3 ? 6 : 9;
            for (int i = 0; i < stars; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Spawn(3, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.4f, 1.1f) * size * 1.4f, Random.Range(10f, 24f) * (0.5f + 0.5f * TierSize[g]),
                      Random.Range(0.4f, 0.65f), 0f, Random.value < 0.5f ? Color.white : hot);
            }
        }
        if (showWord && quality >= ShotQuality.VeryGood) ShowWord(quality, at);
    }

    void Spawn(int kind, Vector2 at, Vector2 vel, float size, float life, float grow, Color color)
    {
        Piece p = null;
        foreach (var q in pieces) if (!q.live) { p = q; break; }
        if (p == null)
        {
            var go = new GameObject("Fx", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            p = new Piece { rt = (RectTransform)go.transform, img = go.GetComponent<Image>() };
            p.img.raycastTarget = false;
            pieces.Add(p);
        }
        p.live = true; p.kind = kind; p.age = 0f; p.life = life; p.vel = vel; p.size = size; p.grow = grow; p.color = color;
        p.spin = Random.Range(-300f, 300f);
        p.img.sprite = kind == 0 ? glow : kind == 1 ? ring : kind == 2 ? streak : star;
        p.img.color = color;
        p.rt.SetAsFirstSibling(); // behind the word
        p.rt.anchoredPosition = at;
        p.rt.sizeDelta = new Vector2(kind == 2 ? size * 2.2f : size, size);
        p.rt.localRotation = kind == 2 ? Quaternion.Euler(0f, 0f, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg) : Quaternion.Euler(0f, 0f, Random.value * 90f);
        p.rt.gameObject.SetActive(true);
    }

    // ---------- the word ----------
    void ShowWord(ShotQuality quality, Vector2 at)
    {
        bool perfect = quality == ShotQuality.Perfect;
        popGrade = perfect ? 2 : 1;
        text.text = I18n.Tr(perfect ? "PERFECT!" : "GREAT SHOT!");
        text.fontSize = perfect ? 100f : 78f;
        subtitle.text = "+ " + I18n.Tr("BONUS") + " +";
        subtitle.fontSize = perfect ? 40f : 32f;
        // Near the point of contact horizontally (kept on screen), at a fixed height: the contact itself is often low in the picture.
        float halfW = root.rect.width * 0.5f, halfH = root.rect.height * 0.5f;
        float x = Mathf.Clamp(at.x * 0.5f, -halfW * 0.25f, halfW * 0.25f);
        popBase = new Vector3(x, (popupHeight * 2f - 1f) * halfH, 0f);
        popup.anchoredPosition = popBase;
        popAge = 0f;
        popLife = perfect ? 1.05f : 0.85f;
        popPower = perfect ? 1f : 0.6f;
        popup.gameObject.SetActive(true);
        Animate(0f);
        // A ring of stars around the word.
        int n = perfect ? 12 : 8;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            Vector2 o = new Vector2(Mathf.Cos(a) * 1.8f, Mathf.Sin(a)) * 60f;
            Spawn(3, (Vector2)popBase + o, o.normalized * Random.Range(60f, 160f), Random.Range(16f, 30f), Random.Range(0.4f, 0.7f), 0f,
                  perfect ? Color.HSVToRGB(Random.Range(0.08f, 0.17f), 0.65f, 1f) : Color.HSVToRGB(Random.Range(0.72f, 0.8f), 0.45f, 1f));
        }
        Spawn(1, popBase, Vector2.zero, 100f, 0.5f, perfect ? 380f : 260f, perfect ? new Color(1f, 0.9f, 0.4f) : new Color(0.8f, 0.6f, 1f));
    }

    static float EaseOutBack(float k)
    {
        const float c = 2.2f;
        k = Mathf.Clamp01(k) - 1f;
        return 1f + (c + 1f) * k * k * k + c * k * k;
    }

    void Animate(float age)
    {
        float life = popLife;
        // The whole group: pops in with an overshoot, shakes a little at the start, floats up and fades out.
        float inK = EaseOutBack(age / 0.18f);
        float outK = Mathf.Clamp01((age - (life - 0.25f)) / 0.25f);
        float shake = popGrade == 2 ? Mathf.Exp(-age * 7f) * 6f : Mathf.Exp(-age * 9f) * 3f;
        popup.anchoredPosition = (Vector2)popBase + new Vector2(Mathf.Sin(age * 80f) * shake, Mathf.Sin(age * 3f) * 3f + age * 14f);
        popup.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 6f) * 3.5f * popPower + Mathf.Exp(-age * 5f) * -9f);
        popup.localScale = Vector3.one * Mathf.Max(0.0001f, inK * (1f - outK * 0.25f) * (1f + Mathf.Sin(age * 14f) * 0.025f * popPower));
        float alpha = 1f - outK;

        // A gradient that runs through the colours while the word is up (golden for perfect, violet for great: the colours of their bursts). The mesh is never touched by hand.
        float h0 = popGrade == 2 ? 0.05f + Mathf.PingPong(age * 0.25f, 0.08f) : 0.74f + Mathf.PingPong(age * 0.2f, 0.05f);
        float h1 = popGrade == 2 ? 0.13f - Mathf.PingPong(age * 0.25f, 0.06f) : 0.82f - Mathf.PingPong(age * 0.2f, 0.04f);
        Color top = Color.HSVToRGB(h1, 0.35f, 1f), bottom = Color.HSVToRGB(h0, 0.9f, 1f);
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(top, top, bottom, bottom);
        text.alpha = alpha * Mathf.Clamp01(age * 8f);
        var sc = subtitle.color; sc.a = alpha * Mathf.Clamp01((age - 0.25f) * 5f);
        subtitle.color = new Color(1f, 0.92f, 0.45f, sc.a);
        ((RectTransform)subtitle.transform).localScale = Vector3.one * (1f + Mathf.Sin(age * 10f) * 0.04f);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        foreach (var p in pieces)
        {
            if (!p.live) continue;
            p.age += dt;
            float k = p.age / p.life;
            if (k >= 1f) { p.live = false; p.rt.gameObject.SetActive(false); continue; }
            var c = p.color;
            switch (p.kind)
            {
                case 0:  // flash: shrinks and fades
                    c.a *= 1f - k;
                    p.rt.localScale = Vector3.one * (1f + k * 0.5f);
                    break;
                case 1:  // ring: grows and thins out
                    {
                        float s = p.size + p.grow * (1f - (1f - k) * (1f - k));
                        p.rt.sizeDelta = new Vector2(s, s);
                        c.a *= (1f - k) * 0.9f;
                        break;
                    }
                case 2:  // spark: a streak that flies off, slows down and shortens
                    p.rt.anchoredPosition += p.vel * dt;
                    p.vel *= Mathf.Exp(-4f * dt);
                    p.rt.localScale = new Vector3(1f - k * 0.8f, 1f - k, 1f);
                    c.a *= 1f - k * k;
                    break;
                default: // star: spins, flies off and shrinks away
                    p.rt.anchoredPosition += p.vel * dt;
                    p.vel *= Mathf.Exp(-3f * dt);
                    p.rt.localRotation *= Quaternion.Euler(0f, 0f, p.spin * dt);
                    p.rt.localScale = Vector3.one * (k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f);
                    break;
            }
            p.img.color = c;
        }
        if (popAge < popLife)
        {
            popAge += dt;
            if (popAge >= popLife) popup.gameObject.SetActive(false);
            else Animate(popAge);
        }
    }

    // Hides whatever is on screen (a new point or a new match starts).
    public void Clear()
    {
        foreach (var p in pieces) { p.live = false; p.rt.gameObject.SetActive(false); }
        popAge = popLife = 0f;
        if (popup != null) popup.gameObject.SetActive(false);
    }
}
