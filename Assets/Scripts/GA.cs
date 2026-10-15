using System.Collections.Generic;
using UnityEngine;

// Shared helpers: layers, materials and primitive construction.
public static class GA
{
    public const int LayerMap = 0, LayerPlayer = 8, LayerEnemy = 9;
    public const int MaskPlayerShot = (1 << LayerMap) | (1 << LayerEnemy);
    public const int MaskEnemyShot = (1 << LayerMap) | (1 << LayerPlayer);
    public const int MaskWorld = 1 << LayerMap;

    static Material litBase, emiBase, unlitBase, fxBase;
    static readonly Dictionary<(Color, float), Material> litCache = new Dictionary<(Color, float), Material>();
    static readonly Dictionary<(Color, float), Material> emiCache = new Dictionary<(Color, float), Material>();
    static readonly Dictionary<Color, Material> unlitCache = new Dictionary<Color, Material>();

    static void Init()
    {
        if (litBase == null) litBase = Resources.Load<Material>("Mats/Lit");
        if (emiBase == null) emiBase = Resources.Load<Material>("Mats/LitEmissive");
        if (unlitBase == null) unlitBase = Resources.Load<Material>("Mats/Unlit");
        if (fxBase == null) fxBase = Resources.Load<Material>("Mats/Fx");
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
