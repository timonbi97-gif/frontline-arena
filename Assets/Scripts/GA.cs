using System.Collections.Generic;
using UnityEngine;

// Shared helpers: layers, materials and primitive construction.
public static class GA
{
    public const int LayerMap = 0, LayerPlayer = 8, LayerEnemy = 9;
    public const int MaskPlayerShot = (1 << LayerMap) | (1 << LayerEnemy);
    public const int MaskEnemyShot = (1 << LayerMap) | (1 << LayerPlayer);
    public const int MaskWorld = 1 << LayerMap;

    static Material litBase, emiBase, unlitBase, fxBase, triBase;
    static readonly Dictionary<(Surf, Color), Material> triCache = new Dictionary<(Surf, Color), Material>();
    static readonly Dictionary<(int, Color, float), Material> clothCache = new Dictionary<(int, Color, float), Material>();
    static readonly Dictionary<(int, Color), Material> decalCache = new Dictionary<(int, Color), Material>();
    static readonly Dictionary<(Color, float), Material> litCache = new Dictionary<(Color, float), Material>();
    static readonly Dictionary<(Color, float), Material> emiCache = new Dictionary<(Color, float), Material>();
    static readonly Dictionary<Color, Material> unlitCache = new Dictionary<Color, Material>();

    static void Init()
    {
        if (litBase == null) litBase = Resources.Load<Material>("Mats/Lit");
        if (emiBase == null) emiBase = Resources.Load<Material>("Mats/LitEmissive");
        if (unlitBase == null) unlitBase = Resources.Load<Material>("Mats/Unlit");
        if (fxBase == null) fxBase = Resources.Load<Material>("Mats/Fx");
        if (triBase == null) triBase = Resources.Load<Material>("Mats/Triplanar");
    }

    // Textured world-space material for map geometry.
    public static Material Tri(Surf s, Color tint)
    {
        Init();
        var key = (s, tint);
        if (!triCache.TryGetValue(key, out var m) || m == null)
        {
            if (triBase == null) return Lit(tint);
            m = new Material(triBase) { color = tint, hideFlags = HideFlags.DontUnloadUnusedAsset };
            TexGen.Apply(m, s);
            triCache[key] = m;
        }
        return m;
    }

    public static Material Tri(Surf s) => Tri(s, Color.white);

    // UV-mapped camouflage cloth for characters (0 woodland, 1 desert, 2 urban, 3 black).
    public static Material Cloth(int camo, Color tint, float gloss = 0.05f)
    {
        Init();
        var key = (camo, tint, gloss);
        if (!clothCache.TryGetValue(key, out var m) || m == null)
        {
            m = new Material(litBase) { color = tint, mainTexture = TexGen.Camo(camo), hideFlags = HideFlags.DontUnloadUnusedAsset };
            m.SetFloat("_Glossiness", gloss);
            clothCache[key] = m;
        }
        return m;
    }

    static readonly Dictionary<(Texture2D, Color, float, float), Material> texCache = new Dictionary<(Texture2D, Color, float, float), Material>();

    // UV-mapped textured material (for moving objects where triplanar would swim).
    public static Material Textured(Texture2D tex, Color tint, float gloss = 0.2f, float tiling = 1f)
    {
        Init();
        var key = (tex, tint, gloss, tiling);
        if (!texCache.TryGetValue(key, out var m) || m == null)
        {
            m = new Material(litBase) { color = tint, mainTexture = tex, hideFlags = HideFlags.DontUnloadUnusedAsset };
            m.mainTextureScale = Vector2.one * tiling;
            m.SetFloat("_Glossiness", gloss);
            texCache[key] = m;
        }
        return m;
    }

    // Shared transparent decal materials (0 bullet hole, 1 blood).
    public static Material Decal(int kind, Color c)
    {
        Init();
        var key = (kind, c);
        if (!decalCache.TryGetValue(key, out var m) || m == null)
        {
            m = new Material(fxBase) { color = c, mainTexture = kind == 0 ? TexGen.Hole() : TexGen.Blood(), hideFlags = HideFlags.DontUnloadUnusedAsset };
            decalCache[key] = m;
        }
        return m;
    }

    public static GameObject MeshObj(Mesh mesh, Transform parent, Vector3 localPos, Vector3 scale, Material m,
        bool collider = false, int layer = 0, bool shadows = true)
    {
        var g = new GameObject(mesh.name);
        g.layer = layer;
        var tr = g.transform;
        if (parent != null) tr.SetParent(parent, false);
        tr.localPosition = localPos;
        tr.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (collider)
        {
            var mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
        }
        return g;
    }

    // Box stretched between two local points (for limbs, wires, beams).
    public static GameObject Segment(Transform parent, Vector3 a, Vector3 b, float thick, Material m, int layer = 0, bool shadows = true)
    {
        var d = b - a;
        var g = Prim(PrimitiveType.Cube, parent, (a + b) * 0.5f, new Vector3(thick, thick, Mathf.Max(0.001f, d.magnitude)), m, false, layer, shadows);
        if (d.sqrMagnitude > 1e-6f) g.transform.localRotation = Quaternion.LookRotation(d);
        return g;
    }

    public static Color C(int hex) =>
        new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

    public static Material Lit(Color c, float gloss = 0.1f)
    {
        Init();
        var key = (c, gloss);
        if (!litCache.TryGetValue(key, out var m) || m == null)
        {
            m = new Material(litBase) { color = c, hideFlags = HideFlags.DontUnloadUnusedAsset };
            m.SetFloat("_Glossiness", gloss);
            litCache[key] = m;
        }
        return m;
    }

    public static Material Emissive(Color c, float intensity = 2f)
    {
        Init();
        var key = (c, intensity);
        if (!emiCache.TryGetValue(key, out var m) || m == null)
        {
            m = new Material(emiBase) { color = c, hideFlags = HideFlags.DontUnloadUnusedAsset };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            emiCache[key] = m;
        }
        return m;
    }

    public static Material Unlit(Color c)
    {
        Init();
        if (!unlitCache.TryGetValue(c, out var m) || m == null)
        {
            m = new Material(unlitBase) { color = c, hideFlags = HideFlags.DontUnloadUnusedAsset };
            unlitCache[c] = m;
        }
        return m;
    }

    // Transparent, per-instance material (caller owns it).
    public static Material Fx(Color c)
    {
        Init();
        return new Material(fxBase) { color = c };
    }

    public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 scale, Material m,
        bool collider = false, int layer = 0, bool shadows = true)
    {
        var g = GameObject.CreatePrimitive(t);
        g.layer = layer;
        var tr = g.transform;
        if (parent != null) tr.SetParent(parent, false);
        tr.localPosition = localPos;
        tr.localScale = scale;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    public static Transform Pivot(Transform parent, Vector3 localPos, string name = "Pivot")
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        t.gameObject.layer = parent.gameObject.layer;
        return t;
    }
}
