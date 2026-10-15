using System.Collections.Generic;
using UnityEngine;

public enum Surf { Grass, Dirt, Field, Sand, Asphalt, Tiles, Concrete, Plaster, Brick, Office, Residential, BrickRes, Metal, Wood, Bark, Leaves, Rock, Roof, Hay }

// Procedural, tileable textures (albedo + normal) and simple meshes. No asset files needed.
public static class TexGen
{
    public class Set { public Texture2D alb, nrm; }

    delegate Color Px(float u, float v, out float h);

    static readonly Dictionary<string, Set> sets = new Dictionary<string, Set>();
    static readonly Dictionary<int, Texture2D> camo = new Dictionary<int, Texture2D>();
    static Texture2D macro, smoke, flash, blood, hole;
    static Mesh cone, trunk;
    static readonly Mesh[] rocks = new Mesh[6];

    // ------------------------------------------------------------ noise

    static float Hash(int x, int y, int s)
    {
        unchecked
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)s * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

    static float Noise(float x, float y, int px, int py, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int xa = Mod(x0, px), xb = Mod(x0 + 1, px), ya = Mod(y0, py), yb = Mod(y0 + 1, py);
        float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    // Tileable fBm over u,v in [0,1).
    public static float Fbm(float u, float v, int fx, int fy, int oct, int seed, float gain = 0.5f)
    {
        float sum = 0, amp = 1, norm = 0;
        for (int o = 0; o < oct; o++)
        {
            sum += Noise(u * fx, v * fy, fx, fy, seed + o * 31) * amp;
            norm += amp;
            amp *= gain;
            fx *= 2; fy *= 2;
        }
        return sum / norm;
    }

    static float F(float u, float v, int f, int oct, int seed) => Fbm(u, v, f, f, oct, seed);
    static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
    static float Frac(float x) => x - Mathf.Floor(x);

    // ------------------------------------------------------------ texture builder

    static Set Make(string name, int size, float bump, Px fn)
    {
        if (sets.TryGetValue(name, out var cached) && cached.alb != null) return cached;
        var alb = new Color32[size * size];
        var h = new float[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var c = fn((x + 0.5f) / size, (y + 0.5f) / size, out float hh);
                alb[y * size + x] = c;
                h[y * size + x] = hh;
            }
        var nrm = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = h[y * size + (x + 1) % size] - h[y * size + (x - 1 + size) % size];
                float dy = h[((y + 1) % size) * size + x] - h[((y - 1 + size) % size) * size + x];
                var n = new Vector3(-dx * bump, -dy * bump, 1f).normalized;
                nrm[y * size + x] = new Color32((byte)(n.x * 127.5f + 127.5f), (byte)(n.y * 127.5f + 127.5f), (byte)(n.z * 127.5f + 127.5f), 255);
            }
        var s = new Set
        {
            alb = NewTex(size, false, alb),
            nrm = NewTex(size, true, nrm)
        };
        sets[name] = s;
        return s;
    }

