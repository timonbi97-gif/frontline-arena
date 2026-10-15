using UnityEngine;

public class GunParts
{
    public Transform root, muzzle, eject, mag;
    public Vector3 grip, fore, sight;   // local points: firing hand, support hand, sight line
    public float eyeDist = 0.24f;       // eye relief for ADS
}

// Builds realistic-proportioned gun models from a WeaponDef (+Z = forward, origin near the grip).
public static class GunBuilder
{
    static int L;
    static bool shadows;

    static Material M(Col c)
    {
        switch (c)
        {
            case Col.Steel: return GA.Lit(new Color(0.2f, 0.21f, 0.23f), 0.65f);
            case Col.Tan: return GA.Lit(new Color(0.55f, 0.47f, 0.33f), 0.2f);
            case Col.Olive: return GA.Lit(new Color(0.25f, 0.28f, 0.18f), 0.2f);
            case Col.Wood: return GA.Textured(TexGen.GunWood(), new Color(0.75f, 0.5f, 0.32f), 0.35f);
            default: return GA.Lit(new Color(0.07f, 0.07f, 0.075f), 0.35f);
        }
    }

    static GameObject B(Transform p, PrimitiveType t, Vector3 pos, Vector3 s, Material m, Quaternion? r = null)
    {
        var g = GA.Prim(t, p, pos, s, m, false, L, shadows);
        if (r.HasValue) g.transform.localRotation = r.Value;
        return g;
    }

    // Cylinder along local Z.
    static GameObject Cyl(Transform p, Vector3 center, float radius, float length, Material m)
        => B(p, PrimitiveType.Cylinder, center, new Vector3(radius * 2, length / 2, radius * 2), m, Quaternion.Euler(90, 0, 0));

    static Transform Point(Transform p, Vector3 pos)
    {
        var t = new GameObject("Pt").transform;
        t.SetParent(p, false);
        t.localPosition = pos;
        t.gameObject.layer = L;
        return t;
    }

    public static GunParts Build(WeaponDef d, Transform parent, int layer, bool viewModel)
    {
        L = layer;
        shadows = !viewModel;
        var parts = new GunParts { root = new GameObject(d.name).transform };
        parts.root.SetParent(parent, false);
        parts.root.gameObject.layer = layer;
        if (d.cat == WCat.Pistol) BuildPistol(d, parts);
        else if (d.cat == WCat.Launcher) BuildLauncher(d, parts);
        else BuildLong(d, parts);
        return parts;
    }

    // ------------------------------------------------------------ optics

