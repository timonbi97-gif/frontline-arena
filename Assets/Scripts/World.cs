using System.Collections.Generic;
using UnityEngine;

// Procedurally builds the 400x400 m map and a navigation grid for the enemy AI.
public class World : MonoBehaviour
{
    public static World I;
    public const float Half = 200f, Cell = 2f;
    public const int N = 200;

    public const byte KGrass = 0, KRoad = 1, KBuilding = 2, KTree = 3, KRock = 4, KWater = 5, KSand = 6,
        KConcrete = 7, KDirt = 8, KWall = 9, KProp = 10, KDarkGrass = 11;

    static readonly Color[] KindColors =
    {
        new Color(0.36f, 0.52f, 0.27f), new Color(0.2f, 0.2f, 0.22f), new Color(0.75f, 0.72f, 0.67f), new Color(0.1f, 0.28f, 0.1f),
        new Color(0.5f, 0.48f, 0.45f), new Color(0.22f, 0.45f, 0.65f), new Color(0.82f, 0.72f, 0.5f), new Color(0.48f, 0.48f, 0.5f),
        new Color(0.45f, 0.33f, 0.2f), new Color(0.1f, 0.1f, 0.1f), new Color(0.62f, 0.44f, 0.26f), new Color(0.25f, 0.4f, 0.2f)
    };

    static readonly int[] BuildingCols = { 0xb8b2a6, 0x9c8f80, 0xc9c1b0, 0x8a8f96, 0xa66e4e, 0xd4cbb8, 0x7d8580, 0xb59a7a };
    static readonly int[] GlassCols = { 0x2e3f52, 0x34495e, 0x263845, 0x3d5566 };
    static readonly int[] CarCols = { 0xb03030, 0x2a5caa, 0xe0e0e0, 0x222222, 0xd9a520, 0x3c7a3c };
    static readonly int[] ContainerCols = { 0xb5432f, 0x2f6fb5, 0x3b8a4a, 0xd19a2a, 0x7a4a8a, 0x8a8a8a };
    static readonly int[] LeafCols = { 0x3f7a2e, 0x4c8a34, 0x2f6a2a, 0x5a8f3a };
    static readonly int[] PineCols = { 0x24502a, 0x2c5e30 };
    static readonly int[] RockCols = { 0x77736c, 0x8a857c, 0x6a675f };
    static readonly int[] SandRockCols = { 0xb08a5a, 0x9c7a50, 0xa5835a };

    public readonly bool[,] blocked = new bool[N, N];
    readonly bool[,] reserved = new bool[N, N];
    public readonly bool[,] reachable = new bool[N, N];
    readonly byte[,] kind = new byte[N, N];
    public Texture2D mapTex;
    public readonly List<Vector3> spawnSpots = new List<Vector3>();
    public readonly List<Vector3> pickupSpots = new List<Vector3>();
    public readonly List<Vector3> weaponSpots = new List<Vector3>();
    public readonly List<int> weaponIds = new List<int>();
    public Vector3 playerSpawn = new Vector3(0f, 0.2f, -6f);

    Transform root;
    System.Random r;

    float Rf(float a, float b) => a + (float)r.NextDouble() * (b - a);
    bool Chance(float p) => r.NextDouble() < p;
    Color Pick(int[] pal) => GA.C(pal[r.Next(pal.Length)]);

    public static Vector2Int ToCell(Vector3 p) => new Vector2Int(
        Mathf.Clamp(Mathf.FloorToInt((p.x + Half) / Cell), 0, N - 1),
        Mathf.Clamp(Mathf.FloorToInt((p.z + Half) / Cell), 0, N - 1));

    public static Vector3 CellCenter(int x, int z) => new Vector3(-Half + (x + 0.5f) * Cell, 0f, -Half + (z + 0.5f) * Cell);

    public bool IsFree(int x, int z) => x >= 0 && z >= 0 && x < N && z < N && !blocked[x, z];

    // ---------------------------------------------------------------- generation

    public void Generate(int seed)
    {
        I = this;
        r = new System.Random(seed);
        root = new GameObject("Map").transform;
        SetupLighting();

        GA.Prim(PrimitiveType.Cube, root, new Vector3(0, -0.5f, 0), new Vector3(Half * 2 + 30, 1, Half * 2 + 30),
            GA.Lit(KindColors[KGrass], 0.05f), true);

        Patch(0, 144, 400, 112, KindColors[KDarkGrass], KDarkGrass, 0.02f);
        Patch(0, -144, 400, 112, GA.C(0x7a7a78), KConcrete, 0.02f);
        Patch(144, 0, 112, 176, GA.C(0xd2b98a), KSand, 0.02f);
        Patch(0, 0, 176, 176, GA.C(0x8f8f8c), KConcrete, 0.02f);

        float[] lines = { -80, -40, 0, 40, 80 };
        foreach (float l in lines) { Road(l, 0, 10, 170); Road(0, l, 170, 10); }
        Road(0, 142, 9, 116); Road(0, -142, 9, 116); Road(142, 0, 116, 9); Road(-142, 0, 116, 9);
        Deco(PrimitiveType.Cube, new Vector3(0, 0.07f, 0), new Vector3(0.3f, 0.06f, 390), GA.C(0xd8b030), null, 0.1f, false);
        Deco(PrimitiveType.Cube, new Vector3(0, 0.07f, 0), new Vector3(390, 0.06f, 0.3f), GA.C(0xd8b030), null, 0.1f, false);

        Reserve(playerSpawn.x, playerSpawn.z, 6, 6);
        AddWeaponSpot(new Vector3(-160, 0, 14), 2, new Color(1f, 0.55f, 0.2f));
        AddWeaponSpot(new Vector3(14, 0, 165), 3, new Color(0.3f, 0.9f, 1f));
        AddWeaponSpot(new Vector3(-14, 0, -165), 4, new Color(1f, 0.25f, 0.25f));
        Depot(new Vector3(165, 0, -16));

        Perimeter();
        City();
        Forest();
        Industrial();
        Desert();
        Farm();

        ComputeReachable();
        CollectSpots();
        BuildMapTexture();

        StaticBatchingUtility.Combine(root.gameObject);
    }

