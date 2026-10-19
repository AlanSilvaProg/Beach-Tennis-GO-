using UnityEngine;

public enum AdMode
{
    [Tooltip("Shows one sprite, fixed.")] Idle,
    [Tooltip("Cycles through the sprite list, sliding each new ad in horizontally.")] Loop
}

// Perimeter advertising board. Each board decides what it shows:
//  - Idle: one fixed sprite (idleIndex).
//  - Loop: the sprites in order; every holdTime seconds the next one slides in from the right.
public class AdPanel : MonoBehaviour
{
    [Header("Content")]
    public AdMode mode = AdMode.Idle;
    [Tooltip("Sprites shown by this board. Loop cycles through all of them in order.")]
    public Sprite[] sprites;
    [Tooltip("Idle only: which sprite of the list is shown.")]
    public int idleIndex = 0;

    [Header("Loop timing")]
    [Tooltip("Seconds each ad stays still before the next one slides in.")]
    public float holdTime = 3f;
    [Tooltip("Seconds the slide takes.")]
    public float slideTime = 0.6f;
    [Tooltip("Delay before this board starts looping (use different values so boards are not in sync).")]
    public float startDelay = 0f;
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Size and visuals")]
    public Vector2 size = new Vector2(3.97f, 1.1f);
    public Color tint = Color.white;
    public Renderer[] screens;
    public Transform frame;
    [Tooltip("Frame margin around the screen (m).")]
    public float frameMargin = 0.08f;

    static readonly int MainTex = Shader.PropertyToID("_MainTex");
    static readonly int NextTex = Shader.PropertyToID("_NextTex");
    static readonly int MainST = Shader.PropertyToID("_MainST");
    static readonly int NextST = Shader.PropertyToID("_NextST");
    static readonly int Slide = Shader.PropertyToID("_Slide");
    static readonly int Tint = Shader.PropertyToID("_Tint");

    // Idle boards showing the same sprite share one material (no property block), so they can batch.
    static readonly System.Collections.Generic.Dictionary<(Sprite, Color), Material> idleMaterials =
        new System.Collections.Generic.Dictionary<(Sprite, Color), Material>();

    MaterialPropertyBlock block;
    int index;
    float timer;
    bool sliding;

    bool CanLoop => mode == AdMode.Loop && sprites != null && sprites.Length > 1;

    void OnValidate() { Fit(); Show(Current(), Current(), 0f); }

    void Awake()
    {
        Fit();
        index = mode == AdMode.Idle ? Mathf.Clamp(idleIndex, 0, Mathf.Max(0, (sprites?.Length ?? 1) - 1)) : 0;
        timer = -startDelay;
        DisableShadows();
        if (!CanLoop && UseSharedMaterial(Current())) { enabled = false; return; }
        Show(Current(), Next(), 0f);
        enabled = CanLoop; // idle boards need no per-frame work
    }

    // Boards are unlit and sit at the edge of the scene: they never need to cast or receive shadows.
    void DisableShadows()
    {
        if (screens != null)
            foreach (var r in screens) SetNoShadows(r);
        if (frame != null) SetNoShadows(frame.GetComponent<Renderer>());
    }

    static void SetNoShadows(Renderer r)
    {
        if (r == null) return;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // Swaps the screens to a material that already holds the sprite (one per sprite/tint, built on first use).
    bool UseSharedMaterial(Sprite s)
    {
        if (screens == null || screens.Length == 0 || screens[0] == null || screens[0].sharedMaterial == null) return false;
        var key = (s, tint);
        if (!idleMaterials.TryGetValue(key, out var mat) || mat == null)
        {
            mat = new Material(screens[0].sharedMaterial) { name = "AdIdle" };
            Texture2D t = s != null ? s.texture : Texture2D.blackTexture;
            Vector4 st = new Vector4(1f, 1f, 0f, 0f);
            if (s != null)
            {
                Rect r = s.textureRect;
                st = new Vector4(r.width / t.width, r.height / t.height, r.x / t.width, r.y / t.height);
            }
            mat.SetTexture(MainTex, t);
            mat.SetTexture(NextTex, t);
            mat.SetVector(MainST, st);
            mat.SetVector(NextST, st);
            mat.SetFloat(Slide, 0f);
            mat.SetColor(Tint, tint);
            idleMaterials[key] = mat;
        }
        foreach (var r in screens)
            if (r != null) r.sharedMaterial = mat;
        return true;
    }

    public void Fit()
    {
        if (screens != null)
            foreach (var s in screens)
                if (s != null) s.transform.localScale = new Vector3(size.x, size.y, 1f);
        if (frame != null) frame.localScale = new Vector3(size.x + frameMargin * 2f, size.y + frameMargin * 2f, 0.1f);
    }

    Sprite Current()
    {
        if (sprites == null || sprites.Length == 0) return null;
        int i = mode == AdMode.Idle ? idleIndex : index;
        return sprites[Mathf.Clamp(i, 0, sprites.Length - 1)];
    }

    Sprite Next()
    {
        if (sprites == null || sprites.Length == 0) return null;
        return sprites[(index + 1) % sprites.Length];
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < 0f) return;

        if (!sliding)
        {
            if (timer < holdTime) return;
            sliding = true;
            timer = 0f;
        }

        float t = slideTime <= 0f ? 1f : Mathf.Clamp01(timer / slideTime);
        Show(sprites[index], sprites[(index + 1) % sprites.Length], slideCurve.Evaluate(t));
        if (t >= 1f)
        {
            index = (index + 1) % sprites.Length;
            Show(sprites[index], sprites[(index + 1) % sprites.Length], 0f);
            sliding = false;
            timer = 0f;
        }
    }

    void Show(Sprite a, Sprite b, float slide)
    {
        if (screens == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        foreach (var r in screens)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            SetSprite(block, MainTex, MainST, a);
            SetSprite(block, NextTex, NextST, b != null ? b : a);
            block.SetFloat(Slide, slide);
            block.SetColor(Tint, tint);
            r.SetPropertyBlock(block);
        }
    }

    static void SetSprite(MaterialPropertyBlock block, int texId, int stId, Sprite s)
    {
        if (s == null) { block.SetTexture(texId, Texture2D.blackTexture); block.SetVector(stId, new Vector4(1f, 1f, 0f, 0f)); return; }
        Texture2D t = s.texture;
        Rect r = s.textureRect;
        block.SetTexture(texId, t);
        block.SetVector(stId, new Vector4(r.width / t.width, r.height / t.height, r.x / t.width, r.y / t.height));
    }
}
