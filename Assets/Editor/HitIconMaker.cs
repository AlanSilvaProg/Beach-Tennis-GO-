using System.IO;
using UnityEditor;
using UnityEngine;

// Tools / Beach Tennis GO / Make hit icon: draws the swing button's picture (Resources/hit-icon.png): a beach tennis racket with the trail of its swing.
// The racket turns about its handle end; the trail is the arc the head has just gone through, thick and bright next to the head and fading away behind it.
// Drawn at 2x and shrunk, so the edges are smooth. Run it again to redraw (after changing the numbers here).
public static class HitIconMaker
{
    const string Path = "Assets/Resources/hit-icon.png";
    const int Size = 256, Super = 2;
    const float Zoom = 1.15f;

    [MenuItem("Tools/Beach Tennis GO/Make hit icon")]
    public static void Make()
    {
        int n = Size * Super;
        var big = new Color[n * n];
        Vector2 m = new Vector2(0.33f, 0.28f);             // where the racket turns (the handle end), in 0..1 picture units
        float headAngle = 40f * Mathf.Deg2Rad;
        Vector2 u = new Vector2(Mathf.Cos(headAngle), Mathf.Sin(headAngle));
        Vector2 across = new Vector2(-u.y, u.x);
        Color white = Color.white, outline = new Color(0.36f, 0.2f, 0.02f, 1f);

        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                p = (p - new Vector2(0.5f, 0.5f)) / Zoom + new Vector2(0.5f, 0.5f); // the drawing is a little larger than the picture's middle
                Color c = new Color(0, 0, 0, 0);

                // ---- the trail: an arc round the turning point, behind the head, with a darker shadow under it so it shows on the light button ----
                float shadow = TrailAlpha(p + new Vector2(0.012f, -0.014f), m);
                if (shadow > 0f) c = Over(new Color(outline.r, outline.g, outline.b, 0.6f * shadow), c);
                float trail = TrailAlpha(p, m);
                if (trail > 0f) c = Over(new Color(1f, 1f, 1f, trail), c);

                // ---- the racket ----
                Vector2 d = p - m;
                float along = Vector2.Dot(d, u), side = Vector2.Dot(d, across);
                float body = RacketCoverage(along, side, out bool rim);
                if (body > 0f)
                {
                    // outline: the racket shape grown a little
                    Color fill = rim ? white : new Color(1f, 1f, 1f, 0.5f);
                    fill.a *= body;
                    c = Over(fill, c);
                }
                else
                {
                    float grown = RacketCoverage(along, side, out bool _r2, 0.014f);
                    if (grown > 0f) c = Over(new Color(outline.r, outline.g, outline.b, 0.85f * grown), c);
                }
                big[y * n + x] = c;
            }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var px = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Color s = Color.clear; float a = 0f;
                for (int j = 0; j < Super; j++)
                    for (int i = 0; i < Super; i++)
                    {
                        var q = big[(y * Super + j) * n + x * Super + i];
                        s += new Color(q.r * q.a, q.g * q.a, q.b * q.a, q.a);
                        a += q.a;
                    }
                float cnt = Super * Super;
                px[y * Size + x] = a > 0f ? new Color(s.r / a, s.g / a, s.b / a, a / cnt) : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(Path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(Path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.maxTextureSize = 256;
        imp.textureCompression = TextureImporterCompression.Compressed;
        imp.crunchedCompression = true;
        imp.compressionQuality = 90;
        imp.SaveAndReimport();
        Debug.Log("[HitIcon] " + Path + " drawn");
    }

    // 1 inside the racket (handle, throat or head), 0 outside; `rim` tells the frame / handle from the strings area. `grow` widens the shape.
    static float RacketCoverage(float along, float side, out bool rim, float grow = 0f)
    {
        rim = true;
        // handle: from 0.02 to 0.2 along the axis, 0.022 each side
        float handle = Capsule(along, side, 0.02f - grow, 0.21f + grow, 0.024f + grow);
        // head: an ellipse centred 0.385 along, 0.17 long and 0.135 wide, with a frame 0.03 thick and holes in the middle
        float ha = 0.17f + grow, hb = 0.135f + grow;
        float e = Mathf.Sqrt(Mathf.Pow((along - 0.385f) / ha, 2f) + Mathf.Pow(side / hb, 2f));
        float head = e <= 1f ? 1f : 0f;
        float inner = Mathf.Sqrt(Mathf.Pow((along - 0.385f) / (0.17f - 0.032f), 2f) + Mathf.Pow(side / (0.135f - 0.032f), 2f));
        bool strings = head > 0f && inner < 1f && grow == 0f;
        // throat: a narrow wedge joining handle and head
        float throat = (along > 0.19f && along < 0.25f && Mathf.Abs(side) < Mathf.Lerp(0.024f, 0.05f, Mathf.InverseLerp(0.19f, 0.25f, along)) + grow) ? 1f : 0f;
        if (strings)
        {
            // holes of a beach tennis racket: a grid of small round holes, see-through
            float gx = Mathf.Repeat(along - 0.385f + 0.5f, 0.05f) - 0.025f, gy = Mathf.Repeat(side + 0.5f, 0.05f) - 0.025f;
            bool hole = gx * gx + gy * gy < 0.0135f * 0.0135f;
            rim = false;
            return hole ? 0f : 1f;
        }
        return Mathf.Max(handle, Mathf.Max(head, throat));
    }

    // How much of the swing's trail covers p (0..1): an arc round `m`, thick and bright next to the head and fading away behind it.
    static float TrailAlpha(Vector2 p, Vector2 m)
    {
        Vector2 d = p - m;
        float r = d.magnitude, ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float t = Mathf.InverseLerp(36f, 128f, ang);            // 0 next to the head ... 1 at the far end of the trail
        if (t <= 0f || t >= 1f) return 0f;
        float half = Mathf.Lerp(0.105f, 0f, Mathf.Pow(t, 0.75f));
        float band = 1f - Smooth(half * 0.55f, half, Mathf.Abs(r - 0.385f));
        float a = band * Mathf.Lerp(1f, 0f, Mathf.Pow(t, 1.05f));
        // a thinner streak inside, shorter
        float t2 = Mathf.InverseLerp(40f, 100f, ang);
        if (t2 > 0f && t2 < 1f)
        {
            float half2 = Mathf.Lerp(0.02f, 0f, t2);
            a = Mathf.Max(a, (1f - Smooth(half2 * 0.5f, half2, Mathf.Abs(r - 0.29f))) * Mathf.Lerp(0.9f, 0f, t2));
        }
        return a;
    }

    // Like the shader function: 0 below e0, 1 above e1, smooth between (Mathf.SmoothStep is something else: it blends two values).
    static float Smooth(float e0, float e1, float x)
    {
        if (e1 <= e0) return x >= e1 ? 1f : 0f;
        float t = Mathf.Clamp01((x - e0) / (e1 - e0));
        return t * t * (3f - 2f * t);
    }

    static float Capsule(float along, float side, float a, float b, float radius)
    {
        float cx = Mathf.Clamp(along, a, b);
        return new Vector2(along - cx, side).magnitude <= radius ? 1f : 0f;
    }

    static Color Over(Color top, Color bottom)
    {
        float a = top.a + bottom.a * (1f - top.a);
        if (a <= 0f) return Color.clear;
        Color c = (top * top.a + bottom * bottom.a * (1f - top.a)) / a;
        c.a = a;
        return c;
    }
}