    void SetupLighting()
    {
        var lgo = new GameObject("Sun");
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.15f;
        l.color = new Color(1f, 0.95f, 0.86f);
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.75f;
        lgo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        RenderSettings.sun = l;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.64f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.48f, 0.48f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.27f, 0.25f, 0.22f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.7f, 0.77f, 0.85f);
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 230f;
        var sky = Resources.Load<Material>("Mats/Sky");
        if (sky != null) RenderSettings.skybox = sky;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 70f;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.pixelLightCount = 4;
    }

    // ---------------------------------------------------------------- primitives

    void Patch(float cx, float cz, float sx, float sz, Color c, byte k, float y)
    {
        Deco(PrimitiveType.Cube, new Vector3(cx, y - 0.05f, cz), new Vector3(sx, 0.1f, sz), c, null, 0.05f, false);
        var a = ToCell(new Vector3(cx - sx / 2 + 0.1f, 0, cz - sz / 2 + 0.1f));
        var b = ToCell(new Vector3(cx + sx / 2 - 0.1f, 0, cz + sz / 2 - 0.1f));
        for (int x = a.x; x <= b.x; x++)
            for (int z = a.y; z <= b.y; z++)
                if (!blocked[x, z]) kind[x, z] = k;
    }

    void Road(float cx, float cz, float sx, float sz)
    {
        Patch(cx, cz, sx, sz, GA.C(0x3a3a3c), KRoad, 0.06f);
        Reserve(cx, cz, sx / 2, sz / 2);
    }

    void Reserve(float cx, float cz, float hx, float hz)
    {
        var a = ToCell(new Vector3(cx - hx, 0, cz - hz));
        var b = ToCell(new Vector3(cx + hx, 0, cz + hz));
        for (int x = a.x; x <= b.x; x++)
            for (int z = a.y; z <= b.y; z++)
                reserved[x, z] = true;
    }

    bool AreaFree(Vector3 c, float hx, float hz)
    {
        var a = ToCell(c - new Vector3(hx + 1, 0, hz + 1));
        var b = ToCell(c + new Vector3(hx + 1, 0, hz + 1));
        if (c.x - hx < -Half + 4 || c.x + hx > Half - 4 || c.z - hz < -Half + 4 || c.z + hz > Half - 4) return false;
        for (int x = a.x; x <= b.x; x++)
            for (int z = a.y; z <= b.y; z++)
                if (blocked[x, z] || reserved[x, z]) return false;
        return true;
    }

    GameObject Solid(PrimitiveType t, Vector3 p, Vector3 s, Color c, byte k, Quaternion? rot = null, float gloss = 0.1f)
    {
        var g = GA.Prim(t, root, p, s, GA.Lit(c, gloss), true);
        if (rot.HasValue) g.transform.rotation = rot.Value;
        Mark(g.GetComponent<Renderer>().bounds, k);
        return g;
    }

    GameObject Deco(PrimitiveType t, Vector3 p, Vector3 s, Color c, Quaternion? rot = null, float gloss = 0.1f, bool shadows = true)
    {
        var g = GA.Prim(t, root, p, s, GA.Lit(c, gloss), false, 0, shadows);
        if (rot.HasValue) g.transform.rotation = rot.Value;
        return g;
    }

    GameObject Glow(PrimitiveType t, Vector3 p, Vector3 s, Color c, float intensity, Quaternion? rot = null)
    {
        var g = GA.Prim(t, root, p, s, GA.Emissive(c, intensity), false, 0, false);
        if (rot.HasValue) g.transform.rotation = rot.Value;
        return g;
    }

    void Mark(Bounds b, byte k)
    {
        if (b.min.y > 1.7f || b.max.y < 0.45f) return;
        const float pad = 0.5f;
        var a = ToCell(new Vector3(b.min.x - pad, 0, b.min.z - pad));
        var c = ToCell(new Vector3(b.max.x + pad, 0, b.max.z + pad));
        for (int x = a.x; x <= c.x; x++)
            for (int z = a.y; z <= c.y; z++)
            {
                blocked[x, z] = true;
                kind[x, z] = k;
            }
    }

    bool TryPlace(float x0, float x1, float z0, float z1, float hx, float hz, int attempts, System.Action<Vector3> build)
    {
        for (int i = 0; i < attempts; i++)
        {
            var p = new Vector3(Rf(x0, x1), 0, Rf(z0, z1));
            if (!AreaFree(p, hx, hz)) continue;
            build(p);
            return true;
        }
        return false;
    }

    static void Shuffle<T>(List<T> list, System.Random r)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = r.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---------------------------------------------------------------- props

    void Tree(Vector3 p, float s)
    {
        Solid(PrimitiveType.Cylinder, p + new Vector3(0, 1.5f * s, 0), new Vector3(0.45f * s, 1.5f * s, 0.45f * s), GA.C(0x5a3e26), KTree);
        Color leaf = Pick(LeafCols);
        Deco(PrimitiveType.Sphere, p + new Vector3(0, 3.8f * s, 0), Vector3.one * 3.2f * s, leaf);
        if (Chance(0.6f))
            Deco(PrimitiveType.Sphere, p + new Vector3(0.7f * s, 4.8f * s, 0.3f * s), Vector3.one * 2.2f * s, leaf);
    }

    void Pine(Vector3 p, float s)
    {
        Solid(PrimitiveType.Cylinder, p + new Vector3(0, 1.2f * s, 0), new Vector3(0.4f * s, 1.2f * s, 0.4f * s), GA.C(0x4a3320), KTree);
        Color c = Pick(PineCols);
        Deco(PrimitiveType.Cylinder, p + new Vector3(0, 2.6f * s, 0), new Vector3(3.4f * s, 0.6f * s, 3.4f * s), c);
        Deco(PrimitiveType.Cylinder, p + new Vector3(0, 3.9f * s, 0), new Vector3(2.5f * s, 0.6f * s, 2.5f * s), c);
        Deco(PrimitiveType.Cylinder, p + new Vector3(0, 5.1f * s, 0), new Vector3(1.6f * s, 0.6f * s, 1.6f * s), c);
        Deco(PrimitiveType.Cylinder, p + new Vector3(0, 6.1f * s, 0), new Vector3(0.7f * s, 0.5f * s, 0.7f * s), c);
    }

    void Rock(Vector3 p, float s, Color c)
    {
        var size = new Vector3(Rf(1f, 1.8f) * s, Rf(0.6f, 1.2f) * s, Rf(1f, 1.8f) * s);
        Solid(PrimitiveType.Cube, p + new Vector3(0, size.y * 0.35f, 0), size, c, KRock,
            Quaternion.Euler(Rf(-15, 15), Rf(0, 360), Rf(-15, 15)), 0.05f);
    }

    void Crate(Vector3 p, float s)
    {
        float yaw = Rf(0, 90);
        Solid(PrimitiveType.Cube, p + new Vector3(0, s / 2, 0), Vector3.one * s, GA.C(0x9a7446), KProp, Quaternion.Euler(0, yaw, 0));
        if (Chance(0.3f))
            Solid(PrimitiveType.Cube, p + new Vector3(0, s + s * 0.35f, 0), Vector3.one * s * 0.7f, GA.C(0x8a6a40), KProp, Quaternion.Euler(0, yaw + Rf(-20, 20), 0));
    }

    void Car(Vector3 p, float yaw)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        Color col = Pick(CarCols);
        Solid(PrimitiveType.Cube, p + new Vector3(0, 0.75f, 0), new Vector3(2.0f, 0.9f, 4.4f), col, KProp, rot, 0.6f);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, 1.5f, -0.3f), new Vector3(1.8f, 0.65f, 2.3f), GA.C(0x1d2630), rot, 0.8f);
        var wr = rot * Quaternion.Euler(0, 0, 90);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Deco(PrimitiveType.Cylinder, p + rot * new Vector3(sx * 0.95f, 0.38f, sz * 1.4f), new Vector3(0.75f, 0.14f, 0.75f), GA.C(0x151515), wr, 0.1f, false);
    }

    void Building(float cx, float cz, float sx, float sz, float h)
    {
        Color wall = Pick(BuildingCols);
        Solid(PrimitiveType.Cube, new Vector3(cx, h / 2, cz), new Vector3(sx, h, sz), wall, KBuilding);
        Color glass = Pick(GlassCols);
        for (float y = 3f; y < h - 1.5f; y += 3.5f)
            Deco(PrimitiveType.Cube, new Vector3(cx, y, cz), new Vector3(sx + 0.12f, 1.3f, sz + 0.12f), glass, null, 0.85f, false);
        Deco(PrimitiveType.Cube, new Vector3(cx, h + 0.3f, cz), new Vector3(sx + 0.6f, 0.6f, sz + 0.6f), wall * 0.75f);
        if (Chance(0.7f))
            Deco(PrimitiveType.Cube, new Vector3(cx + Rf(-sx / 4, sx / 4), h + 1.2f, cz + Rf(-sz / 4, sz / 4)), new Vector3(3, 1.8f, 3), GA.C(0x8c8c8c));
        if (Chance(0.4f))
            Deco(PrimitiveType.Cylinder, new Vector3(cx + sx / 4, h + 4f, cz - sz / 4), new Vector3(0.15f, 4f, 0.15f), GA.C(0x444444));
        int side = r.Next(4);
        Vector3 dp; Vector3 ds;
        if (side == 0) { dp = new Vector3(cx, 1.3f, cz - sz / 2 - 0.06f); ds = new Vector3(2.4f, 2.6f, 0.15f); }
        else if (side == 1) { dp = new Vector3(cx, 1.3f, cz + sz / 2 + 0.06f); ds = new Vector3(2.4f, 2.6f, 0.15f); }
        else if (side == 2) { dp = new Vector3(cx - sx / 2 - 0.06f, 1.3f, cz); ds = new Vector3(0.15f, 2.6f, 2.4f); }
        else { dp = new Vector3(cx + sx / 2 + 0.06f, 1.3f, cz); ds = new Vector3(0.15f, 2.6f, 2.4f); }
        Deco(PrimitiveType.Cube, dp, ds, GA.C(0x3b2a1e), null, 0.2f, false);
    }

    void Gable(Vector3 p, float w, float len, float wallH, Color c, float yaw)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        float slope = 35f;
        float half = w / 2f;
        float panel = half / Mathf.Cos(slope * Mathf.Deg2Rad) + 0.5f;
        float rise = Mathf.Tan(slope * Mathf.Deg2Rad) * half;
        for (int s = -1; s <= 1; s += 2)
        {
            var pr = rot * Quaternion.Euler(0, 0, -s * slope);
            Deco(PrimitiveType.Cube, p + rot * new Vector3(s * half / 2f, wallH + rise / 2f, 0), new Vector3(panel, 0.3f, len + 0.6f), c, pr);
        }
        // gable ends
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, wallH + rise * 0.35f, 0), new Vector3(w * 0.6f, rise * 0.7f, len - 0.1f), c * 0.9f, rot);
    }

    // ---------------------------------------------------------------- zones

    void Perimeter()
    {
        Color c = GA.C(0x5a5d61);
        float t = 3f, h = 7f, e = Half - t / 2;
        Solid(PrimitiveType.Cube, new Vector3(0, h / 2, e), new Vector3(Half * 2, h, t), c, KWall);
        Solid(PrimitiveType.Cube, new Vector3(0, h / 2, -e), new Vector3(Half * 2, h, t), c, KWall);
        Solid(PrimitiveType.Cube, new Vector3(e, h / 2, 0), new Vector3(t, h, Half * 2), c, KWall);
        Solid(PrimitiveType.Cube, new Vector3(-e, h / 2, 0), new Vector3(t, h, Half * 2), c, KWall);
        for (float x = -Half; x <= Half; x += 50f)
            foreach (float z in new[] { -e, e })
            {
                Deco(PrimitiveType.Cube, new Vector3(x, h + 1f, z), new Vector3(4, 2, 4), GA.C(0x45484c));
                Deco(PrimitiveType.Cube, new Vector3(z, h + 1f, x), new Vector3(4, 2, 4), GA.C(0x45484c));
            }
    }

    void AddWeaponSpot(Vector3 p, int weapon, Color glow)
    {
        weaponSpots.Add(p);
        weaponIds.Add(weapon);
        Reserve(p.x, p.z, 6, 6);
        Solid(PrimitiveType.Cube, p + new Vector3(0, 0.15f, 0), new Vector3(3f, 0.3f, 3f), GA.C(0x2e3238), KProp);
        Glow(PrimitiveType.Cylinder, p + new Vector3(0, 0.31f, 0), new Vector3(2.4f, 0.01f, 2.4f), glow, 1.5f);
        // sandbag ring with openings
        for (int i = 0; i < 10; i++)
        {
            if (i % 5 == 0) continue;
            float a = i * 36f;
            var rot = Quaternion.Euler(0, a, 0);
            Solid(PrimitiveType.Cube, p + rot * new Vector3(0, 0.45f, 5.2f), new Vector3(2.8f, 0.9f, 0.8f), GA.C(0xb8a070), KProp, rot);
        }
    }

    void Depot(Vector3 p)
    {
        Reserve(p.x, p.z, 9, 9);
        Patch(p.x, p.z, 16, 16, GA.C(0x6e6a60), KConcrete, 0.04f);
        for (int i = 0; i < 4; i++)
        {
            var off = new Vector3(i % 2 == 0 ? -3 : 3, 0, i < 2 ? -3 : 3);
            pickupSpots.Add(p + off);
        }
        Deco(PrimitiveType.Cube, p + new Vector3(0, 4.2f, 0), new Vector3(10, 0.3f, 10), GA.C(0x4a5a3a));
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Solid(PrimitiveType.Cube, p + new Vector3(sx * 4.6f, 2.1f, sz * 4.6f), new Vector3(0.4f, 4.2f, 0.4f), GA.C(0x3a3a3a), KProp);
    }

    void City()
    {
        float[] centers = { -60, -20, 20, 60 };
        var types = new List<int> { 1, 1, 2, 3, 4 };
        while (types.Count < 16) types.Add(0);
        Shuffle(types, r);
        int k = 0;
        foreach (float bx in centers)
            foreach (float bz in centers)
            {
                switch (types[k++])
                {
                    case 0: BuildingBlock(bx, bz); break;
                    case 1: Ruin(bx, bz, 24f); break;
                    case 2: Park(bx, bz); break;
                    case 3: Parking(bx, bz); break;
                    default: Plaza(bx, bz); break;
                }
            }

        // street lamps
        foreach (float l in new float[] { -40, 0, 40 })
            for (float z = -70; z <= 70; z += 28)
                for (int side = -1; side <= 1; side += 2)
                {
                    Lamp(new Vector3(l + side * 6.3f, 0, z + 6));
                    Lamp(new Vector3(z + 6, 0, l + side * 6.3f));
                }

        // wrecked cars and barricades on the roads
        float[] lines = { -80, -40, 0, 40, 80 };
        for (int i = 0; i < 22; i++)
        {
            float l = lines[r.Next(lines.Length)];
            float along = Rf(-75, 75);
            if (Mathf.Abs(along % 40f) < 7f) continue;
            bool vertical = Chance(0.5f);
            var p = vertical ? new Vector3(l + Rf(-3, 3), 0, along) : new Vector3(along, 0, l + Rf(-3, 3));
            if (Mathf.Abs(p.x) < 10 && Mathf.Abs(p.z) < 14) continue;
            if (Chance(0.7f)) Car(p, (vertical ? 0 : 90) + Rf(-25, 25));
            else
                for (int j = 0; j < 3; j++)
                    Solid(PrimitiveType.Cube, p + new Vector3(j * 1.6f - 1.6f, 0.5f, 0) , new Vector3(1.4f, 1f, 0.6f), GA.C(0xa8a49a), KProp,
                        Quaternion.Euler(0, (vertical ? 90 : 0) + Rf(-15, 15), 0));
        }
    }

    void Lamp(Vector3 p)
    {
        Solid(PrimitiveType.Cylinder, p + new Vector3(0, 3f, 0), new Vector3(0.18f, 3f, 0.18f), GA.C(0x3c3f44), KProp);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 6f, 0), new Vector3(0.25f, 0.2f, 1.4f), GA.C(0x3c3f44), null, 0.2f, false);
        Glow(PrimitiveType.Cube, p + new Vector3(0, 5.88f, 0.5f), new Vector3(0.3f, 0.06f, 0.4f), new Color(1f, 0.9f, 0.6f), 1.5f);
    }

    void BuildingBlock(float bx, float bz)
    {
        float dc = new Vector2(bx, bz).magnitude;
        float hMax = Mathf.Lerp(40f, 14f, Mathf.Clamp01(dc / 90f));
        int split = r.Next(3);
        if (split == 0) Building(bx, bz, 24, 24, Rf(10, hMax));
        else if (split == 1)
        {
            if (Chance(0.5f)) { Building(bx - 7.5f, bz, 11, 24, Rf(8, hMax)); Building(bx + 7.5f, bz, 11, 24, Rf(8, hMax)); }
            else { Building(bx, bz - 7.5f, 24, 11, Rf(8, hMax)); Building(bx, bz + 7.5f, 24, 11, Rf(8, hMax)); }
        }
        else
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    if (Chance(0.85f)) Building(bx + sx * 7.5f, bz + sz * 7.5f, 10.5f, 10.5f, Rf(7, hMax));
                    else Crate(new Vector3(bx + sx * 7.5f, 0, bz + sz * 7.5f), 1.2f);
        }
        for (int i = 0; i < 3; i++)
        {
            var p = new Vector3(bx + Rf(-14, 14), 0, bz + Rf(-14, 14));
            if (AreaFree(p, 0.6f, 0.6f)) Crate(p, Rf(0.9f, 1.3f));
        }
    }

    void Ruin(float cx, float cz, float size)
    {
        Patch(cx, cz, size + 2, size + 2, GA.C(0x6f6b64), KConcrete, 0.04f);
        Color wc = Pick(BuildingCols);
        float h = Rf(3f, 5f), t = 0.7f;
        int segs = 6;
        float seg = size / segs;
        for (int side = 0; side < 4; side++)
        {
            int door = r.Next(1, segs - 1);
            for (int i = 0; i < segs; i++)
            {
                if (i == door || Chance(0.18f)) continue;
                float off = -size / 2 + seg * (i + 0.5f);
                float hh = h * Rf(0.45f, 1f);
                Vector3 p, s;
                if (side < 2) { p = new Vector3(cx + off, hh / 2, cz + (side == 0 ? -1 : 1) * size / 2); s = new Vector3(seg + 0.02f, hh, t); }
                else { p = new Vector3(cx + (side == 2 ? -1 : 1) * size / 2, hh / 2, cz + off); s = new Vector3(t, hh, seg + 0.02f); }
                Solid(PrimitiveType.Cube, p, s, wc, KBuilding);
            }
        }
        for (int i = 0; i < 3; i++)
        {
            float len = Rf(4, 8), hh = Rf(1.2f, 3f);
            var p = new Vector3(cx + Rf(-size / 3, size / 3), hh / 2, cz + Rf(-size / 3, size / 3));
            Solid(PrimitiveType.Cube, p, Chance(0.5f) ? new Vector3(len, hh, t) : new Vector3(t, hh, len), wc * 0.9f, KBuilding);
        }
        for (int i = 0; i < 12; i++)
        {
            var p = new Vector3(cx + Rf(-size / 2 + 2, size / 2 - 2), 0, cz + Rf(-size / 2 + 2, size / 2 - 2));
            float s = Rf(0.4f, 1.3f);
            Solid(PrimitiveType.Cube, p + new Vector3(0, s * 0.3f, 0), Vector3.one * s, wc * 0.8f, KRock,
                Quaternion.Euler(Rf(0, 40), Rf(0, 360), Rf(0, 40)));
        }
    }

    void Park(float cx, float cz)
    {
        Patch(cx, cz, 28, 28, GA.C(0x4f8a3a), KGrass, 0.04f);
        float px = cx + Rf(-5, 5), pz = cz + Rf(-5, 5);
        Patch(px, pz, 10, 7, GA.C(0x3a6e94), KWater, 0.06f);
        Reserve(px, pz, 5, 3.5f);
        for (int i = 0; i < 14; i++)
        {
            var p = new Vector3(cx + Rf(-12, 12), 0, cz + Rf(-12, 12));
            if (AreaFree(p, 0.8f, 0.8f)) Tree(p, Rf(0.8f, 1.2f));
        }
        for (int i = 0; i < 4; i++)
        {
            var p = new Vector3(cx + Rf(-11, 11), 0, cz + Rf(-11, 11));
            if (!AreaFree(p, 1.2f, 0.6f)) continue;
            var rot = Quaternion.Euler(0, r.Next(4) * 90, 0);
            Solid(PrimitiveType.Cube, p + new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.12f, 0.6f), GA.C(0x7a5232), KProp, rot);
            Deco(PrimitiveType.Cube, p + rot * new Vector3(0, 0.8f, -0.3f), new Vector3(2.2f, 0.5f, 0.1f), GA.C(0x7a5232), rot);
        }
    }

    void Parking(float cx, float cz)
    {
        Patch(cx, cz, 28, 28, GA.C(0x2f2f31), KRoad, 0.04f);
        for (int row = -1; row <= 1; row += 2)
            for (int i = -2; i <= 2; i++)
            {
                var p = new Vector3(cx + i * 5f, 0, cz + row * 7f);
                Deco(PrimitiveType.Cube, new Vector3(p.x + 2.5f, 0.05f, p.z), new Vector3(0.15f, 0.02f, 5f), new Color(0.85f, 0.85f, 0.85f), null, 0.1f, false);
                if (Chance(0.7f)) Car(p, (row > 0 ? 0 : 180) + Rf(-6, 6));
            }
    }

    void Plaza(float cx, float cz)
    {
        Patch(cx, cz, 28, 28, GA.C(0xc9c2b2), KConcrete, 0.04f);
        Solid(PrimitiveType.Cube, new Vector3(cx, 0.5f, cz), new Vector3(6, 1, 6), GA.C(0x8f8a80), KBuilding);
        Solid(PrimitiveType.Cube, new Vector3(cx, 6f, cz), new Vector3(1.8f, 10, 1.8f), GA.C(0xa9a397), KBuilding);
        Deco(PrimitiveType.Sphere, new Vector3(cx, 12f, cz), Vector3.one * 2.4f, GA.C(0xc8a040), null, 0.7f);
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var p = new Vector3(cx + sx * 10, 0, cz + sz * 10);
                Solid(PrimitiveType.Cube, p + new Vector3(0, 0.4f, 0), new Vector3(2.6f, 0.8f, 2.6f), GA.C(0x8f8a80), KProp);
                Deco(PrimitiveType.Sphere, p + new Vector3(0, 1.4f, 0), Vector3.one * 2.2f, GA.C(0x3f7a2e));
            }
    }

    void Forest()
    {
        Patch(-90, 150, 50, 34, GA.C(0xc9b88a), KSand, 0.03f);
        Patch(-90, 150, 44, 28, GA.C(0x3a6e94), KWater, 0.05f);
        Reserve(-90, 150, 22, 14);

        for (int i = 0; i < 4; i++)
            TryPlace(-185, 185, 100, 188, 6, 6, 40, Cabin);
        for (int i = 0; i < 420; i++)
        {
            var p = new Vector3(Rf(-196, 196), 0, Rf(92, 196));
            if (!AreaFree(p, 0.8f, 0.8f)) continue;
            if (Chance(0.55f)) Pine(p, Rf(0.8f, 1.4f)); else Tree(p, Rf(0.8f, 1.3f));
        }
        for (int i = 0; i < 40; i++)
        {
            var p = new Vector3(Rf(-196, 196), 0, Rf(92, 196));
            if (AreaFree(p, 1.5f, 1.5f)) Rock(p, Rf(0.8f, 2.2f), Pick(RockCols));
        }
        for (int i = 0; i < 20; i++)
        {
            var p = new Vector3(Rf(-196, 196), 0, Rf(92, 196));
            if (!AreaFree(p, 2f, 2f)) continue;
            Solid(PrimitiveType.Cylinder, p + new Vector3(0, 0.35f, 0), new Vector3(0.7f, Rf(1.5f, 2.5f), 0.7f), GA.C(0x5a3e26), KTree,
                Quaternion.Euler(0, Rf(0, 360), 0) * Quaternion.Euler(90, 0, 0));
        }
    }

    void Cabin(Vector3 p)
    {
        float yaw = r.Next(4) * 90f;
        var rot = Quaternion.Euler(0, yaw, 0);
        Solid(PrimitiveType.Cube, p + new Vector3(0, 1.75f, 0), new Vector3(7, 3.5f, 6), GA.C(0x6b4a2e), KBuilding, rot);
        Gable(p, 6f, 7f, 3.5f, GA.C(0x3d2a1c), yaw + 90f);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(2f, 5f, 1f), new Vector3(0.8f, 2.5f, 0.8f), GA.C(0x6e6e6e), rot);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, 1.1f, -3.05f), new Vector3(1.2f, 2.2f, 0.1f), GA.C(0x2e1f14), rot);
    }

    void Industrial()
    {
        for (int i = 0; i < 5; i++)
            TryPlace(-180, 180, -185, -105, 15, 15, 60, p =>
            {
                bool rot = Chance(0.5f);
                var size = rot ? new Vector3(28, 9, 20) : new Vector3(20, 9, 28);
                Color c = Chance(0.5f) ? GA.C(0x7f8a8f) : GA.C(0x9a8a70);
                Solid(PrimitiveType.Cube, p + new Vector3(0, size.y / 2, 0), size, c, KBuilding);
                Deco(PrimitiveType.Cube, p + new Vector3(0, size.y + 0.4f, 0), new Vector3(size.x + 0.8f, 0.8f, size.z + 0.8f), c * 0.7f);
                var door = rot ? new Vector3(0, 3f, -size.z / 2 - 0.06f) : new Vector3(-size.x / 2 - 0.06f, 3f, 0);
                Deco(PrimitiveType.Cube, p + door, rot ? new Vector3(8, 6, 0.15f) : new Vector3(0.15f, 6, 8), GA.C(0x55595c), null, 0.3f, false);
            });
        for (int i = 0; i < 9; i++)
            TryPlace(-185, 185, -190, -100, 5, 5, 40, p =>
            {
                Solid(PrimitiveType.Cylinder, p + new Vector3(0, 4f, 0), new Vector3(8, 4, 8), GA.C(0xd8d8d0), KProp, null, 0.4f);
                Deco(PrimitiveType.Sphere, p + new Vector3(0, 8f, 0), new Vector3(8, 2, 8), GA.C(0xc8c8c0), null, 0.4f);
                Deco(PrimitiveType.Cube, p + new Vector3(0, 4f, -4.05f), new Vector3(0.6f, 8f, 0.15f), GA.C(0x555555));
            });
        for (float x = -185; x < 185; x += 9)
            for (float z = -188; z < -98; z += 14)
            {
                if (!Chance(0.4f)) continue;
                var p = new Vector3(x + Rf(-1, 1), 0, z);
                bool turned = Chance(0.3f);
                if (!AreaFree(p, turned ? 3.2f : 1.4f, turned ? 1.4f : 3.2f)) continue;
                Container(p, turned ? 90 : 0, Chance(0.3f));
            }
        for (int i = 0; i < 70; i++)
        {
            var p = new Vector3(Rf(-195, 195), 0, Rf(-195, -95));
            if (AreaFree(p, 0.7f, 0.7f)) Crate(p, Rf(0.8f, 1.4f));
        }
        for (int i = 0; i < 50; i++)
        {
            var p = new Vector3(Rf(-195, 195), 0, Rf(-195, -95));
            if (AreaFree(p, 0.4f, 0.4f))
                Solid(PrimitiveType.Cylinder, p + new Vector3(0, 0.6f, 0), new Vector3(0.8f, 0.6f, 0.8f), Chance(0.6f) ? GA.C(0xa33a2a) : GA.C(0x2a5aa3), KProp, null, 0.3f);
        }
    }

    void Container(Vector3 p, float yaw, bool stack)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        Color c = Pick(ContainerCols);
        Solid(PrimitiveType.Cube, p + new Vector3(0, 1.3f, 0), new Vector3(2.5f, 2.6f, 6.1f), c, KProp, rot, 0.3f);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 1.3f, 0) + rot * new Vector3(0, 0, 3.04f), new Vector3(2.6f, 2.7f, 0.12f), c * 0.7f, rot);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 1.3f, 0) + rot * new Vector3(0, 0, -3.04f), new Vector3(2.6f, 2.7f, 0.12f), c * 0.7f, rot);
        if (stack)
        {
            var c2 = Pick(ContainerCols);
            Solid(PrimitiveType.Cube, p + new Vector3(0, 3.9f, 0), new Vector3(2.5f, 2.6f, 6.1f), c2, KProp, rot * Quaternion.Euler(0, Rf(-6, 6), 0), 0.3f);
        }
    }

    void Desert()
    {
        for (int i = 0; i < 3; i++)
            TryPlace(95, 190, -80, 80, 5, 5, 40, p =>
            {
                float yaw = r.Next(4) * 90f; var rot = Quaternion.Euler(0, yaw, 0);
                Solid(PrimitiveType.Cube, p + new Vector3(0, 1.5f, 0), new Vector3(8, 3, 6), GA.C(0x8a8a80), KBuilding, rot);
                Deco(PrimitiveType.Cube, p + new Vector3(0, 2.2f, 0) + rot * new Vector3(0, 0, -3.02f), new Vector3(6, 0.4f, 0.1f), GA.C(0x161616), rot, 0.1f, false);
            });
        for (int i = 0; i < 12; i++)
            TryPlace(95, 190, -80, 80, 3.5f, 3.5f, 30, SandbagNest);
        for (int i = 0; i < 75; i++)
        {
            var p = new Vector3(Rf(92, 196), 0, Rf(-86, 86));
            if (AreaFree(p, 2f, 2f)) Rock(p, Rf(1.2f, 3.5f), Pick(SandRockCols));
        }
        for (int i = 0; i < 60; i++)
        {
            var p = new Vector3(Rf(92, 196), 0, Rf(-86, 86));
            if (!AreaFree(p, 0.6f, 0.6f)) continue;
            float s = Rf(0.8f, 1.3f);
            Color g = GA.C(0x4a7a3a);
            Solid(PrimitiveType.Cylinder, p + new Vector3(0, 1.5f * s, 0), new Vector3(0.6f, 1.5f, 0.6f) * s, g, KProp);
            if (Chance(0.7f))
            {
                Deco(PrimitiveType.Cylinder, p + new Vector3(0.55f * s, 1.6f * s, 0), new Vector3(0.4f, 0.35f, 0.4f) * s, g, Quaternion.Euler(0, 0, 90));
                Deco(PrimitiveType.Cylinder, p + new Vector3(0.75f * s, 2.1f * s, 0), new Vector3(0.38f, 0.5f, 0.38f) * s, g);
            }
            if (Chance(0.5f))
            {
                Deco(PrimitiveType.Cylinder, p + new Vector3(-0.5f * s, 1.2f * s, 0), new Vector3(0.38f, 0.3f, 0.38f) * s, g, Quaternion.Euler(0, 0, 90));
                Deco(PrimitiveType.Cylinder, p + new Vector3(-0.7f * s, 1.6f * s, 0), new Vector3(0.36f, 0.45f, 0.36f) * s, g);
            }
        }
        for (int i = 0; i < 15; i++)
        {
            var p = new Vector3(Rf(92, 196), 0, Rf(-86, 86));
            if (!AreaFree(p, 0.6f, 0.6f)) continue;
            Color c = GA.C(0x6b5a48);
            Solid(PrimitiveType.Cylinder, p + new Vector3(0, 2f, 0), new Vector3(0.3f, 2f, 0.3f), c, KTree);
            Deco(PrimitiveType.Cylinder, p + new Vector3(0.5f, 3f, 0), new Vector3(0.15f, 0.8f, 0.15f), c, Quaternion.Euler(0, 0, -45));
            Deco(PrimitiveType.Cylinder, p + new Vector3(-0.4f, 3.4f, 0.2f), new Vector3(0.12f, 0.6f, 0.12f), c, Quaternion.Euler(30, 0, 40));
        }
    }

    void SandbagNest(Vector3 p)
    {
        float baseYaw = Rf(0, 360);
        for (int i = 0; i < 7; i++)
        {
            float a = baseYaw + i * 32f;
            var rot = Quaternion.Euler(0, a, 0);
            Solid(PrimitiveType.Cube, p + rot * new Vector3(0, 0.45f, 3f), new Vector3(1.8f, 0.9f, 0.8f), GA.C(0xb8a070), KProp, rot);
        }
        if (Chance(0.5f)) Crate(p, 1f);
    }

    void Farm()
    {
        float[] fx = { -178, -148, -118 };
        float[] fz = { -57, 57 };
        foreach (float x in fx)
            foreach (float z in fz)
            {
                Patch(x, z, 24, 30, GA.C(0x6b4f2e), KDirt, 0.04f);
                Reserve(x, z, 12, 15);
                Color crop = Chance(0.5f) ? GA.C(0x7a9a3a) : GA.C(0xc8a845);
                for (float rx = x - 10; rx <= x + 10; rx += 2.5f)
                    Deco(PrimitiveType.Cube, new Vector3(rx, 0.3f, z), new Vector3(0.6f, 0.5f, 27f), crop, null, 0.05f, false);
            }
        for (int i = 0; i < 3; i++)
            TryPlace(-185, -100, -32, 32, 8, 8, 50, p =>
            {
                float yaw = r.Next(2) * 90f; var rot = Quaternion.Euler(0, yaw, 0);
                Solid(PrimitiveType.Cube, p + new Vector3(0, 3f, 0), new Vector3(10, 6, 14), GA.C(0x9c2f2a), KBuilding, rot);
                Gable(p, 10f, 14f, 6f, GA.C(0x4a4a4a), yaw);
                Deco(PrimitiveType.Cube, p + new Vector3(0, 2.2f, 0) + rot * new Vector3(0, 0, -7.05f), new Vector3(4f, 4.4f, 0.1f), GA.C(0xe8e0d0), rot);
            });
        for (int i = 0; i < 4; i++)
            TryPlace(-190, -95, -35, 35, 2.5f, 2.5f, 40, p =>
            {
                Solid(PrimitiveType.Cylinder, p + new Vector3(0, 6f, 0), new Vector3(4, 6, 4), GA.C(0xb8bcc0), KBuilding, null, 0.5f);
                Deco(PrimitiveType.Sphere, p + new Vector3(0, 12f, 0), new Vector3(4, 2.5f, 4), GA.C(0x9aa0a6), null, 0.5f);
            });
        for (int i = 0; i < 45; i++)
        {
            var p = new Vector3(Rf(-196, -92), 0, Rf(-86, 86));
            if (AreaFree(p, 0.8f, 0.8f))
                Solid(PrimitiveType.Cylinder, p + new Vector3(0, 0.7f, 0), new Vector3(1.4f, 0.6f, 1.4f), GA.C(0xd6b44a), KProp,
                    Quaternion.Euler(0, Rf(0, 180), 0) * Quaternion.Euler(0, 0, 90));
        }
        for (int i = 0; i < 40; i++)
        {
            var p = new Vector3(Rf(-196, -92), 0, Rf(-86, 86));
            if (AreaFree(p, 0.8f, 0.8f)) Tree(p, Rf(0.9f, 1.3f));
        }
        // fences along field edges
        foreach (float x in fx)
            foreach (float z in fz)
                for (float fzz = z - 15; fzz <= z + 15; fzz += 3f)
                {
                    if (r.NextDouble() < 0.25) continue;
                    Deco(PrimitiveType.Cube, new Vector3(x - 12.5f, 0.6f, fzz), new Vector3(0.12f, 1.2f, 0.12f), GA.C(0x7a5a3a));
                    Deco(PrimitiveType.Cube, new Vector3(x - 12.5f, 0.85f, fzz + 1.5f), new Vector3(0.08f, 0.1f, 3f), GA.C(0x7a5a3a));
                }
    }

    // ---------------------------------------------------------------- analysis

    void ComputeReachable()
    {
        var start = ToCell(playerSpawn);
        var q = new Queue<Vector2Int>();
        if (blocked[start.x, start.y]) return;
        q.Enqueue(start);
        reachable[start.x, start.y] = true;
        int[] dx = { 1, -1, 0, 0 }, dz = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nx = c.x + dx[i], nz = c.y + dz[i];
                if (!IsFree(nx, nz) || reachable[nx, nz]) continue;
                reachable[nx, nz] = true;
                q.Enqueue(new Vector2Int(nx, nz));
            }
        }
    }

    void CollectSpots()
    {
        for (int tries = 0; tries < 8000 && spawnSpots.Count < 500; tries++)
        {
            int x = r.Next(2, N - 2), z = r.Next(2, N - 2);
            if (!reachable[x, z]) continue;
            if (!IsFree(x + 1, z) || !IsFree(x - 1, z) || !IsFree(x, z + 1) || !IsFree(x, z - 1)) continue;
            spawnSpots.Add(CellCenter(x, z));
        }
        int fixedCount = pickupSpots.Count;
        for (int tries = 0; tries < 10000 && pickupSpots.Count < fixedCount + 46; tries++)
        {
            int x = r.Next(2, N - 2), z = r.Next(2, N - 2);
            if (!reachable[x, z]) continue;
            var p = CellCenter(x, z);
            if (Vector3.Distance(p, playerSpawn) < 8f) continue;
            bool ok = true;
            foreach (var o in pickupSpots) if ((o - p).sqrMagnitude < 22f * 22f) { ok = false; break; }
            foreach (var o in weaponSpots) if ((o - p).sqrMagnitude < 12f * 12f) { ok = false; break; }
            if (ok) pickupSpots.Add(p);
        }
    }

    void BuildMapTexture()
    {
        mapTex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var px = new Color32[N * N];
        for (int z = 0; z < N; z++)
            for (int x = 0; x < N; x++)
            {
                Color c = KindColors[kind[x, z]];
                if (blocked[x, z] && kind[x, z] == KBuilding) c = Color.Lerp(c, Color.white, 0.15f);
                c.a = 1f;
                px[z * N + x] = c;
            }
        mapTex.SetPixels32(px);
        mapTex.Apply();
    }
}