    static void Optic(WeaponDef d, GunParts g, float railTop, float z, float rearIronZ, float frontIronZ)
    {
        var r = g.root;
        var dark = M(Col.Black);
        var red = GA.Emissive(new Color(1f, 0.05f, 0.03f), 8f);
        switch (d.sight)
        {
            case Sight.Iron:
                {
                    float y = railTop + 0.012f;
                    B(r, PrimitiveType.Cube, new Vector3(-0.0065f, y, rearIronZ), new Vector3(0.005f, 0.02f, 0.008f), dark);
                    B(r, PrimitiveType.Cube, new Vector3(0.0065f, y, rearIronZ), new Vector3(0.005f, 0.02f, 0.008f), dark);
                    B(r, PrimitiveType.Cube, new Vector3(0, railTop + 0.003f, rearIronZ), new Vector3(0.02f, 0.006f, 0.01f), dark);
                    B(r, PrimitiveType.Cube, new Vector3(0, y - 0.002f, frontIronZ), new Vector3(0.0028f, 0.02f, 0.004f), dark);
                    B(r, PrimitiveType.Cube, new Vector3(0, railTop - 0.004f, frontIronZ), new Vector3(0.016f, 0.012f, 0.012f), dark);
                    g.sight = new Vector3(0, y + 0.006f, rearIronZ);
                    g.eyeDist = 0.13f;
                    break;
                }
            case Sight.RedDot:
                {
                    float cy = railTop + 0.032f;
                    B(r, PrimitiveType.Cube, new Vector3(0, railTop + 0.007f, z), new Vector3(0.026f, 0.014f, 0.04f), dark);
                    const int n = 12;
                    const float rad = 0.017f;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i / (float)n * Mathf.PI * 2;
                        B(r, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * rad, cy + Mathf.Sin(a) * rad, z), new Vector3(0.0075f, 0.0075f, 0.03f), dark,
                            Quaternion.Euler(0, 0, a * Mathf.Rad2Deg));
                    }
                    B(r, PrimitiveType.Sphere, new Vector3(0, cy, z + 0.012f), Vector3.one * 0.0016f, red);
                    g.sight = new Vector3(0, cy, z);
                    g.eyeDist = 0.25f;
                    break;
                }
            case Sight.Holo:
                {
                    float cy = railTop + 0.036f;
                    B(r, PrimitiveType.Cube, new Vector3(0, railTop + 0.009f, z), new Vector3(0.036f, 0.018f, 0.075f), dark);
                    B(r, PrimitiveType.Cube, new Vector3(-0.021f, cy, z + 0.02f), new Vector3(0.005f, 0.042f, 0.03f), dark);
                    B(r, PrimitiveType.Cube, new Vector3(0.021f, cy, z + 0.02f), new Vector3(0.005f, 0.042f, 0.03f), dark);
                    B(r, PrimitiveType.Cube, new Vector3(0, cy + 0.023f, z + 0.02f), new Vector3(0.047f, 0.005f, 0.032f), dark);
                    const int n = 16;
                    for (int i = 0; i < n; i++)
                    {
                        float a = i / (float)n * Mathf.PI * 2;
                        B(r, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.0058f, cy + Mathf.Sin(a) * 0.0058f, z + 0.03f), Vector3.one * 0.0009f, red);
                    }
                    B(r, PrimitiveType.Cube, new Vector3(0, cy, z + 0.03f), Vector3.one * 0.0009f, red);
                    g.sight = new Vector3(0, cy, z + 0.03f);
                    g.eyeDist = 0.25f;
                    break;
                }
            case Sight.Acog:
                {
                    float cy = railTop + 0.03f;
                    B(r, PrimitiveType.Cube, new Vector3(0, railTop + 0.008f, z), new Vector3(0.024f, 0.016f, 0.06f), dark);
                    Cyl(r, new Vector3(0, cy, z), 0.019f, 0.11f, dark);
                    Cyl(r, new Vector3(0, cy, z + 0.065f), 0.022f, 0.025f, dark);
                    Cyl(r, new Vector3(0, cy, z - 0.06f), 0.02f, 0.02f, dark);
                    B(r, PrimitiveType.Cube, new Vector3(0, cy + 0.0205f, z + 0.01f), new Vector3(0.004f, 0.003f, 0.03f), GA.Lit(new Color(0.5f, 0.15f, 0.05f), 0.6f));
                    g.sight = new Vector3(0, cy, z);
                    g.eyeDist = 0.12f;
                    break;
                }
            default:
                {
                    float cy = railTop + 0.04f;
                    for (int k = -1; k <= 1; k += 2)
                    {
                        B(r, PrimitiveType.Cube, new Vector3(0, railTop + 0.012f, z + k * 0.06f), new Vector3(0.024f, 0.024f, 0.018f), dark);
                        B(r, PrimitiveType.Cube, new Vector3(0, cy, z + k * 0.06f), new Vector3(0.04f, 0.04f, 0.016f), dark);
                    }
                    Cyl(r, new Vector3(0, cy, z), 0.016f, 0.26f, dark);
                    Cyl(r, new Vector3(0, cy, z + 0.16f), 0.027f, 0.08f, dark);
                    Cyl(r, new Vector3(0, cy, z - 0.15f), 0.021f, 0.06f, dark);
                    Cyl(r, new Vector3(0, cy + 0.024f, z), 0.008f, 0.02f, dark).transform.localRotation = Quaternion.identity;
                    g.sight = new Vector3(0, cy, z);
                    g.eyeDist = 0.1f;
                    break;
                }
        }
    }

    // ------------------------------------------------------------ rifles, SMGs, shotguns, LMGs

    static void BuildLong(WeaponDef d, GunParts g)
    {
        var r = g.root;
        var body = M(d.body);
        var furn = M(d.furn);
        var dark = M(Col.Black);
        bool bp = d.stock == StockT.Bullpup;
        float rl = d.rl, rh = d.rh, rw = 0.05f;
        float z0 = bp ? -0.36f : -0.09f;
        float z1 = z0 + rl;
        float hz1 = z1 + d.hl;
        float bz1 = hz1 + d.bl;

        // receiver
        B(r, PrimitiveType.Cube, new Vector3(0, 0.006f, (z0 + z1) / 2), new Vector3(rw, rh * 0.55f, rl), body);
        B(r, PrimitiveType.Cube, new Vector3(0, -rh * 0.3f, (z0 + z1) / 2 + (bp ? 0f : -0.01f)), new Vector3(rw * 0.95f, rh * 0.45f, rl * (bp ? 1f : 0.82f)), bp ? body : furn == M(Col.Wood) ? body : furn);
        float railTop = rh * 0.28f + 0.012f;
        if (d.sight != Sight.Iron || d.cat == WCat.Rifle)
            B(r, PrimitiveType.Cube, new Vector3(0, rh * 0.28f + 0.006f, (z0 + z1) / 2 + (bp ? 0.05f : 0.03f)), new Vector3(0.022f, 0.012f, rl * 0.9f), dark);
        // ejection port + charging handle
        B(r, PrimitiveType.Cube, new Vector3(rw / 2 + 0.001f, 0.01f, z0 + rl * 0.55f), new Vector3(0.002f, 0.016f, 0.05f), GA.Lit(new Color(0.02f, 0.02f, 0.02f), 0.1f));
        B(r, PrimitiveType.Cube, new Vector3(-rw / 2 - 0.006f, 0.012f, z0 + rl * 0.7f), new Vector3(0.012f, 0.01f, 0.02f), dark);

        // handguard
        if (d.hl > 0f)
        {
            float hy = d.magT == MagT.Tube ? -0.022f : -0.002f;
            float hw = d.magT == MagT.Tube ? 0.048f : rw * 1.04f;
            var hg = B(r, PrimitiveType.Cube, new Vector3(0, hy, (z1 + hz1) / 2), new Vector3(hw, rh * 0.68f, d.hl), d.magT == MagT.Tube ? furn : (d.furn == Col.Wood ? furn : dark));
            if (d.magT == MagT.Tube) g.mag = hg.transform;
            if (d.cat == WCat.Rifle || d.cat == WCat.LMG)
                for (int k = 0; k < 4; k++)
                    B(r, PrimitiveType.Cube, new Vector3(0, hy - rh * 0.34f - 0.002f, z1 + d.hl * (0.15f + k * 0.22f)), new Vector3(rw * 0.7f, 0.004f, 0.012f), dark);
        }

        // barrel(s)
        float by = 0.008f;
        if (d.dbl)
        {
            Cyl(r, new Vector3(-0.012f, by, (z1 - 0.02f + bz1) / 2), d.bt, bz1 - z1 + 0.02f, body);
            Cyl(r, new Vector3(0.012f, by, (z1 - 0.02f + bz1) / 2), d.bt, bz1 - z1 + 0.02f, body);
        }
        else Cyl(r, new Vector3(0, by, (z1 - 0.02f + bz1) / 2), d.bt * 0.5f + 0.004f, bz1 - z1 + 0.02f, dark);
        if (d.magT == MagT.Tube)
            Cyl(r, new Vector3(0, by - 0.03f, (z1 + bz1 - 0.04f) / 2), 0.011f, bz1 - z1 - 0.04f, dark);
        float muzZ = bz1;
        if (d.brake)
        {
            Cyl(r, new Vector3(0, by, bz1 + 0.025f), d.bt * 0.8f + 0.004f, 0.05f, dark);
            muzZ += 0.05f;
        }
        if (d.suppressor)
        {
            Cyl(r, new Vector3(0, by, muzZ + 0.08f), 0.018f, 0.16f, dark);
            muzZ += 0.16f;
        }
        g.muzzle = Point(r, new Vector3(0, by, muzZ + 0.01f));
        // gas block / front post base for irons
        float frontZ = d.hl > 0f ? hz1 + 0.02f : z1 + 0.02f;
        if (d.sight == Sight.Iron && !d.dbl)
            B(r, PrimitiveType.Cube, new Vector3(0, (by + railTop) / 2 - 0.004f, frontZ), new Vector3(0.014f, railTop - by + 0.006f, 0.014f), dark);

        // pistol grip + trigger guard
        float gripZ = bp ? 0.04f : 0f;
        B(r, PrimitiveType.Cube, new Vector3(0, -rh * 0.5f - 0.045f, gripZ - 0.012f), new Vector3(0.03f, 0.095f, 0.042f), d.furn == Col.Wood && d.stock == StockT.Wood && !bp ? furn : dark, Quaternion.Euler(18, 0, 0));
        B(r, PrimitiveType.Cube, new Vector3(0, -rh * 0.5f - 0.016f, gripZ + 0.04f), new Vector3(0.006f, 0.006f, 0.05f), dark);
        g.grip = new Vector3(0, -rh * 0.5f - 0.04f, gripZ - 0.012f);

        // magazine
        float magZ = bp ? -0.13f : 0.07f;
        float my = -rh * 0.5f;
        switch (d.magT)
        {
            case MagT.Straight:
                g.mag = B(r, PrimitiveType.Cube, new Vector3(0, my - 0.065f, magZ), new Vector3(0.026f, 0.13f, 0.055f), dark, Quaternion.Euler(8, 0, 0)).transform;
                break;
            case MagT.Curved:
                {
                    var m = new GameObject("Mag").transform;
                    m.SetParent(r, false); m.localPosition = new Vector3(0, my, magZ); m.gameObject.layer = L;
                    B(m, PrimitiveType.Cube, new Vector3(0, -0.045f, 0.004f), new Vector3(0.026f, 0.09f, 0.055f), d.furn == Col.Wood ? M(Col.Steel) : dark, Quaternion.Euler(10, 0, 0));
                    B(m, PrimitiveType.Cube, new Vector3(0, -0.12f, 0.03f), new Vector3(0.026f, 0.08f, 0.05f), d.furn == Col.Wood ? M(Col.Steel) : dark, Quaternion.Euler(30, 0, 0));
                    g.mag = m;
                    break;
                }
            case MagT.Box:
                g.mag = B(r, PrimitiveType.Cube, new Vector3(0.005f, my - 0.055f, magZ + 0.02f), new Vector3(0.075f, 0.1f, 0.11f), M(Col.Olive)).transform;
                B(r, PrimitiveType.Cube, new Vector3(0.04f, 0.0f, magZ + 0.02f), new Vector3(0.025f, 0.03f, 0.06f), GA.Lit(new Color(0.6f, 0.45f, 0.15f), 0.6f));
                break;
            case MagT.Top:
                g.mag = B(r, PrimitiveType.Cube, new Vector3(0, rh * 0.5f + 0.012f, (z0 + z1) / 2 - 0.02f), new Vector3(0.042f, 0.022f, rl * 0.7f), GA.Lit(new Color(0.15f, 0.13f, 0.1f), 0.7f)).transform;
                railTop = rh * 0.5f + 0.03f;
                break;
            case MagT.None:
                g.mag = B(r, PrimitiveType.Cube, new Vector3(0, -0.01f, z0 + 0.02f), new Vector3(0.03f, 0.02f, 0.02f), dark).transform;
                break;
            default:
                if (g.mag == null) g.mag = B(r, PrimitiveType.Cube, new Vector3(0, my - 0.02f, magZ), new Vector3(0.02f, 0.03f, 0.04f), dark).transform;
                break;
        }

        // stock
        switch (d.stock)
        {
            case StockT.Fixed:
                B(r, PrimitiveType.Cube, new Vector3(0, -0.012f, z0 - 0.13f), new Vector3(0.042f, 0.075f, 0.26f), d.furn == Col.Black ? dark : furn);
                B(r, PrimitiveType.Cube, new Vector3(0, -0.018f, z0 - 0.265f), new Vector3(0.044f, 0.115f, 0.02f), dark);
                B(r, PrimitiveType.Cube, new Vector3(0, 0.028f, z0 - 0.14f), new Vector3(0.036f, 0.02f, 0.14f), d.furn == Col.Black ? dark : furn);
                break;
            case StockT.Skeleton:
                B(r, PrimitiveType.Cube, new Vector3(0, 0.01f, z0 - 0.12f), new Vector3(0.022f, 0.022f, 0.24f), dark);
                B(r, PrimitiveType.Cube, new Vector3(0, -0.03f, z0 - 0.16f), new Vector3(0.012f, 0.012f, 0.18f), dark, Quaternion.Euler(-12, 0, 0));
                B(r, PrimitiveType.Cube, new Vector3(0, -0.012f, z0 - 0.245f), new Vector3(0.04f, 0.1f, 0.025f), dark);
                break;
            case StockT.Wood:
                B(r, PrimitiveType.Cube, new Vector3(0, -0.028f, z0 - 0.16f), new Vector3(0.04f, 0.085f, 0.32f), furn, Quaternion.Euler(-6, 0, 0));
                B(r, PrimitiveType.Cube, new Vector3(0, -0.048f, z0 - 0.32f), new Vector3(0.042f, 0.12f, 0.02f), dark, Quaternion.Euler(-6, 0, 0));
                break;
            case StockT.Bullpup:
                B(r, PrimitiveType.Cube, new Vector3(0, -0.01f, z0 - 0.01f), new Vector3(0.05f, rh * 1.25f, 0.025f), dark);
                B(r, PrimitiveType.Cube, new Vector3(0, rh * 0.38f, z0 + 0.1f), new Vector3(0.04f, 0.02f, 0.18f), body);
                break;
        }

        float opticZ = bp ? -0.02f : z0 + rl * 0.42f;
        Optic(d, g, railTop, opticZ, bp ? z0 + 0.12f : z0 + 0.03f, frontZ);
        g.eject = Point(r, new Vector3(rw / 2 + 0.01f, 0.01f, z0 + rl * 0.55f));
        g.fore = d.magT == MagT.Tube ? new Vector3(0, -0.045f, (z1 + hz1) / 2)
               : d.hl > 0.06f ? new Vector3(0, -0.04f, z1 + d.hl * 0.55f)
               : new Vector3(0, -0.04f, z1 + 0.04f);
    }

    // ------------------------------------------------------------ pistols

    static void BuildPistol(WeaponDef d, GunParts g)
    {
        var r = g.root;
        var body = M(d.body);
        var furn = M(d.furn == Col.Black ? Col.Black : d.furn);
        var dark = M(Col.Black);
        float sl = d.rl, sh = d.rh > 0.075f ? 0.04f : 0.033f;
        float zc = 0.035f;
        if (d.magT == MagT.Cylinder)
        {
            Cyl(r, new Vector3(0, 0.032f, zc + 0.06f), 0.011f, sl * 0.75f, body);
            B(r, PrimitiveType.Cube, new Vector3(0, 0.043f, zc + 0.06f), new Vector3(0.008f, 0.006f, sl * 0.75f), body);
            B(r, PrimitiveType.Cube, new Vector3(0, 0.02f, -0.005f), new Vector3(0.026f, 0.045f, 0.08f), body);
            g.mag = Cyl(r, new Vector3(0, 0.022f, 0.01f), 0.019f, 0.042f, body).transform;
            B(r, PrimitiveType.Cube, new Vector3(0, 0.05f, -0.045f), new Vector3(0.008f, 0.014f, 0.012f), body);
        }
        else
        {
            B(r, PrimitiveType.Cube, new Vector3(0, 0.03f, zc), new Vector3(0.03f, sh, sl), body);
            for (int k = 0; k < 4; k++)
                B(r, PrimitiveType.Cube, new Vector3(0.0155f, 0.03f, zc - sl / 2 + 0.012f + k * 0.007f), new Vector3(0.001f, sh * 0.7f, 0.003f), dark);
            B(r, PrimitiveType.Cube, new Vector3(0, 0.002f, zc - 0.005f), new Vector3(0.028f, 0.024f, sl * 0.88f), d.body == Col.Steel ? body : dark);
            Cyl(r, new Vector3(0, 0.03f, zc + sl / 2 + 0.002f), 0.006f, 0.006f, GA.Lit(new Color(0.01f, 0.01f, 0.01f), 0.2f));
            g.mag = B(r, PrimitiveType.Cube, new Vector3(0, -0.075f, -0.035f), new Vector3(0.022f, 0.05f, 0.034f), dark, Quaternion.Euler(14, 0, 0)).transform;
        }
        B(r, PrimitiveType.Cube, new Vector3(0, -0.048f, -0.025f), new Vector3(0.03f, 0.1f, 0.046f), furn, Quaternion.Euler(14, 0, 0));
        B(r, PrimitiveType.Cube, new Vector3(0, -0.016f, 0.02f), new Vector3(0.006f, 0.005f, 0.04f), dark);
        float top = d.magT == MagT.Cylinder ? 0.046f : 0.03f + sh / 2;
        float rear = d.magT == MagT.Cylinder ? -0.04f : zc - sl / 2 + 0.008f;
        float front = d.magT == MagT.Cylinder ? zc + 0.06f + sl * 0.37f : zc + sl / 2 - 0.008f;
        float y = top + 0.006f;
        B(r, PrimitiveType.Cube, new Vector3(-0.0055f, y, rear), new Vector3(0.0045f, 0.012f, 0.006f), dark);
        B(r, PrimitiveType.Cube, new Vector3(0.0055f, y, rear), new Vector3(0.0045f, 0.012f, 0.006f), dark);
        B(r, PrimitiveType.Cube, new Vector3(0, y, front), new Vector3(0.003f, 0.012f, 0.005f), dark);
        g.sight = new Vector3(0, y + 0.004f, rear);
        g.eyeDist = 0.4f;
        g.muzzle = Point(r, new Vector3(0, 0.03f, (d.magT == MagT.Cylinder ? zc + 0.06f + sl * 0.38f : zc + sl / 2) + 0.01f));
        g.eject = Point(r, new Vector3(0.018f, 0.04f, zc));
        g.grip = new Vector3(0, -0.05f, -0.025f);
        g.fore = new Vector3(-0.012f, -0.065f, -0.015f);
    }

    // ------------------------------------------------------------ launchers

    static void BuildLauncher(WeaponDef d, GunParts g)
    {
        var r = g.root;
        var body = M(d.body);
        var dark = M(Col.Black);
        if (d.rocket)
        {
            Cyl(r, new Vector3(0, 0.04f, 0.1f), 0.033f, 0.9f, body);
            Cyl(r, new Vector3(0, 0.04f, 0.02f), 0.042f, 0.24f, M(Col.Wood));
            Cyl(r, new Vector3(0, 0.04f, -0.37f), 0.045f, 0.06f, dark);
            var war = new GameObject("Warhead").transform;
            war.SetParent(r, false); war.localPosition = new Vector3(0, 0.04f, 0.55f); war.gameObject.layer = L;
            Cyl(war, new Vector3(0, 0, 0.0f), 0.022f, 0.1f, M(Col.Olive));
            B(war, PrimitiveType.Sphere, new Vector3(0, 0, 0.1f), new Vector3(0.085f, 0.085f, 0.13f), M(Col.Olive));
            var cone = GA.MeshObj(TexGen.Cone(), war, new Vector3(0, 0, 0.15f), new Vector3(0.042f, 0.12f, 0.042f), M(Col.Olive), false, L, shadows);
            cone.transform.localRotation = Quaternion.Euler(90, 0, 0);
            g.mag = war;
            B(r, PrimitiveType.Cube, new Vector3(0, -0.035f, 0.0f), new Vector3(0.03f, 0.09f, 0.045f), dark, Quaternion.Euler(14, 0, 0));
            B(r, PrimitiveType.Cube, new Vector3(0, -0.035f, 0.2f), new Vector3(0.03f, 0.08f, 0.04f), M(Col.Wood));
            // side optic (PGO-7 style); irons on top
            B(r, PrimitiveType.Cube, new Vector3(-0.045f, 0.075f, 0.02f), new Vector3(0.02f, 0.045f, 0.09f), dark);
            float y = 0.04f + 0.033f;
            B(r, PrimitiveType.Cube, new Vector3(-0.007f, y + 0.012f, -0.06f), new Vector3(0.005f, 0.024f, 0.008f), dark);
            B(r, PrimitiveType.Cube, new Vector3(0.007f, y + 0.012f, -0.06f), new Vector3(0.005f, 0.024f, 0.008f), dark);
            B(r, PrimitiveType.Cube, new Vector3(0, y + 0.012f, 0.4f), new Vector3(0.003f, 0.024f, 0.005f), dark);
            g.sight = new Vector3(0, y + 0.02f, -0.06f);
            g.eyeDist = 0.15f;
            g.muzzle = Point(r, new Vector3(0, 0.04f, 0.56f));
            g.eject = Point(r, new Vector3(0, 0.04f, -0.4f));
            g.grip = new Vector3(0, -0.04f, 0f);
            g.fore = new Vector3(0, -0.04f, 0.2f);
        }
        else
        {
            // M32 revolving grenade launcher
            var drum = new GameObject("Drum").transform;
            drum.SetParent(r, false); drum.localPosition = new Vector3(0, 0.0f, 0.08f); drum.gameObject.layer = L;
            Cyl(drum, Vector3.zero, 0.058f, 0.13f, body);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2;
                Cyl(drum, new Vector3(Mathf.Cos(a) * 0.04f, Mathf.Sin(a) * 0.04f, 0.066f), 0.017f, 0.004f, GA.Lit(new Color(0.02f, 0.02f, 0.02f), 0.1f));
            }
            g.mag = drum;
            Cyl(r, new Vector3(0, 0.04f, 0.3f), 0.024f, 0.32f, dark);
            B(r, PrimitiveType.Cube, new Vector3(0, 0.04f, 0.06f), new Vector3(0.05f, 0.05f, 0.22f), dark);
            B(r, PrimitiveType.Cube, new Vector3(0, -0.06f, -0.03f), new Vector3(0.03f, 0.1f, 0.042f), dark, Quaternion.Euler(16, 0, 0));
            B(r, PrimitiveType.Cube, new Vector3(0, -0.02f, 0.3f), new Vector3(0.03f, 0.08f, 0.04f), dark);
            B(r, PrimitiveType.Cube, new Vector3(0, 0.035f, -0.18f), new Vector3(0.022f, 0.022f, 0.22f), dark);
            B(r, PrimitiveType.Cube, new Vector3(0, 0.0f, -0.29f), new Vector3(0.04f, 0.1f, 0.025f), dark);
            float railTop = 0.04f + 0.03f;
            B(r, PrimitiveType.Cube, new Vector3(0, railTop - 0.004f, 0.1f), new Vector3(0.022f, 0.01f, 0.2f), dark);
            Optic(d, g, railTop, 0.04f, 0f, 0f);
            g.muzzle = Point(r, new Vector3(0, 0.04f, 0.47f));
            g.eject = Point(r, new Vector3(0.05f, 0.0f, 0.08f));
            g.grip = new Vector3(0, -0.055f, -0.03f);
            g.fore = new Vector3(0, -0.04f, 0.3f);
        }
    }
}
