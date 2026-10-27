using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A small burst of sparkles (and a flash ring) on the UI, for a star that fills or a trophy that is won. The pieces are Images made on demand under
// this object and reused; time is unscaled so it plays the same whatever the game speed is.
public class UiSparkles : MonoBehaviour
{
    public Sprite sparkle;
    public Sprite ring;
    public Color[] colors = { new Color(1f, 0.93f, 0.4f), Color.white, new Color(1f, 0.7f, 0.2f) };
    [Tooltip("Pieces per burst.")] public int count = 12;
    public float life = 0.7f;
    [Tooltip("Speed of the pieces and size of a piece, in canvas units.")]
    public float speed = 120f;
    public float pieceSize = 16f;

    class Piece { public RectTransform rt; public Image img; public Vector2 vel; public float age, life, spin, size; public bool ring; public bool live; }
    readonly List<Piece> pieces = new List<Piece>();

    // Pieces are made while playing; any left in the scene by an editing session would stay frozen on screen.
    void Awake() { for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject); }

    Piece Get()
    {
        foreach (var p in pieces) if (!p.live) return p;
        var go = new GameObject("Spark", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var piece = new Piece { rt = (RectTransform)go.transform, img = go.GetComponent<Image>() };
        piece.img.raycastTarget = false;
        pieces.Add(piece);
        return piece;
    }

    // `world` is a position in the world space of the canvas (a RectTransform.position); `scale` multiplies the sizes and speed.
    public void Burst(Vector3 world, float scale = 1f)
    {
        Spawn(true, world, Vector2.zero, scale, 0.45f);
        for (int i = 0; i < count; i++)
        {
            float a = (i + Random.value * 0.8f) / count * Mathf.PI * 2f;
            Spawn(false, world, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * scale * Random.Range(0.6f, 1.2f), scale, life * Random.Range(0.8f, 1.15f));
        }
    }

    void Spawn(bool isRing, Vector3 world, Vector2 vel, float scale, float lifeTime)
    {
        var p = Get();
        p.live = true; p.ring = isRing; p.age = 0f; p.life = lifeTime; p.vel = vel;
        p.spin = Random.Range(-240f, 240f);
        p.size = (isRing ? pieceSize * 3f : pieceSize * Random.Range(0.6f, 1.2f)) * scale;
        p.img.sprite = isRing ? ring : sparkle;
        p.img.color = isRing ? Color.white : colors[Random.Range(0, colors.Length)];
        p.rt.SetAsLastSibling();
        p.rt.position = world;
        p.rt.sizeDelta = new Vector2(p.size, p.size);
        p.rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 72f));
        p.img.enabled = p.img.sprite != null;
        p.rt.gameObject.SetActive(true);
    }

    // Removes every piece still flying (the screen they belong to went away).
    public void Clear()
    {
        foreach (var p in pieces) if (p.live || p.rt != null) { p.live = false; if (p.rt != null) p.rt.gameObject.SetActive(false); }
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
            var c = p.img.color;
            if (p.ring)
            {
                float s = p.size * (1f + 2.2f * (1f - Mathf.Pow(1f - k, 2f)));
                p.rt.sizeDelta = new Vector2(s, s);
                c.a = (1f - k) * 0.8f;
            }
            else
            {
                p.rt.anchoredPosition += p.vel * dt;
                p.vel *= Mathf.Exp(-3.5f * dt);
                p.rt.localRotation *= Quaternion.Euler(0f, 0f, p.spin * dt);
                p.rt.localScale = Vector3.one * (k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f);
                c.a = Mathf.Clamp01((1f - k) * 2f);
            }
            p.img.color = c;
        }
    }
}