    static Texture2D NewTex(int size, bool linear, Color32[] px, TextureWrapMode wrap = TextureWrapMode.Repeat)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, true, linear)
        {
            wrapMode = wrap,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 4,
            hideFlags = HideFlags.DontUnloadUnusedAsset
        };
        t.SetPixels32(px);
        t.Apply(true, true);
        return t;
    }

    // ------------------------------------------------------------ surfaces

    static Color Brick(float u, float v, int rows, int perRow, out float h)
    {
        float ry = v * rows;
        int row = Mathf.FloorToInt(ry);
        float rx = u * perRow + (row % 2 == 0 ? 0f : 0.5f);
        int col = Mathf.FloorToInt(rx);
        float fx = Frac(rx), fy = Frac(ry);
        bool mortar = fy < 0.1f || fx < 0.045f;
        float n = F(u, v, 32, 2, 77);
        if (mortar) { h = 0.15f + n * 0.1f; return C(0.6f, 0.58f, 0.54f) * (0.85f + 0.2f * n); }
        float t = Hash(Mod(col, perRow * 2), row, 5);
        h = 0.85f + n * 0.15f;
        var c = Color.Lerp(C(0.42f, 0.19f, 0.13f), C(0.62f, 0.32f, 0.21f), t) * (0.8f + 0.35f * n);
        return c;
    }

    static Color Window(float u, float v, float u0, float u1, float v0, float v1, Color wall, float wallH, out float h, bool mullion)
    {
        const float fr = 0.025f;
        bool inWin = u > u0 && u < u1 && v > v0 && v < v1;
        if (!inWin) { h = wallH; return wall; }
        bool frame = u < u0 + fr || u > u1 - fr || v < v0 + fr * 1.2f || v > v1 - fr * 1.2f || (mullion && Mathf.Abs(u - (u0 + u1) * 0.5f) < fr * 0.6f);
        if (frame) { h = 0.9f; var fc = C(0.82f, 0.82f, 0.8f); fc.a = 0.5f; return fc; }
        float t = (v - v0) / (v1 - v0);
        float n = F(u, v, 8, 2, 13);
        var g = Color.Lerp(C(0.05f, 0.07f, 0.09f), C(0.22f, 0.28f, 0.34f), t * 0.8f + n * 0.2f);
        g.a = 0.97f;
        h = 0.15f;
        return g;
    }

    public static Set Get(Surf s)
    {
        switch (s)
        {
            case Surf.Grass:
                return Make("grass", 256, 3f, (float u, float v, out float h) =>
                {
                    float g = F(u, v, 4, 4, 1), d = F(u, v, 6, 3, 2), b = F(u, v, 64, 2, 3);
                    var c = Color.Lerp(C(0.13f, 0.21f, 0.06f), C(0.3f, 0.39f, 0.12f), g);
                    c = Color.Lerp(c, C(0.4f, 0.37f, 0.18f), Mathf.SmoothStep(0.55f, 0.78f, d) * 0.55f);
                    c *= 0.72f + 0.55f * b;
                    h = b * 0.7f + g * 0.3f;
                    c.a = 0.12f;
                    return c;
                });
            case Surf.Dirt:
                return Make("dirt", 256, 4f, (float u, float v, out float h) =>
                {
                    float g = F(u, v, 4, 4, 11), p = F(u, v, 32, 2, 12);
                    var c = Color.Lerp(C(0.27f, 0.2f, 0.13f), C(0.42f, 0.33f, 0.22f), g);
                    if (p > 0.64f) c = Color.Lerp(c, C(0.55f, 0.52f, 0.48f), (p - 0.64f) * 4f);
                    h = p * 0.6f + g * 0.4f;
                    c.a = 0.1f;
                    return c;
                });
            case Surf.Field:
                return Make("field", 256, 5f, (float u, float v, out float h) =>
                {
                    float g = F(u, v, 4, 4, 21), p = F(u, v, 32, 2, 22);
                    float fur = 0.5f + 0.5f * Mathf.Sin((u + 0.02f * g) * Mathf.PI * 2f * 10f);
                    var c = Color.Lerp(C(0.22f, 0.15f, 0.09f), C(0.4f, 0.3f, 0.19f), fur * 0.6f + g * 0.4f) * (0.85f + 0.3f * p);
                    h = fur * 0.8f + p * 0.2f;
                    c.a = 0.08f;
                    return c;
                });
            case Surf.Sand:
                return Make("sand", 256, 2.5f, (float u, float v, out float h) =>
                {
                    float g = F(u, v, 4, 4, 31), p = F(u, v, 64, 2, 32);
                    float rip = 0.5f + 0.5f * Mathf.Sin((u * 0.6f + v + 0.12f * g) * Mathf.PI * 2f * 8f);
                    var c = Color.Lerp(C(0.64f, 0.52f, 0.34f), C(0.8f, 0.69f, 0.48f), g) * (0.9f + 0.18f * p);
                    h = rip * 0.5f + p * 0.5f;
                    c.a = 0.1f;
                    return c;
                });
            case Surf.Asphalt:
                return Make("asphalt", 512, 1.2f, (float u, float v, out float h) =>
                {
                    float g = F(u, v, 4, 4, 41), s = F(u, v, 128, 1, 42), cr = Mathf.Abs(F(u, v, 5, 4, 43) - 0.5f);
                    float shade = 0.11f + 0.06f * g;
                    var c = C(shade, shade, shade * 1.03f);
                    if (s > 0.72f) c *= 1.25f;
                    h = s * 0.5f + 0.5f;
                    if (cr < 0.012f) { c *= 0.45f; h = 0.1f; }
                    c.a = 0.28f + 0.1f * g;
                    return c;
                });
            case Surf.Tiles:
                return Make("tiles", 256, 4f, (float u, float v, out float h) =>
                {
                    float tu = u * 2f, tv = v * 2f, fx = Frac(tu), fy = Frac(tv);
                    bool joint = fx < 0.025f || fx > 0.975f || fy < 0.025f || fy > 0.975f;
                    float n = F(u, v, 8, 4, 51), p = F(u, v, 64, 2, 52);
                    float tint = 0.9f + 0.14f * Hash(Mathf.FloorToInt(tu), Mathf.FloorToInt(tv), 53);
                    var c = C(0.58f, 0.57f, 0.54f) * tint * (0.82f + 0.3f * n) * (0.92f + 0.12f * p);
                    h = 0.8f + p * 0.2f;
                    if (joint) { c *= 0.5f; h = 0.1f; }
                    c.a = 0.2f;
                    return c;
                });
            case Surf.Concrete:
                return Make("concrete", 256, 2.5f, (float u, float v, out float h) =>
                {
                    float n = F(u, v, 4, 4, 61), st = Fbm(u, v, 16, 2, 3, 62), p = F(u, v, 128, 1, 63);
                    var c = C(0.55f, 0.54f, 0.52f) * (0.78f + 0.35f * n) * (0.88f + 0.18f * st);
                    h = 0.7f + n * 0.3f;
                    if (p > 0.82f) { c *= 0.6f; h -= 0.3f; }
                    if (Frac(v * 2f) < 0.008f) { c *= 0.7f; h = 0.2f; }
                    c.a = 0.18f;
                    return c;
                });
            case Surf.Plaster:
                return Make("plaster", 256, 2f, (float u, float v, out float h) =>
                {
                    float n = F(u, v, 4, 4, 71), r = F(u, v, 64, 2, 72), st = Fbm(u, v, 16, 2, 3, 73);
                    var c = C(0.78f, 0.74f, 0.66f) * (0.84f + 0.22f * n) * (0.9f + 0.14f * st);
                    h = r;
                    c.a = 0.1f;
                    return c;
                });
            case Surf.Brick:
                return Make("brick", 256, 5f, (float u, float v, out float h) =>
                {
                    var c = Brick(u, v, 12, 4, out h);
                    c.a = 0.08f;
                    return c;
                });
            case Surf.Office:
                return Make("office", 256, 4f, (float u, float v, out float h) =>
                {
                    float n = F(u, v, 8, 3, 81);
                    var wall = C(0.62f, 0.62f, 0.63f) * (0.88f + 0.2f * n);
                    wall.a = 0.3f;
                    float wh = 0.7f + n * 0.1f;
                    if (Frac(v * 4f) < 0.01f) { wall *= 0.7f; wh = 0.3f; }
                    return Window(u, v, 0.03f, 0.97f, 0.3f, 0.98f, wall, wh, out h, true);
                });
            case Surf.Residential:
                return Make("residential", 256, 4f, (float u, float v, out float h) =>
                {
                    float n = F(u, v, 4, 4, 91), r = F(u, v, 64, 2, 92);
                    var wall = C(0.78f, 0.74f, 0.66f) * (0.85f + 0.22f * n);
                    wall.a = 0.1f;
                    if (u > 0.24f && u < 0.76f && v > 0.27f && v < 0.32f) { h = 1f; var sill = C(0.7f, 0.7f, 0.68f); sill.a = 0.2f; return sill; }
                    return Window(u, v, 0.28f, 0.72f, 0.32f, 0.84f, wall, 0.6f + r * 0.15f, out h, true);
                });
            case Surf.BrickRes:
                return Make("brickres", 256, 4f, (float u, float v, out float h) =>
                {
                    var wall = Brick(u, v, 14, 4, out float bh);
                    wall.a = 0.08f;
                    if (u > 0.26f && u < 0.74f && v > 0.84f && v < 0.9f) { h = 0.95f; var lin = C(0.66f, 0.64f, 0.6f); lin.a = 0.15f; return lin; }
                    if (u > 0.24f && u < 0.76f && v > 0.27f && v < 0.32f) { h = 1f; var sill = C(0.66f, 0.64f, 0.6f); sill.a = 0.15f; return sill; }
                    return Window(u, v, 0.28f, 0.72f, 0.32f, 0.84f, wall, bh * 0.8f, out h, false);
                });
            case Surf.Metal:
                return Make("metal", 256, 6f, (float u, float v, out float h) =>
                {
                    float rib = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 8f);
                    float n = F(u, v, 8, 4, 101), rs = F(u, v, 6, 4, 102), streak = Fbm(u, v, 24, 2, 3, 103);
                    var c = C(0.78f, 0.78f, 0.78f) * (0.9f + 0.12f * n) * (0.9f + 0.15f * streak);
                    float rust = Mathf.SmoothStep(0.6f, 0.75f, rs);
                    c = Color.Lerp(c, C(0.42f, 0.24f, 0.12f), rust * 0.8f);
                    h = rib;
                    c.a = Mathf.Lerp(0.5f, 0.1f, rust);
                    return c;
                });
            case Surf.Wood:
                return Make("wood", 256, 4f, (float u, float v, out float h) =>
                {
                    float py = v * 5f, fy = Frac(py);
                    float grain = Fbm(u, v, 2, 40, 4, 111);
                    float tint = 0.8f + 0.35f * Hash(Mathf.FloorToInt(py), 0, 112);
                    var c = C(0.42f, 0.3f, 0.18f) * (0.75f + 0.45f * grain) * tint;
                    h = 0.7f + grain * 0.3f;
                    if (fy < 0.04f) { c *= 0.35f; h = 0f; }
                    c.a = 0.12f;
                    return c;
                });
            case Surf.Bark:
                return Make("bark", 256, 6f, (float u, float v, out float h) =>
                {
                    float n = Fbm(u, v, 8, 2, 4, 121);
                    float ridge = Mathf.Abs(Mathf.Sin((u + 0.2f * n) * Mathf.PI * 2f * 6f));
                    var c = C(0.24f, 0.18f, 0.12f) * (0.55f + 0.65f * ridge);
                    h = ridge;
                    c.a = 0.08f;
                    return c;
                });
            case Surf.Leaves:
                return Make("leaves", 256, 5f, (float u, float v, out float h) =>
                {
                    float n = F(u, v, 16, 3, 131), m = F(u, v, 4, 3, 132);
                    var c = Color.Lerp(C(0.09f, 0.17f, 0.05f), C(0.3f, 0.43f, 0.13f), n) * (0.8f + 0.35f * m);
                    h = n;
                    c.a = 0.25f;
                    return c;
                });
            case Surf.Rock:
                return Make("rock", 256, 6f, (float u, float v, out float h) =>
                {
                    float n = F(u, v, 4, 5, 141), cr = Mathf.Abs(F(u, v, 6, 4, 142) - 0.5f), li = F(u, v, 16, 3, 143);
                    var c = C(0.46f, 0.44f, 0.41f) * (0.7f + 0.5f * n);
                    if (li > 0.68f) c = Color.Lerp(c, C(0.42f, 0.45f, 0.25f), 0.6f);
                    h = n;
                    if (cr < 0.02f) { c *= 0.5f; h -= 0.3f; }
                    c.a = 0.15f;
                    return c;
                });
            case Surf.Roof:
                return Make("roof", 256, 3f, (float u, float v, out float h) =>
                {
                    float n = F(u, v, 4, 4, 151), p = F(u, v, 128, 1, 152);
                    var c = C(0.27f, 0.27f, 0.28f) * (0.75f + 0.4f * n) * (0.8f + 0.4f * p);
                    h = p;
                    c.a = 0.1f;
                    return c;
                });
            default:
                return Make("hay", 256, 4f, (float u, float v, out float h) =>
                {
                    float n = Fbm(u, v, 4, 64, 3, 161);
                    var c = C(0.72f, 0.6f, 0.3f) * (0.65f + 0.5f * n);
                    h = n;
                    c.a = 0.08f;
                    return c;
                });
        }
    }

    // Configures a Frontline/Triplanar material for a surface type.
    public static void Apply(Material m, Surf s)
    {
        Set side = Get(s), top = side;
        var scale = new Vector4(2f, 2f, 2f, 70f);
        float gloss = 1f, bump = 1f, macroStr = 0.3f;
        switch (s)
        {
            case Surf.Grass: side = Get(Surf.Dirt); scale = new Vector4(3, 3, 3, 70); macroStr = 0.45f; break;
            case Surf.Dirt: scale = new Vector4(3, 3, 3, 60); break;
            case Surf.Field: scale = new Vector4(6, 6, 6, 60); break;
            case Surf.Sand: scale = new Vector4(4, 4, 4, 60); macroStr = 0.35f; break;
            case Surf.Asphalt: scale = new Vector4(5, 5, 5, 50); macroStr = 0.4f; bump = 0.5f; break;
            case Surf.Tiles: scale = new Vector4(2, 2, 2, 40); break;
            case Surf.Concrete: scale = new Vector4(4, 4, 4, 40); break;
            case Surf.Plaster: scale = new Vector4(3, 3, 3, 40); top = Get(Surf.Roof); break;
            case Surf.Brick: scale = new Vector4(2, 2, 2, 40); break;
            case Surf.Office: scale = new Vector4(3, 3.5f, 4, 40); top = Get(Surf.Roof); macroStr = 0.15f; break;
            case Surf.Residential: scale = new Vector4(3, 3.5f, 4, 40); top = Get(Surf.Roof); macroStr = 0.2f; break;
            case Surf.BrickRes: scale = new Vector4(3, 3.5f, 4, 40); top = Get(Surf.Roof); macroStr = 0.2f; break;
            case Surf.Metal: scale = new Vector4(2.5f, 2.5f, 2.5f, 30); break;
            case Surf.Wood: scale = new Vector4(2, 2, 2, 30); break;
            case Surf.Bark: scale = new Vector4(1.2f, 2.5f, 1.2f, 30); macroStr = 0.1f; break;
            case Surf.Leaves: scale = new Vector4(2, 2, 2, 25); macroStr = 0.5f; break;
            case Surf.Rock: scale = new Vector4(4, 4, 4, 30); break;
            case Surf.Roof: scale = new Vector4(4, 4, 4, 40); break;
            case Surf.Hay: scale = new Vector4(1.5f, 1.5f, 1.5f, 20); break;
        }
        m.SetTexture("_MainTex", side.alb);
        m.SetTexture("_BumpMap", side.nrm);
        m.SetTexture("_TopTex", top.alb);
        m.SetTexture("_TopBump", top.nrm);
        m.SetTexture("_MacroTex", Macro());
        m.SetVector("_Scale", scale);
        m.SetFloat("_Glossiness", gloss);
        m.SetFloat("_BumpScale", bump);
        m.SetFloat("_MacroStrength", macroStr);
    }

    public static Texture2D Macro()
    {
        if (macro != null) return macro;
        const int S = 256;
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = F((x + 0.5f) / S, (y + 0.5f) / S, 3, 5, 171);
                byte b = (byte)(Mathf.Clamp01(n) * 255);
                px[y * S + x] = new Color32(b, b, b, 255);
            }
        macro = NewTex(S, true, px);
        return macro;
    }

    // 0 woodland, 1 desert, 2 urban, 3 black
    public static Texture2D Camo(int kind)
    {
        if (camo.TryGetValue(kind, out var t) && t != null) return t;
        Color[][] pals =
        {
            new[] { C(0.33f, 0.36f, 0.22f), C(0.22f, 0.26f, 0.14f), C(0.36f, 0.27f, 0.17f), C(0.1f, 0.1f, 0.08f) },
            new[] { C(0.66f, 0.57f, 0.41f), C(0.55f, 0.45f, 0.3f), C(0.44f, 0.36f, 0.25f), C(0.72f, 0.65f, 0.5f) },
            new[] { C(0.48f, 0.49f, 0.5f), C(0.33f, 0.34f, 0.36f), C(0.6f, 0.6f, 0.6f), C(0.18f, 0.19f, 0.2f) },
            new[] { C(0.12f, 0.12f, 0.13f), C(0.08f, 0.08f, 0.09f), C(0.17f, 0.17f, 0.18f), C(0.05f, 0.05f, 0.05f) },
        };
        var pal = pals[Mathf.Clamp(kind, 0, 3)];
        const int S = 128;
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                var c = pal[0];
                if (F(u, v, 4, 3, 181 + kind) > 0.55f) c = pal[1];
                if (F(u, v, 4, 3, 191 + kind) > 0.6f) c = pal[2];
                if (F(u, v, 8, 2, 201 + kind) > 0.68f) c = pal[3];
                c *= 0.9f + 0.2f * F(u, v, 64, 1, 211);
                px[y * S + x] = c;
            }
        t = NewTex(S, false, px);
        camo[kind] = t;
        return t;
    }

    static Texture2D Sprite(int size, System.Func<float, float, float, float, float> alpha)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float dx = u * 2 - 1, dy = v * 2 - 1;
                float r = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                float a = Mathf.Clamp01(alpha(u, v, r, ang));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        return NewTex(size, false, px, TextureWrapMode.Clamp);
    }

    static Texture2D gunWood;

    // Fine wood grain without plank gaps (for gun furniture).
    public static Texture2D GunWood()
    {
        if (gunWood != null) return gunWood;
        const int S = 128;
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
                float grain = Fbm(u, v, 2, 24, 4, 251);
                float ring = 0.5f + 0.5f * Mathf.Sin((v * 18f + grain * 6f) * Mathf.PI);
                px[y * S + x] = C(0.55f, 0.4f, 0.26f) * (0.7f + 0.25f * ring + 0.2f * grain);
            }
        gunWood = NewTex(S, false, px);
        return gunWood;
    }

    public static Texture2D Smoke() => smoke != null ? smoke : smoke = Sprite(128, (u, v, r, a) =>
        Mathf.Pow(Mathf.Clamp01(1 - r), 1.4f) * (0.45f + 0.55f * F(u, v, 4, 4, 221)));

    public static Texture2D Flash() => flash != null ? flash : flash = Sprite(128, (u, v, r, a) =>
        Mathf.Pow(Mathf.Clamp01(1 - r), 2.5f) * 1.4f + Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 3f)), 24f) * Mathf.Clamp01(1 - r) * 0.9f);

    public static Texture2D Blood() => blood != null ? blood : blood = Sprite(128, (u, v, r, a) =>
    {
        float edge = 0.42f + 0.22f * F(Frac(a / (Mathf.PI * 2) + 1f), 0.5f, 8, 2, 231);
        float main = r < edge ? 1f : 0f;
        float drops = F(u, v, 16, 1, 232) > 0.8f && r < 0.95f ? 1f : 0f;
        return Mathf.Max(main, drops) * (0.7f + 0.3f * F(u, v, 8, 2, 233));
    });

    public static Texture2D Hole() => hole != null ? hole : hole = Sprite(64, (u, v, r, a) =>
        r < 0.22f ? 1f : Mathf.Clamp01(1f - (r - 0.22f) / 0.6f) * 0.7f * (0.6f + 0.4f * F(u, v, 8, 2, 241)));

    // ------------------------------------------------------------ meshes

    // Frustum of height 1 with base at y=0 (rTop = 0 gives a cone).
    public static Mesh Frustum(float rBottom, float rTop, int seg)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            verts.Add(d * rBottom); uvs.Add(new Vector2(i / (float)seg, 0));
            verts.Add(d * rTop + Vector3.up); uvs.Add(new Vector2(i / (float)seg, 1));
        }
        for (int i = 0; i < seg; i++)
        {
            int b = i * 2;
            tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
        }
        int c0 = verts.Count;
        verts.Add(Vector3.zero); uvs.Add(new Vector2(0.5f, 0.5f));
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2;
            verts.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * rBottom);
            uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
        }
        for (int i = 0; i < seg; i++) tris.AddRange(new[] { c0, c0 + 1 + i, c0 + 2 + i });
        var m = new Mesh { name = "Frustum" };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        m.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return m;
    }

    public static Mesh Cone() => cone != null ? cone : cone = Frustum(1f, 0f, 14);
    public static Mesh Trunk() => trunk != null ? trunk : trunk = Frustum(1f, 0.55f, 10);

    // Lumpy rock mesh (unit size, sits on y=0).
    public static Mesh Rock(int variant)
    {
        variant = Mod(variant, rocks.Length);
        if (rocks[variant] != null) return rocks[variant];
        const int lon = 14, lat = 9;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float o1 = variant * 13.7f, o2 = variant * 5.3f;
        for (int y = 0; y <= lat; y++)
        {
            float t = y / (float)lat;
            float phi = t * Mathf.PI;
            for (int x = 0; x <= lon; x++)
            {
                float th = (x % lon) / (float)lon * Mathf.PI * 2;
                var d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), -Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                float n = Mathf.PerlinNoise(d.x * 1.6f + o1, d.z * 1.6f + d.y + o2) * 0.5f
                        + Mathf.PerlinNoise(d.x * 3.5f + o2, d.y * 3.5f + d.z + o1) * 0.25f;
                var p = d * (0.75f + n * 0.6f);
                p.y = Mathf.Max(p.y, -0.25f) + 0.25f;
                verts.Add(p * 0.5f);
                uvs.Add(new Vector2(x / (float)lon, t));
            }
        }
        for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                int a = y * (lon + 1) + x, b = a + lon + 1;
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        var m = new Mesh { name = "Rock" };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        m.hideFlags = HideFlags.DontUnloadUnusedAsset;
        rocks[variant] = m;
        return m;
    }
}
