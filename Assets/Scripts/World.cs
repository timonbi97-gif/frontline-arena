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
        new Color(0.36f, 0.5f, 0.27f), new Color(0.2f, 0.2f, 0.22f), new Color(0.75f, 0.72f, 0.67f), new Color(0.1f, 0.28f, 0.1f),
        new Color(0.5f, 0.48f, 0.45f), new Color(0.22f, 0.4f, 0.5f), new Color(0.8f, 0.7f, 0.5f), new Color(0.5f, 0.5f, 0.5f),
        new Color(0.45f, 0.33f, 0.2f), new Color(0.1f, 0.1f, 0.1f), new Color(0.62f, 0.44f, 0.26f), new Color(0.25f, 0.38f, 0.2f)
    };

    public readonly bool[,] blocked = new bool[N, N];
    readonly bool[,] reserved = new bool[N, N];
    public readonly bool[,] reachable = new bool[N, N];
    readonly byte[,] kind = new byte[N, N];
    public Texture2D mapTex;
    public readonly List<Vector3> spawnSpots = new List<Vector3>();
    public readonly List<Vector3> pickupSpots = new List<Vector3>();
    public readonly List<Vector3> weaponSpots = new List<Vector3>();
    public readonly List<int> weaponIds = new List<int>();
    public readonly List<Vector3> crateSpots = new List<Vector3>();
    public Vector3 playerSpawn = new Vector3(0f, 0.2f, -6f);

    Transform root;
    System.Random r;

    // materials
    Material mAsphalt, mTiles, mConcrete, mWater, mGlass, mDarkGlass, mTire, mWhitePaint, mLeaf, mLeafDark, mLeafAutumn,
        mBark, mWood, mRock, mSandRock, mSandbag, mMetal, mDarkMetal, mRoof, mCharred;
    static readonly Color[] OfficeTints = { new Color(0.85f, 0.88f, 0.92f), new Color(0.95f, 0.93f, 0.88f), new Color(0.75f, 0.78f, 0.8f) };
    static readonly Color[] ResTints = { new Color(1f, 0.95f, 0.85f), new Color(0.95f, 0.84f, 0.74f), new Color(0.9f, 0.92f, 0.95f), new Color(1f, 0.88f, 0.8f), new Color(0.85f, 0.9f, 0.8f) };
    static readonly Color[] BrickTints = { Color.white, new Color(0.85f, 0.8f, 0.78f), new Color(1.05f, 0.95f, 0.9f) };
    static readonly int[] CarCols = { 0x8c1c1c, 0x1f3f7a, 0xd0d0d0, 0x1a1a1a, 0x9a7a2a, 0x2f5a35, 0x5a5a60 };
    static readonly int[] ContainerCols = { 0xa83a28, 0x2a5f9e, 0x357a42, 0xc08a2a, 0x6a3f7a, 0x7a7a7a, 0x2a6a6a };

    float Rf(float a, float b) => a + (float)r.NextDouble() * (b - a);
    bool Chance(float p) => r.NextDouble() < p;
    T Pick<T>(T[] arr) => arr[r.Next(arr.Length)];

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

        mAsphalt = GA.Tri(Surf.Asphalt);
        mTiles = GA.Tri(Surf.Tiles);
        mConcrete = GA.Tri(Surf.Concrete);
        mWater = GA.Lit(new Color(0.04f, 0.09f, 0.1f), 0.95f);
        mGlass = GA.Lit(new Color(0.08f, 0.1f, 0.12f), 0.95f);
        mDarkGlass = GA.Lit(new Color(0.03f, 0.035f, 0.04f), 0.9f);
        mTire = GA.Lit(new Color(0.05f, 0.05f, 0.05f), 0.25f);
        mWhitePaint = GA.Lit(new Color(0.8f, 0.8f, 0.78f), 0.2f);
        mLeaf = GA.Tri(Surf.Leaves);
        mLeafDark = GA.Tri(Surf.Leaves, new Color(0.65f, 0.8f, 0.7f));
        mLeafAutumn = GA.Tri(Surf.Leaves, new Color(1.25f, 1.05f, 0.6f));
        mBark = GA.Tri(Surf.Bark);
        mWood = GA.Tri(Surf.Wood);
        mRock = GA.Tri(Surf.Rock);
        mSandRock = GA.Tri(Surf.Rock, new Color(1.25f, 1.02f, 0.78f));
        mSandbag = GA.Tri(Surf.Sand, new Color(0.85f, 0.82f, 0.72f));
        mMetal = GA.Tri(Surf.Metal, new Color(0.75f, 0.76f, 0.78f));
        mDarkMetal = GA.Lit(new Color(0.12f, 0.12f, 0.13f), 0.5f);
        mRoof = GA.Tri(Surf.Roof);
        mCharred = GA.Tri(Surf.Metal, new Color(0.16f, 0.13f, 0.11f));

        GA.Prim(PrimitiveType.Cube, root, new Vector3(0, -0.5f, 0), new Vector3(2400, 1, 2400), GA.Tri(Surf.Grass), true);

        Patch(0, 144, 400, 112, GA.Tri(Surf.Grass, new Color(0.8f, 0.92f, 0.78f)), KDarkGrass, 0.02f);
        Patch(0, -144, 400, 112, mConcrete, KConcrete, 0.02f);
        Patch(144, 0, 112, 176, GA.Tri(Surf.Sand), KSand, 0.02f);

        // city: asphalt everywhere, raised sidewalk plates per block
        Patch(0, 0, 172, 172, mAsphalt, KRoad, 0.03f);
        float[] lines = { -80, -40, 0, 40, 80 };
        foreach (float l in lines) { Reserve(l, 0, 5, 85); Reserve(0, l, 85, 5); }
        Road(0, 143, 9, 114); Road(0, -143, 9, 114); Road(143, 0, 114, 9); Road(-143, 0, 114, 9);
        RoadMarkings(lines);

        Reserve(playerSpawn.x, playerSpawn.z, 6, 6);
        AddWeaponSpot(new Vector3(-160, 0, 14), Arsenal.Find("M32 Granatwerfer"), new Color(1f, 0.55f, 0.2f));
        AddWeaponSpot(new Vector3(14, 0, 165), Arsenal.Find("Barrett M82"), new Color(0.3f, 0.9f, 1f));
        AddWeaponSpot(new Vector3(-14, 0, -165), Arsenal.Find("RPG-7"), new Color(1f, 0.25f, 0.25f));
        Depot(new Vector3(165, 0, -16));

        Perimeter();
        City(lines);
        Forest();
        Industrial();
        Desert();
        Farm();
        Mountains();

        ComputeReachable();
        CollectSpots();
        BuildMapTexture();

        StaticBatchingUtility.Combine(root.gameObject);

        var probe = new GameObject("Reflections").AddComponent<ReflectionProbe>();
        probe.transform.position = new Vector3(0, 25, 0);
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.size = new Vector3(1200, 500, 1200);
        probe.resolution = 128;
        probe.farClipPlane = 1200f;
        probe.hdr = true;
        probe.RenderProbe();
    }

    void SetupLighting()
    {
        var lgo = new GameObject("Sun");
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.35f;
        l.color = new Color(1f, 0.92f, 0.8f);
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.85f;
        l.shadowBias = 0.04f;
        l.shadowNormalBias = 0.3f;
        lgo.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
        RenderSettings.sun = l;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.58f, 0.7f);
        RenderSettings.ambientEquatorColor = new Color(0.42f, 0.43f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.19f, 0.17f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.66f, 0.73f, 0.8f);
        RenderSettings.fogDensity = 0.0026f;
        RenderSettings.reflectionIntensity = 0.8f;
        var sky = Resources.Load<Material>("Mats/Sky");
        if (sky != null) RenderSettings.skybox = sky;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 95f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.pixelLightCount = 4;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.realtimeReflectionProbes = true;
    }

    // ---------------------------------------------------------------- primitives

    // Flat box from below ground up to `top` (raised plates form curbs).
    void Patch(float cx, float cz, float sx, float sz, Material m, byte k, float top)
    {
        float h = top + 0.1f;
        GA.Prim(PrimitiveType.Cube, root, new Vector3(cx, top - h / 2, cz), new Vector3(sx, h, sz), m, top >= 0.1f, 0, false);
        var a = ToCell(new Vector3(cx - sx / 2 + 0.1f, 0, cz - sz / 2 + 0.1f));
        var b = ToCell(new Vector3(cx + sx / 2 - 0.1f, 0, cz + sz / 2 - 0.1f));
        for (int x = a.x; x <= b.x; x++)
            for (int z = a.y; z <= b.y; z++)
                if (!blocked[x, z]) kind[x, z] = k;
    }

    void Road(float cx, float cz, float sx, float sz)
    {
        Patch(cx, cz, sx + 3, sz + 3, GA.Tri(Surf.Dirt), KDirt, 0.025f);
        Patch(cx, cz, sx, sz, mAsphalt, KRoad, 0.04f);
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
        if (c.x - hx < -Half + 4 || c.x + hx > Half - 4 || c.z - hz < -Half + 4 || c.z + hz > Half - 4) return false;
        var a = ToCell(c - new Vector3(hx + 1, 0, hz + 1));
        var b = ToCell(c + new Vector3(hx + 1, 0, hz + 1));
        for (int x = a.x; x <= b.x; x++)
            for (int z = a.y; z <= b.y; z++)
                if (blocked[x, z] || reserved[x, z]) return false;
        return true;
    }

    GameObject Solid(PrimitiveType t, Vector3 p, Vector3 s, Material m, byte k, Quaternion? rot = null)
    {
        var g = GA.Prim(t, root, p, s, m, true);
        if (rot.HasValue) g.transform.rotation = rot.Value;
        Mark(g.GetComponent<Renderer>().bounds, k);
        return g;
    }

    GameObject Deco(PrimitiveType t, Vector3 p, Vector3 s, Material m, Quaternion? rot = null, bool shadows = true)
    {
        var g = GA.Prim(t, root, p, s, m, false, 0, shadows);
        if (rot.HasValue) g.transform.rotation = rot.Value;
        return g;
    }

    GameObject MeshSolid(Mesh mesh, Vector3 p, Vector3 s, Material m, byte k, Quaternion? rot = null, bool collider = true)
    {
        var g = GA.MeshObj(mesh, root, p, s, m, collider);
        if (rot.HasValue) g.transform.rotation = rot.Value;
        if (collider) Mark(g.GetComponent<Renderer>().bounds, k);
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
        var trunk = MeshSolid(TexGen.Trunk(), p, new Vector3(0.32f * s, 3.4f * s, 0.32f * s), mBark, KTree, Quaternion.Euler(0, Rf(0, 360), 0), false);
        var cap = trunk.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0, 0.5f, 0); cap.height = 1f; cap.radius = 0.7f;
        Mark(trunk.GetComponent<Renderer>().bounds, KTree);
        var m = Chance(0.15f) ? mLeafAutumn : (Chance(0.5f) ? mLeaf : mLeafDark);
        int blobs = r.Next(4, 7);
        for (int i = 0; i < blobs; i++)
        {
            float a = Rf(0, Mathf.PI * 2), d = Rf(0.2f, 1.3f) * s;
            float rad = Rf(1.4f, 2.3f) * s;
            Deco(PrimitiveType.Sphere, p + new Vector3(Mathf.Cos(a) * d, Rf(3.4f, 5.4f) * s, Mathf.Sin(a) * d),
                new Vector3(rad, rad * Rf(0.7f, 0.9f), rad), m);
        }
    }

    void Pine(Vector3 p, float s)
    {
        var trunk = MeshSolid(TexGen.Trunk(), p, new Vector3(0.28f * s, 6.5f * s, 0.28f * s), mBark, KTree, null, false);
        var cap = trunk.AddComponent<CapsuleCollider>();
        cap.center = new Vector3(0, 0.5f, 0); cap.height = 1f; cap.radius = 0.7f;
        Mark(trunk.GetComponent<Renderer>().bounds, KTree);
        int layers = r.Next(4, 6);
        for (int i = 0; i < layers; i++)
        {
            float k = 1f - i / (float)layers;
            float y = (1.6f + i * 1.25f) * s;
            float rad = (0.8f + 1.9f * k) * s;
            GA.MeshObj(TexGen.Cone(), root, p + new Vector3(0, y, 0), new Vector3(rad, 2.4f * s, rad), mLeafDark)
                .transform.rotation = Quaternion.Euler(0, Rf(0, 360), 0);
        }
    }

    void Bush(Vector3 p, float s)
    {
        var m = Chance(0.5f) ? mLeaf : mLeafDark;
        for (int i = 0; i < 3; i++)
        {
            float rad = Rf(0.7f, 1.2f) * s;
            Deco(PrimitiveType.Sphere, p + new Vector3(Rf(-0.6f, 0.6f) * s, rad * 0.35f, Rf(-0.6f, 0.6f) * s), new Vector3(rad, rad * 0.75f, rad), m);
        }
    }

    void Rock(Vector3 p, float s, Material m)
    {
        var size = new Vector3(Rf(1.2f, 2.2f), Rf(0.7f, 1.4f), Rf(1.2f, 2.2f)) * s;
        MeshSolid(TexGen.Rock(r.Next(6)), p + Vector3.down * 0.1f * s, size, m, KRock, Quaternion.Euler(Rf(-8, 8), Rf(0, 360), Rf(-8, 8)));
    }

    void Crate(Vector3 p, float s)
    {
        float yaw = Rf(0, 90);
        Solid(PrimitiveType.Cube, p + new Vector3(0, s / 2, 0), Vector3.one * s, mWood, KProp, Quaternion.Euler(0, yaw, 0));
        if (Chance(0.3f))
            Solid(PrimitiveType.Cube, p + new Vector3(0, s + s * 0.35f, 0), Vector3.one * s * 0.7f, mWood, KProp, Quaternion.Euler(0, yaw + Rf(-20, 20), 0));
    }

    void Car(Vector3 p, float yaw, bool burned = false, bool smoke = false)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        Material paint = burned ? mCharred : GA.Lit(GA.C(Pick(CarCols)), 0.75f);
        Material glass = burned ? mDarkMetal : mGlass;
        Solid(PrimitiveType.Cube, p + new Vector3(0, 0.72f, 0), new Vector3(1.85f, 0.7f, 4.3f), paint, KProp, rot);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, 1.3f, -0.25f), new Vector3(1.65f, 0.55f, 2.2f), paint, rot);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, 1.3f, -0.25f), new Vector3(1.68f, 0.42f, 2.05f), glass, rot);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, 0.55f, 2.17f), new Vector3(1.9f, 0.22f, 0.12f), mDarkMetal, rot, false);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, 0.55f, -2.17f), new Vector3(1.9f, 0.22f, 0.12f), mDarkMetal, rot, false);
        if (!burned)
        {
            Glow(PrimitiveType.Cube, p + rot * new Vector3(0.65f, 0.8f, 2.16f), new Vector3(0.35f, 0.14f, 0.04f), new Color(1f, 0.95f, 0.8f), 0.6f, rot);
            Glow(PrimitiveType.Cube, p + rot * new Vector3(-0.65f, 0.8f, 2.16f), new Vector3(0.35f, 0.14f, 0.04f), new Color(1f, 0.95f, 0.8f), 0.6f, rot);
            Glow(PrimitiveType.Cube, p + rot * new Vector3(0.7f, 0.85f, -2.16f), new Vector3(0.3f, 0.12f, 0.04f), new Color(0.8f, 0.05f, 0.05f), 0.6f, rot);
            Glow(PrimitiveType.Cube, p + rot * new Vector3(-0.7f, 0.85f, -2.16f), new Vector3(0.3f, 0.12f, 0.04f), new Color(0.8f, 0.05f, 0.05f), 0.6f, rot);
        }
        var wr = rot * Quaternion.Euler(0, 0, 90);
        float wy = burned ? 0.25f : 0.36f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Deco(PrimitiveType.Cylinder, p + rot * new Vector3(sx * 0.86f, wy, sz * 1.35f), new Vector3(0.72f, 0.12f, 0.72f), mTire, wr, false);
        if (smoke)
        {
            var sc = new GameObject("Smoke").AddComponent<SmokeColumn>();
            sc.transform.position = p + Vector3.up * 1.2f;
        }
    }

    void Building(float cx, float cz, float sx, float sz, int floors)
    {
        float h = floors * 3.5f;
        int type = r.Next(3);
        Material wall = type == 0 ? GA.Tri(Surf.Office, Pick(OfficeTints))
                      : type == 1 ? GA.Tri(Surf.Residential, Pick(ResTints))
                      : GA.Tri(Surf.BrickRes, Pick(BrickTints));
        Solid(PrimitiveType.Cube, new Vector3(cx, h / 2, cz), new Vector3(sx, h, sz), wall, KBuilding);
        // parapet
        float pt = 0.3f, ph = 0.9f, py = h + ph / 2;
        Deco(PrimitiveType.Cube, new Vector3(cx, py, cz - sz / 2 + pt / 2), new Vector3(sx, ph, pt), mConcrete);
        Deco(PrimitiveType.Cube, new Vector3(cx, py, cz + sz / 2 - pt / 2), new Vector3(sx, ph, pt), mConcrete);
        Deco(PrimitiveType.Cube, new Vector3(cx - sx / 2 + pt / 2, py, cz), new Vector3(pt, ph, sz), mConcrete);
        Deco(PrimitiveType.Cube, new Vector3(cx + sx / 2 - pt / 2, py, cz), new Vector3(pt, ph, sz), mConcrete);
        // roof clutter
        for (int i = 0; i < r.Next(1, 4); i++)
            Deco(PrimitiveType.Cube, new Vector3(cx + Rf(-sx / 3, sx / 3), h + 0.7f, cz + Rf(-sz / 3, sz / 3)), new Vector3(Rf(1.5f, 3f), 1.4f, Rf(1.2f, 2.5f)), mMetal);
        if (type != 0 && Chance(0.5f))
        {
            var tp = new Vector3(cx + Rf(-sx / 4, sx / 4), h, cz + Rf(-sz / 4, sz / 4));
            Deco(PrimitiveType.Cylinder, tp + new Vector3(0, 2.6f, 0), new Vector3(2.2f, 1.1f, 2.2f), mWood);
            for (int k = 0; k < 4; k++)
                Deco(PrimitiveType.Cube, tp + new Vector3(k % 2 == 0 ? -0.8f : 0.8f, 0.75f, k < 2 ? -0.8f : 0.8f), new Vector3(0.12f, 1.5f, 0.12f), mDarkMetal);
        }
        if (Chance(0.4f))
            Deco(PrimitiveType.Cylinder, new Vector3(cx + sx / 4, h + 4f, cz - sz / 4), new Vector3(0.1f, 4f, 0.1f), mDarkMetal);
        // shops on the ground floor
        if (type != 0 && Chance(0.6f))
        {
            Deco(PrimitiveType.Cube, new Vector3(cx, 1.55f, cz), new Vector3(sx + 0.1f, 2.6f, sz + 0.1f), mDarkGlass, null, false);
            Color sign = GA.C(Pick(new[] { 0x7a1f1f, 0x1f4a7a, 0x2a6a3a, 0x7a5a1f, 0x333333 }));
            Deco(PrimitiveType.Cube, new Vector3(cx, 3.15f, cz), new Vector3(sx + 0.16f, 0.6f, sz + 0.16f), GA.Lit(sign, 0.3f), null, false);
            if (Chance(0.6f))
                Deco(PrimitiveType.Cube, new Vector3(cx, 2.9f, cz - sz / 2 - 0.7f), new Vector3(sx * 0.6f, 0.08f, 1.4f), GA.Lit(sign * 1.4f, 0.1f), Quaternion.Euler(-12, 0, 0));
        }
        int side = r.Next(4);
        Vector3 dp, ds;
        if (side == 0) { dp = new Vector3(cx, 1.2f, cz - sz / 2 - 0.06f); ds = new Vector3(1.8f, 2.4f, 0.12f); }
        else if (side == 1) { dp = new Vector3(cx, 1.2f, cz + sz / 2 + 0.06f); ds = new Vector3(1.8f, 2.4f, 0.12f); }
        else if (side == 2) { dp = new Vector3(cx - sx / 2 - 0.06f, 1.2f, cz); ds = new Vector3(0.12f, 2.4f, 1.8f); }
        else { dp = new Vector3(cx + sx / 2 + 0.06f, 1.2f, cz); ds = new Vector3(0.12f, 2.4f, 1.8f); }
        Deco(PrimitiveType.Cube, dp, ds, mWood, null, false);
    }

    // Snap lot edges to the 3 m facade grid so windows line up with corners.
    void Lot(float x0, float z0, float x1, float z1, int maxFloors)
    {
        float sx0 = Mathf.Ceil(x0 / 3f) * 3f, sx1 = Mathf.Floor(x1 / 3f) * 3f;
        float sz0 = Mathf.Ceil(z0 / 3f) * 3f, sz1 = Mathf.Floor(z1 / 3f) * 3f;
        if (sx1 - sx0 < 6f || sz1 - sz0 < 6f) return;
        Building((sx0 + sx1) / 2, (sz0 + sz1) / 2, sx1 - sx0, sz1 - sz0, r.Next(2, Mathf.Max(3, maxFloors + 1)));
    }

    void Gable(Vector3 p, float w, float len, float wallH, Material m, float yaw)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        const float slope = 32f;
        float half = w / 2f;
        float panel = half / Mathf.Cos(slope * Mathf.Deg2Rad) + 0.5f;
        float rise = Mathf.Tan(slope * Mathf.Deg2Rad) * half;
        for (int s = -1; s <= 1; s += 2)
        {
            var pr = rot * Quaternion.Euler(0, 0, -s * slope);
            Deco(PrimitiveType.Cube, p + rot * new Vector3(s * half / 2f, wallH + rise / 2f, 0), new Vector3(panel, 0.25f, len + 0.6f), m, pr);
        }
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, wallH + rise * 0.35f, 0), new Vector3(w * 0.6f, rise * 0.7f, len - 0.1f), m);
    }

    void JerseyBarrier(Vector3 p, float yaw)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        Solid(PrimitiveType.Cube, p + new Vector3(0, 0.3f, 0), new Vector3(0.65f, 0.6f, 3f), mConcrete, KProp, rot);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 0.75f, 0), new Vector3(0.3f, 0.35f, 3f), mConcrete, rot);
    }

    void Hedgehog(Vector3 p)
    {
        float yaw = Rf(0, 90);
        var b = Quaternion.Euler(0, yaw, 0);
        Solid(PrimitiveType.Cube, p + new Vector3(0, 0.55f, 0), new Vector3(0.5f, 0.5f, 0.5f), mDarkMetal, KProp, b);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 0.6f, 0), new Vector3(0.14f, 0.14f, 2f), mDarkMetal, b * Quaternion.Euler(45, 0, 0));
        Deco(PrimitiveType.Cube, p + new Vector3(0, 0.6f, 0), new Vector3(0.14f, 0.14f, 2f), mDarkMetal, b * Quaternion.Euler(-45, 0, 0));
        Deco(PrimitiveType.Cube, p + new Vector3(0, 0.6f, 0), new Vector3(2f, 0.14f, 0.14f), mDarkMetal, b * Quaternion.Euler(0, 0, 45));
    }

    void SandbagWall(Vector3 p, float yaw, float len)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        Solid(PrimitiveType.Cube, p + new Vector3(0, 0.3f, 0), new Vector3(len, 0.6f, 0.7f), mSandbag, KProp, rot);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 0.82f, 0), new Vector3(len * 0.94f, 0.44f, 0.55f), mSandbag, rot);
    }

    // ---------------------------------------------------------------- zones

    void Perimeter()
    {
        var c = GA.Tri(Surf.Concrete, new Color(0.85f, 0.85f, 0.85f));
        float t = 1f, h = 5f, e = Half - t / 2;
        Solid(PrimitiveType.Cube, new Vector3(0, h / 2, e), new Vector3(Half * 2, h, t), c, KWall);
        Solid(PrimitiveType.Cube, new Vector3(0, h / 2, -e), new Vector3(Half * 2, h, t), c, KWall);
        Solid(PrimitiveType.Cube, new Vector3(e, h / 2, 0), new Vector3(t, h, Half * 2), c, KWall);
        Solid(PrimitiveType.Cube, new Vector3(-e, h / 2, 0), new Vector3(t, h, Half * 2), c, KWall);
        // barbed wire coil on top
        var wire = GA.Lit(new Color(0.25f, 0.25f, 0.25f), 0.6f);
        foreach (float z in new[] { -e, e })
        {
            Deco(PrimitiveType.Cylinder, new Vector3(0, h + 0.3f, z), new Vector3(0.6f, Half, 0.6f), wire, Quaternion.Euler(0, 0, 90), false);
            Deco(PrimitiveType.Cylinder, new Vector3(z, h + 0.3f, 0), new Vector3(0.6f, Half, 0.6f), wire, Quaternion.Euler(90, 0, 0), false);
        }
        // watch towers
        foreach (var cp in new[] { new Vector3(-190, 0, -190), new Vector3(190, 0, -190), new Vector3(-190, 0, 190), new Vector3(190, 0, 190) })
        {
            for (int k = 0; k < 4; k++)
                Solid(PrimitiveType.Cube, cp + new Vector3(k % 2 == 0 ? -1.5f : 1.5f, 4f, k < 2 ? -1.5f : 1.5f), new Vector3(0.3f, 8f, 0.3f), mWood, KProp);
            Deco(PrimitiveType.Cube, cp + new Vector3(0, 8.2f, 0), new Vector3(4.2f, 0.25f, 4.2f), mWood);
            Deco(PrimitiveType.Cube, cp + new Vector3(0, 8.8f, 0), new Vector3(4.2f, 1f, 4.2f), mWood);
            Deco(PrimitiveType.Cube, cp + new Vector3(0, 10.6f, 0), new Vector3(4.8f, 0.2f, 4.8f), mRoof);
        }
    }

    void AddWeaponSpot(Vector3 p, int weapon, Color glow)
    {
        weaponSpots.Add(p);
        weaponIds.Add(weapon);
        Reserve(p.x, p.z, 6, 6);
        Solid(PrimitiveType.Cube, p + new Vector3(0, 0.15f, 0), new Vector3(3.2f, 0.3f, 3.2f), mConcrete, KProp);
        Solid(PrimitiveType.Cube, p + new Vector3(0, 0.55f, 0), new Vector3(1.4f, 0.5f, 0.7f), GA.Tri(Surf.Wood, new Color(0.7f, 0.8f, 0.6f)), KProp);
        Glow(PrimitiveType.Cube, p + new Vector3(0, 0.81f, 0), new Vector3(1.0f, 0.02f, 0.2f), glow, 1.2f);
        for (int i = 0; i < 10; i++)
        {
            if (i % 5 == 0) continue;
            float a = i * 36f;
            SandbagWall(p + Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 5.2f), a + 90f, 2.9f);
        }
        // camo net overhead
        Deco(PrimitiveType.Cube, p + new Vector3(0, 2.6f, 0), new Vector3(6f, 0.05f, 6f), GA.Cloth(0, Color.white), null, true);
        for (int k = 0; k < 4; k++)
            Deco(PrimitiveType.Cube, p + new Vector3(k % 2 == 0 ? -2.8f : 2.8f, 1.3f, k < 2 ? -2.8f : 2.8f), new Vector3(0.1f, 2.6f, 0.1f), mWood);
    }

    void Depot(Vector3 p)
    {
        Reserve(p.x, p.z, 9, 9);
        Patch(p.x, p.z, 16, 16, mConcrete, KConcrete, 0.05f);
        for (int i = 0; i < 4; i++)
            pickupSpots.Add(p + new Vector3(i % 2 == 0 ? -3 : 3, 0, i < 2 ? -3 : 3));
        Deco(PrimitiveType.Cube, p + new Vector3(0, 4.2f, 0), new Vector3(10, 0.06f, 10), GA.Cloth(1, Color.white));
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Solid(PrimitiveType.Cube, p + new Vector3(sx * 4.6f, 2.1f, sz * 4.6f), new Vector3(0.2f, 4.2f, 0.2f), mWood, KProp);
        SandbagWall(p + new Vector3(0, 0, -7.5f), 0, 6);
        SandbagWall(p + new Vector3(-7.5f, 0, 0), 90, 6);
    }

    void RoadMarkings(float[] lines)
    {
        var paint = mWhitePaint;
        foreach (float l in lines)
        {
            for (float t = -82; t < 82; t += 9f)
            {
                bool nearCross = false;
                foreach (float c in lines) if (Mathf.Abs(t + 1.5f - c) < 7f) nearCross = true;
                if (nearCross) continue;
                Deco(PrimitiveType.Cube, new Vector3(l, 0.035f, t + 1.5f), new Vector3(0.15f, 0.02f, 3f), paint, null, false);
                Deco(PrimitiveType.Cube, new Vector3(t + 1.5f, 0.035f, l), new Vector3(3f, 0.02f, 0.15f), paint, null, false);
            }
        }
        // crosswalks on the main intersections
        foreach (float ix in new float[] { -40, 0, 40 })
            foreach (float iz in new float[] { -40, 0, 40 })
            {
                for (float o = -4f; o <= 4.01f; o += 1.3f)
                {
                    Deco(PrimitiveType.Cube, new Vector3(ix + o, 0.035f, iz + 6.8f), new Vector3(0.6f, 0.02f, 2.6f), paint, null, false);
                    Deco(PrimitiveType.Cube, new Vector3(ix + o, 0.035f, iz - 6.8f), new Vector3(0.6f, 0.02f, 2.6f), paint, null, false);
                    Deco(PrimitiveType.Cube, new Vector3(ix + 6.8f, 0.035f, iz + o), new Vector3(2.6f, 0.02f, 0.6f), paint, null, false);
                    Deco(PrimitiveType.Cube, new Vector3(ix - 6.8f, 0.035f, iz + o), new Vector3(2.6f, 0.02f, 0.6f), paint, null, false);
                }
                TrafficLight(new Vector3(ix + 5.7f, 0.15f, iz + 5.7f));
            }
        // outer road dashes
        for (float t = 90; t < 195; t += 9f)
            foreach (int sgn in new[] { -1, 1 })
            {
                Deco(PrimitiveType.Cube, new Vector3(0, 0.045f, sgn * t), new Vector3(0.15f, 0.02f, 3f), paint, null, false);
                Deco(PrimitiveType.Cube, new Vector3(sgn * t, 0.045f, 0), new Vector3(3f, 0.02f, 0.15f), paint, null, false);
            }
    }

    void TrafficLight(Vector3 p)
    {
        Solid(PrimitiveType.Cylinder, p + new Vector3(0, 2.6f, 0), new Vector3(0.16f, 2.6f, 0.16f), mDarkMetal, KProp);
        Deco(PrimitiveType.Cube, p + new Vector3(-2.2f, 5.1f, 0), new Vector3(4.4f, 0.12f, 0.12f), mDarkMetal);
        var box = p + new Vector3(-3.6f, 4.55f, 0);
        Deco(PrimitiveType.Cube, box, new Vector3(0.35f, 1.0f, 0.3f), mDarkMetal);
        Glow(PrimitiveType.Cube, box + new Vector3(0, 0.3f, -0.16f), new Vector3(0.18f, 0.18f, 0.02f), new Color(1f, 0.1f, 0.05f), 3f);
        Deco(PrimitiveType.Cube, box + new Vector3(0, 0f, -0.16f), new Vector3(0.18f, 0.18f, 0.02f), GA.Lit(new Color(0.25f, 0.2f, 0.05f), 0.8f));
        Deco(PrimitiveType.Cube, box + new Vector3(0, -0.3f, -0.16f), new Vector3(0.18f, 0.18f, 0.02f), GA.Lit(new Color(0.05f, 0.2f, 0.08f), 0.8f));
    }

    void City(float[] lines)
    {
        float[] centers = { -60, -20, 20, 60 };
        var types = new List<int> { 1, 1, 2, 3, 4 };
        while (types.Count < 16) types.Add(0);
        Shuffle(types, r);
        int k = 0;
        foreach (float bx in centers)
            foreach (float bz in centers)
            {
                Patch(bx, bz, 30, 30, mTiles, KConcrete, 0.15f);
                switch (types[k++])
                {
                    case 0: BuildingBlock(bx, bz); break;
                    case 1: Ruin(bx, bz, 24f); break;
                    case 2: Park(bx, bz); break;
                    case 3: Parking(bx, bz); break;
                    default: Plaza(bx, bz); break;
                }
                // sidewalk furniture
                for (int i = 0; i < 2; i++)
                {
                    float edge = 14.2f;
                    var sp = Chance(0.5f) ? new Vector3(bx + Rf(-12, 12), 0.15f, bz + (Chance(0.5f) ? edge : -edge))
                                          : new Vector3(bx + (Chance(0.5f) ? edge : -edge), 0.15f, bz + Rf(-12, 12));
                    if (Chance(0.5f)) Solid(PrimitiveType.Cylinder, sp + new Vector3(0, 0.45f, 0), new Vector3(0.55f, 0.45f, 0.55f), GA.Tri(Surf.Metal, new Color(0.3f, 0.4f, 0.3f)), KProp);
                    else Solid(PrimitiveType.Cylinder, sp + new Vector3(0, 0.35f, 0), new Vector3(0.25f, 0.35f, 0.25f), GA.Lit(new Color(0.6f, 0.08f, 0.06f), 0.5f), KProp);
                }
            }

        foreach (float l in new float[] { -40, 0, 40 })
            for (float z = -70; z <= 70; z += 28)
                for (int side = -1; side <= 1; side += 2)
                {
                    Lamp(new Vector3(l + side * 5.6f, 0.15f, z + 6), side);
                    Lamp(new Vector3(z + 6, 0.15f, l + side * 5.6f), side, true);
                }

        // abandoned and burned cars, barricades
        int smokes = 0;
        for (int i = 0; i < 26; i++)
        {
            float l = lines[r.Next(lines.Length)];
            float along = Rf(-75, 75);
            if (Mathf.Abs(Mathf.Repeat(along + 20f, 40f) - 20f) < 8f) continue;
            bool vertical = Chance(0.5f);
            var p = vertical ? new Vector3(l + Rf(-2.8f, 2.8f), 0.03f, along) : new Vector3(along, 0.03f, l + Rf(-2.8f, 2.8f));
            if (Mathf.Abs(p.x) < 10 && Mathf.Abs(p.z) < 14) continue;
            float roll = Rf(0, 1);
            if (roll < 0.55f)
            {
                bool burned = Chance(0.4f);
                bool smoke = burned && smokes < 6 && Chance(0.7f);
                if (smoke) smokes++;
                Car(p, (vertical ? 0 : 90) + Rf(-30, 30), burned, smoke);
            }
            else if (roll < 0.8f) JerseyBarrier(p, (vertical ? 90 : 0) + Rf(-20, 20));
            else
                for (int j = 0; j < 3; j++) Hedgehog(p + (vertical ? new Vector3(j * 2.4f - 2.4f, 0, 0) : new Vector3(0, 0, j * 2.4f - 2.4f)));
        }
    }

    void Lamp(Vector3 p, int side, bool alongX = false)
    {
        Solid(PrimitiveType.Cylinder, p + new Vector3(0, 3.5f, 0), new Vector3(0.14f, 3.5f, 0.14f), mDarkMetal, KProp);
        var dir = alongX ? new Vector3(0, 0, -side) : new Vector3(-side, 0, 0);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 6.95f, 0) + dir * 0.7f, alongX ? new Vector3(0.1f, 0.1f, 1.5f) : new Vector3(1.5f, 0.1f, 0.1f), mDarkMetal, null, false);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 6.85f, 0) + dir * 1.4f, new Vector3(0.35f, 0.15f, 0.35f), mDarkMetal, null, false);
        Glow(PrimitiveType.Cube, p + new Vector3(0, 6.76f, 0) + dir * 1.4f, new Vector3(0.28f, 0.02f, 0.28f), new Color(1f, 0.9f, 0.7f), 1.2f);
    }

    void BuildingBlock(float bx, float bz)
    {
        float dc = new Vector2(bx, bz).magnitude;
        int maxFloors = Mathf.RoundToInt(Mathf.Lerp(11f, 4f, Mathf.Clamp01(dc / 90f)));
        int split = r.Next(3);
        if (split == 0) Lot(bx - 13, bz - 13, bx + 13, bz + 13, maxFloors);
        else if (split == 1)
        {
            if (Chance(0.5f)) { Lot(bx - 13, bz - 13, bx - 1.5f, bz + 13, maxFloors); Lot(bx + 1.5f, bz - 13, bx + 13, bz + 13, maxFloors); }
            else { Lot(bx - 13, bz - 13, bx + 13, bz - 1.5f, maxFloors); Lot(bx - 13, bz + 1.5f, bx + 13, bz + 13, maxFloors); }
        }
        else
        {
            for (int sx = 0; sx < 2; sx++)
                for (int sz = 0; sz < 2; sz++)
                {
                    float x0 = sx == 0 ? bx - 13 : bx + 1.5f, x1 = sx == 0 ? bx - 1.5f : bx + 13;
                    float z0 = sz == 0 ? bz - 13 : bz + 1.5f, z1 = sz == 0 ? bz - 1.5f : bz + 13;
                    if (Chance(0.85f)) Lot(x0, z0, x1, z1, maxFloors);
                    else Crate(new Vector3((x0 + x1) / 2, 0.15f, (z0 + z1) / 2), 1.2f);
                }
        }
        for (int i = 0; i < 3; i++)
        {
            var p = new Vector3(bx + Rf(-14, 14), 0.15f, bz + Rf(-14, 14));
            if (AreaFree(p, 0.6f, 0.6f)) Crate(p, Rf(0.9f, 1.3f));
        }
    }

    void Ruin(float cx, float cz, float size)
    {
        Patch(cx, cz, size + 2, size + 2, GA.Tri(Surf.Concrete, new Color(0.75f, 0.73f, 0.7f)), KConcrete, 0.2f);
        var wm = Chance(0.5f) ? GA.Tri(Surf.Brick) : GA.Tri(Surf.Concrete);
        float h = Rf(3f, 6f), t = 0.6f;
        int segs = 8;
        float seg = size / segs;
        for (int side = 0; side < 4; side++)
        {
            int door = r.Next(1, segs - 1);
            for (int i = 0; i < segs; i++)
            {
                if (i == door || Chance(0.15f)) continue;
                float off = -size / 2 + seg * (i + 0.5f);
                float hh = h * Rf(0.35f, 1f);
                Vector3 p, s;
                if (side < 2) { p = new Vector3(cx + off, hh / 2, cz + (side == 0 ? -1 : 1) * size / 2); s = new Vector3(seg + 0.02f, hh, t); }
                else { p = new Vector3(cx + (side == 2 ? -1 : 1) * size / 2, hh / 2, cz + off); s = new Vector3(t, hh, seg + 0.02f); }
                Solid(PrimitiveType.Cube, p, s, wm, KBuilding);
                if (Chance(0.5f))
                    Deco(PrimitiveType.Cube, p + new Vector3(Rf(-0.4f, 0.4f), hh / 2 + 0.2f, 0), new Vector3(s.x * 0.5f, 0.5f, s.z * 0.9f), wm, Quaternion.Euler(Rf(-15, 15), 0, Rf(-25, 25)));
            }
        }
        for (int i = 0; i < 3; i++)
        {
            float len = Rf(4, 8), hh = Rf(1.2f, 3f);
            var p = new Vector3(cx + Rf(-size / 3, size / 3), hh / 2, cz + Rf(-size / 3, size / 3));
            Solid(PrimitiveType.Cube, p, Chance(0.5f) ? new Vector3(len, hh, t) : new Vector3(t, hh, len), wm, KBuilding);
        }
        var rubble = GA.Tri(Surf.Rock, new Color(0.9f, 0.82f, 0.75f));
        for (int i = 0; i < 8; i++)
        {
            var p = new Vector3(cx + Rf(-size / 2 + 2, size / 2 - 2), 0.2f, cz + Rf(-size / 2 + 2, size / 2 - 2));
            MeshSolid(TexGen.Rock(r.Next(6)), p, new Vector3(Rf(1.5f, 3.5f), Rf(0.5f, 1.2f), Rf(1.5f, 3.5f)), rubble, KRock, Quaternion.Euler(0, Rf(0, 360), 0));
        }
        for (int i = 0; i < 4; i++)
            Deco(PrimitiveType.Cube, new Vector3(cx + Rf(-size / 3, size / 3), 0.6f, cz + Rf(-size / 3, size / 3)), new Vector3(0.25f, 0.25f, Rf(3, 5)),
                GA.Tri(Surf.Wood, new Color(0.25f, 0.2f, 0.17f)), Quaternion.Euler(Rf(5, 25), Rf(0, 360), 0));
        if (Chance(0.6f))
        {
            var sc = new GameObject("RuinSmoke").AddComponent<SmokeColumn>();
            sc.transform.position = new Vector3(cx + Rf(-4, 4), 0.5f, cz + Rf(-4, 4));
            sc.fire = Chance(0.5f);
        }
    }

    void Park(float cx, float cz)
    {
        Patch(cx, cz, 28.5f, 28.5f, GA.Tri(Surf.Grass), KGrass, 0.2f);
        Patch(cx, cz, 2.5f, 28.5f, mTiles, KConcrete, 0.22f);
        Patch(cx, cz, 28.5f, 2.5f, mTiles, KConcrete, 0.22f);
        float px = cx + 7f * (Chance(0.5f) ? 1 : -1), pz = cz + 7f * (Chance(0.5f) ? 1 : -1);
        Patch(px, pz, 9, 7, GA.Tri(Surf.Concrete), KConcrete, 0.35f);
        Patch(px, pz, 8, 6, mWater, KWater, 0.3f);
        Reserve(px, pz, 4.5f, 3.5f);
        Reserve(cx, cz, 1.5f, 14f);
        Reserve(cx, cz, 14f, 1.5f);
        for (int i = 0; i < 14; i++)
        {
            var p = new Vector3(cx + Rf(-12, 12), 0.2f, cz + Rf(-12, 12));
            if (AreaFree(p, 0.8f, 0.8f)) Tree(p, Rf(0.8f, 1.15f));
            else if (AreaFree(p, 0.3f, 0.3f)) Bush(p, Rf(0.7f, 1.1f));
        }
        for (int i = 0; i < 4; i++)
        {
            var p = new Vector3(cx + (i % 2 == 0 ? 2.2f : -2.2f), 0.22f, cz + (i < 2 ? 6f : -6f));
            var rot = Quaternion.Euler(0, i % 2 == 0 ? 90 : -90, 0);
            Solid(PrimitiveType.Cube, p + new Vector3(0, 0.45f, 0), new Vector3(2f, 0.08f, 0.55f), mWood, KProp, rot);
            Deco(PrimitiveType.Cube, p + new Vector3(0, 0.75f, 0) + rot * new Vector3(0, 0, -0.27f), new Vector3(2f, 0.45f, 0.06f), mWood, rot);
            Deco(PrimitiveType.Cube, p + new Vector3(0, 0.22f, 0), new Vector3(1.8f, 0.44f, 0.08f), mDarkMetal, rot);
        }
    }

    void Parking(float cx, float cz)
    {
        Patch(cx, cz, 28.5f, 28.5f, mAsphalt, KRoad, 0.18f);
        for (int row = -1; row <= 1; row += 2)
            for (int i = -2; i <= 2; i++)
            {
                var p = new Vector3(cx + i * 5f, 0.18f, cz + row * 7f);
                Deco(PrimitiveType.Cube, new Vector3(p.x + 2.5f, 0.195f, p.z), new Vector3(0.12f, 0.02f, 5f), mWhitePaint, null, false);
                if (Chance(0.65f)) Car(p, (row > 0 ? 0 : 180) + Rf(-6, 6), Chance(0.15f));
            }
    }

    void Plaza(float cx, float cz)
    {
        Patch(cx, cz, 28.5f, 28.5f, GA.Tri(Surf.Tiles, new Color(1.05f, 1f, 0.92f)), KConcrete, 0.2f);
        var stone = GA.Tri(Surf.Concrete, new Color(1.1f, 1.05f, 0.95f));
        Solid(PrimitiveType.Cube, new Vector3(cx, 0.7f, cz), new Vector3(6, 1, 6), stone, KBuilding);
        Solid(PrimitiveType.Cube, new Vector3(cx, 1.4f, cz), new Vector3(4.5f, 0.5f, 4.5f), stone, KBuilding);
        Solid(PrimitiveType.Cube, new Vector3(cx, 6.5f, cz), new Vector3(1.4f, 10, 1.4f), stone, KBuilding);
        Deco(PrimitiveType.Cube, new Vector3(cx, 11.8f, cz), new Vector3(1.8f, 0.6f, 1.8f), stone);
        Deco(PrimitiveType.Sphere, new Vector3(cx, 12.8f, cz), Vector3.one * 1.4f, GA.Lit(new Color(0.55f, 0.42f, 0.2f), 0.75f));
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var p = new Vector3(cx + sx * 10, 0.2f, cz + sz * 10);
                Solid(PrimitiveType.Cube, p + new Vector3(0, 0.4f, 0), new Vector3(2.6f, 0.8f, 2.6f), stone, KProp);
                Bush(p + new Vector3(0, 0.8f, 0), 0.9f);
            }
        Solid(PrimitiveType.Cylinder, new Vector3(cx + 6, 4.2f, cz - 6), new Vector3(0.12f, 4f, 0.12f), mWhitePaint, KProp);
        Deco(PrimitiveType.Cube, new Vector3(cx + 6.8f, 7.4f, cz - 6), new Vector3(1.5f, 0.9f, 0.03f), GA.Lit(new Color(0.15f, 0.25f, 0.45f), 0.1f));
    }

    void Forest()
    {
        Patch(-90, 150, 50, 34, GA.Tri(Surf.Sand, new Color(0.85f, 0.85f, 0.8f)), KSand, 0.03f);
        Patch(-90, 150, 44, 28, mWater, KWater, 0.06f);
        Reserve(-90, 150, 22, 14);
        Deck(new Vector3(-90, 0, 136));

        for (int i = 0; i < 4; i++)
            TryPlace(-185, 185, 100, 188, 6, 6, 40, Cabin);
        for (int i = 0; i < 380; i++)
        {
            var p = new Vector3(Rf(-196, 196), 0, Rf(92, 196));
            if (!AreaFree(p, 0.8f, 0.8f)) continue;
            if (Chance(0.6f)) Pine(p, Rf(0.85f, 1.4f)); else Tree(p, Rf(0.85f, 1.3f));
        }
        for (int i = 0; i < 120; i++)
        {
            var p = new Vector3(Rf(-196, 196), 0, Rf(92, 196));
            if (AreaFree(p, 0.3f, 0.3f)) Bush(p, Rf(0.6f, 1.2f));
        }
        for (int i = 0; i < 40; i++)
        {
            var p = new Vector3(Rf(-196, 196), 0, Rf(92, 196));
            if (AreaFree(p, 1.5f, 1.5f)) Rock(p, Rf(1f, 2.6f), mRock);
        }
        for (int i = 0; i < 20; i++)
        {
            var p = new Vector3(Rf(-196, 196), 0, Rf(92, 196));
            if (!AreaFree(p, 2f, 2f)) continue;
            Solid(PrimitiveType.Cylinder, p + new Vector3(0, 0.35f, 0), new Vector3(0.7f, Rf(1.5f, 2.5f), 0.7f), mBark, KTree,
                Quaternion.Euler(0, Rf(0, 360), 0) * Quaternion.Euler(90, 0, 0));
        }
    }

    void Deck(Vector3 p)
    {
        Deco(PrimitiveType.Cube, p + new Vector3(0, 0.35f, 0), new Vector3(2f, 0.12f, 8f), mWood);
        for (int i = 0; i < 4; i++)
            Deco(PrimitiveType.Cylinder, p + new Vector3(i % 2 == 0 ? -0.9f : 0.9f, 0.1f, i < 2 ? -3.5f : 3.5f), new Vector3(0.18f, 0.35f, 0.18f), mBark);
    }

    void Cabin(Vector3 p)
    {
        float yaw = r.Next(4) * 90f;
        var rot = Quaternion.Euler(0, yaw, 0);
        var logs = GA.Tri(Surf.Wood, new Color(0.85f, 0.75f, 0.65f));
        Solid(PrimitiveType.Cube, p + new Vector3(0, 1.75f, 0), new Vector3(7, 3.5f, 6), logs, KBuilding, rot);
        Gable(p, 6f, 7f, 3.5f, mRoof, yaw + 90f);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(2f, 5f, 1f), new Vector3(0.8f, 2.5f, 0.8f), GA.Tri(Surf.Brick), rot);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, 1.1f, -3.05f), new Vector3(1.1f, 2.2f, 0.1f), GA.Tri(Surf.Wood, new Color(0.5f, 0.4f, 0.32f)), rot);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(2.2f, 1.8f, -3.03f), new Vector3(1f, 0.9f, 0.06f), mGlass, rot);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(-2.2f, 1.8f, -3.03f), new Vector3(1f, 0.9f, 0.06f), mGlass, rot);
        Deco(PrimitiveType.Cube, p + rot * new Vector3(0, 0.15f, -4f), new Vector3(7f, 0.15f, 2f), mWood, rot);
    }

    void Industrial()
    {
        for (int i = 0; i < 5; i++)
            TryPlace(-180, 180, -185, -105, 15, 15, 60, p =>
            {
                bool rot = Chance(0.5f);
                var size = rot ? new Vector3(28, 9, 20) : new Vector3(20, 9, 28);
                var wall = GA.Tri(Surf.Metal, Chance(0.5f) ? new Color(0.7f, 0.75f, 0.78f) : new Color(0.8f, 0.72f, 0.6f));
                Solid(PrimitiveType.Cube, p + new Vector3(0, size.y / 2, 0), size, wall, KBuilding);
                Deco(PrimitiveType.Cube, p + new Vector3(0, 1f, 0), new Vector3(size.x + 0.1f, 2f, size.z + 0.1f), mConcrete);
                Gable(p, rot ? size.z : size.x, rot ? size.x : size.z, size.y, GA.Tri(Surf.Metal, new Color(0.45f, 0.47f, 0.5f)), rot ? 90f : 0f);
                var door = rot ? new Vector3(0, 3f, -size.z / 2 - 0.06f) : new Vector3(-size.x / 2 - 0.06f, 3f, 0);
                Deco(PrimitiveType.Cube, p + door, rot ? new Vector3(8, 6, 0.15f) : new Vector3(0.15f, 6, 8), GA.Tri(Surf.Metal, new Color(0.55f, 0.55f, 0.5f)), null, false);
                Deco(PrimitiveType.Cube, p + new Vector3(0, 7.5f, 0), new Vector3(size.x + 0.08f, 0.9f, size.z + 0.08f), mDarkGlass, null, false);
            });
        for (int i = 0; i < 9; i++)
            TryPlace(-185, 185, -190, -100, 5, 5, 40, p =>
            {
                var tm = GA.Tri(Surf.Metal, new Color(0.95f, 0.95f, 0.92f));
                Solid(PrimitiveType.Cylinder, p + new Vector3(0, 4f, 0), new Vector3(8, 4, 8), tm, KProp);
                Deco(PrimitiveType.Sphere, p + new Vector3(0, 8f, 0), new Vector3(8, 1.8f, 8), tm);
                Deco(PrimitiveType.Cube, p + new Vector3(0, 4f, -4.05f), new Vector3(0.6f, 8f, 0.12f), mDarkMetal);
                Deco(PrimitiveType.Cylinder, p + new Vector3(4.6f, 1.5f, 0), new Vector3(0.6f, 1.5f, 0.6f), mDarkMetal, Quaternion.Euler(0, 0, 90));
            });
        for (float x = -185; x < 185; x += 9)
            for (float z = -188; z < -98; z += 14)
            {
                if (!Chance(0.4f)) continue;
                var p = new Vector3(x + Rf(-1, 1), 0.02f, z);
                bool turned = Chance(0.3f);
                if (!AreaFree(p, turned ? 3.2f : 1.4f, turned ? 1.4f : 3.2f)) continue;
                Container(p, turned ? 90 : 0, Chance(0.3f));
            }
        for (int i = 0; i < 60; i++)
        {
            var p = new Vector3(Rf(-195, 195), 0.02f, Rf(-195, -95));
            if (AreaFree(p, 0.7f, 0.7f)) Crate(p, Rf(0.8f, 1.4f));
        }
        for (int i = 0; i < 30; i++)
        {
            var p = new Vector3(Rf(-195, 195), 0.02f, Rf(-195, -95));
            if (!AreaFree(p, 0.7f, 0.7f)) continue;
            var rot = Quaternion.Euler(0, Rf(0, 90), 0);
            for (int k = 0; k < r.Next(1, 4); k++)
                Solid(PrimitiveType.Cube, p + new Vector3(0, 0.07f + k * 0.15f, 0), new Vector3(1.2f, 0.14f, 1f), mWood, KProp, rot);
        }
        for (int i = 0; i < 50; i++)
        {
            var p = new Vector3(Rf(-195, 195), 0.02f, Rf(-195, -95));
            if (AreaFree(p, 0.4f, 0.4f))
                Solid(PrimitiveType.Cylinder, p + new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.45f, 0.6f),
                    GA.Tri(Surf.Metal, Chance(0.6f) ? new Color(0.65f, 0.2f, 0.15f) : new Color(0.2f, 0.35f, 0.6f)), KProp);
        }
    }

    void Container(Vector3 p, float yaw, bool stack)
    {
        var rot = Quaternion.Euler(0, yaw, 0);
        var m = GA.Tri(Surf.Metal, GA.C(Pick(ContainerCols)));
        Solid(PrimitiveType.Cube, p + new Vector3(0, 1.3f, 0), new Vector3(2.45f, 2.6f, 6.06f), m, KProp, rot);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 1.3f, 0) + rot * new Vector3(0, 0, 3.03f), new Vector3(2.5f, 2.62f, 0.1f), mDarkMetal, rot);
        Deco(PrimitiveType.Cube, p + new Vector3(0, 1.3f, 0) + rot * new Vector3(0, 0, -3.03f), new Vector3(2.5f, 2.62f, 0.1f), mDarkMetal, rot);
        if (stack)
        {
            var m2 = GA.Tri(Surf.Metal, GA.C(Pick(ContainerCols)));
            Solid(PrimitiveType.Cube, p + new Vector3(0, 3.9f, 0), new Vector3(2.45f, 2.6f, 6.06f), m2, KProp, rot * Quaternion.Euler(0, Rf(-5, 5), 0));
        }
    }

    void Desert()
    {
        for (int i = 0; i < 3; i++)
            TryPlace(95, 190, -80, 80, 5, 5, 40, p =>
            {
                float yaw = r.Next(4) * 90f; var rot = Quaternion.Euler(0, yaw, 0);
                Solid(PrimitiveType.Cube, p + new Vector3(0, 1.4f, 0), new Vector3(8, 2.8f, 6), mConcrete, KBuilding, rot);
                Deco(PrimitiveType.Cube, p + new Vector3(0, 2.9f, 0), new Vector3(8.6f, 0.4f, 6.6f), mConcrete, rot);
                Deco(PrimitiveType.Cube, p + new Vector3(0, 2.1f, 0) + rot * new Vector3(0, 0, -3.02f), new Vector3(6, 0.35f, 0.1f), mDarkGlass, rot, false);
            });
        for (int i = 0; i < 12; i++)
            TryPlace(95, 190, -80, 80, 3.5f, 3.5f, 30, SandbagNest);
        for (int i = 0; i < 70; i++)
        {
            var p = new Vector3(Rf(92, 196), 0, Rf(-86, 86));
            if (AreaFree(p, 2f, 2f)) Rock(p, Rf(1.4f, 4f), mSandRock);
        }
        var cactus = GA.Lit(new Color(0.24f, 0.36f, 0.2f), 0.2f);
        for (int i = 0; i < 45; i++)
        {
            var p = new Vector3(Rf(92, 196), 0, Rf(-86, 86));
            if (!AreaFree(p, 0.6f, 0.6f)) continue;
            float s = Rf(0.8f, 1.3f);
            Solid(PrimitiveType.Cylinder, p + new Vector3(0, 1.5f * s, 0), new Vector3(0.5f, 1.5f, 0.5f) * s, cactus, KProp);
            Deco(PrimitiveType.Sphere, p + new Vector3(0, 3f * s, 0), new Vector3(0.5f, 0.4f, 0.5f) * s, cactus);
            if (Chance(0.7f))
            {
                Deco(PrimitiveType.Cylinder, p + new Vector3(0.45f * s, 1.6f * s, 0), new Vector3(0.32f, 0.3f, 0.32f) * s, cactus, Quaternion.Euler(0, 0, 90));
                Deco(PrimitiveType.Cylinder, p + new Vector3(0.65f * s, 2.1f * s, 0), new Vector3(0.3f, 0.5f, 0.3f) * s, cactus);
            }
        }
        for (int i = 0; i < 60; i++)
        {
            var p = new Vector3(Rf(92, 196), 0, Rf(-86, 86));
            if (AreaFree(p, 0.3f, 0.3f)) Bush(p, Rf(0.4f, 0.7f));
        }
        for (int i = 0; i < 15; i++)
        {
            var p = new Vector3(Rf(92, 196), 0, Rf(-86, 86));
            if (!AreaFree(p, 0.6f, 0.6f)) continue;
            var c = GA.Tri(Surf.Bark, new Color(0.9f, 0.85f, 0.8f));
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
            SandbagWall(p + Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 3f), a + 90f, 1.8f);
        }
        if (Chance(0.5f)) Crate(p, 1f);
    }

    void Farm()
    {
        float[] fx = { -178, -148, -118 };
        float[] fz = { -57, 57 };
        var field = GA.Tri(Surf.Field);
        var wheat = GA.Tri(Surf.Hay, new Color(1f, 0.95f, 0.8f));
        foreach (float x in fx)
            foreach (float z in fz)
            {
                Patch(x, z, 24, 30, field, KDirt, 0.05f);
                Reserve(x, z, 12, 15);
                bool green = Chance(0.5f);
                for (float rx = x - 10; rx <= x + 10; rx += 2.4f)
                    Deco(PrimitiveType.Cube, new Vector3(rx, 0.35f, z), new Vector3(0.7f, 0.6f, 27f), green ? mLeaf : wheat, null, false);
            }
        var barnWood = GA.Tri(Surf.Wood, new Color(1.3f, 0.55f, 0.45f));
        for (int i = 0; i < 3; i++)
            TryPlace(-185, -100, -32, 32, 8, 8, 50, p =>
            {
                float yaw = r.Next(2) * 90f; var rot = Quaternion.Euler(0, yaw, 0);
                Solid(PrimitiveType.Cube, p + new Vector3(0, 3f, 0), new Vector3(10, 6, 14), barnWood, KBuilding, rot);
                Gable(p, 10f, 14f, 6f, GA.Tri(Surf.Metal, new Color(0.35f, 0.36f, 0.38f)), yaw);
                Deco(PrimitiveType.Cube, p + new Vector3(0, 2.2f, 0) + rot * new Vector3(0, 0, -7.05f), new Vector3(4f, 4.4f, 0.1f), GA.Tri(Surf.Wood, new Color(1.2f, 1.15f, 1.1f)), rot);
            });
        for (int i = 0; i < 4; i++)
            TryPlace(-190, -95, -35, 35, 2.5f, 2.5f, 40, p =>
            {
                Solid(PrimitiveType.Cylinder, p + new Vector3(0, 6f, 0), new Vector3(4, 6, 4), mMetal, KBuilding);
                Deco(PrimitiveType.Sphere, p + new Vector3(0, 12f, 0), new Vector3(4, 2.2f, 4), mMetal);
            });
        var hay = GA.Tri(Surf.Hay);
        for (int i = 0; i < 45; i++)
        {
            var p = new Vector3(Rf(-196, -92), 0, Rf(-86, 86));
            if (AreaFree(p, 0.8f, 0.8f))
                Solid(PrimitiveType.Cylinder, p + new Vector3(0, 0.7f, 0), new Vector3(1.4f, 0.6f, 1.4f), hay, KProp,
                    Quaternion.Euler(0, Rf(0, 180), 0) * Quaternion.Euler(0, 0, 90));
        }
        for (int i = 0; i < 40; i++)
        {
            var p = new Vector3(Rf(-196, -92), 0, Rf(-86, 86));
            if (AreaFree(p, 0.8f, 0.8f)) Tree(p, Rf(0.9f, 1.3f));
        }
        foreach (float x in fx)
            foreach (float z in fz)
                for (float fzz = z - 15; fzz <= z + 15; fzz += 3f)
                {
                    if (r.NextDouble() < 0.2) continue;
                    Deco(PrimitiveType.Cube, new Vector3(x - 12.5f, 0.6f, fzz), new Vector3(0.12f, 1.2f, 0.12f), mWood);
                    Deco(PrimitiveType.Cube, new Vector3(x - 12.5f, 0.85f, fzz + 1.5f), new Vector3(0.06f, 0.1f, 3f), mWood);
                    Deco(PrimitiveType.Cube, new Vector3(x - 12.5f, 0.45f, fzz + 1.5f), new Vector3(0.06f, 0.1f, 3f), mWood);
                }
        // power line along the west road
        var wire = GA.Lit(new Color(0.06f, 0.06f, 0.06f), 0.3f);
        Vector3 prev = Vector3.zero;
        for (float x = -95; x >= -195; x -= 20)
        {
            var p = new Vector3(x, 0, -7f);
            Solid(PrimitiveType.Cylinder, p + new Vector3(0, 4.5f, 0), new Vector3(0.28f, 4.5f, 0.28f), mBark, KProp);
            Deco(PrimitiveType.Cube, p + new Vector3(0, 8.4f, 0), new Vector3(0.15f, 0.15f, 2.4f), mWood);
            if (x < -95)
                for (int w = -1; w <= 1; w++)
                    GA.Segment(root, prev + new Vector3(0, 8.55f, w * 1f), p + new Vector3(0, 8.55f, w * 1f), 0.03f, wire, 0, false);
            prev = p;
        }
    }

    void Mountains()
    {
        const int seg = 220, rings = 22;
        const float rMax = 1100f;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float r0 = 203f / Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa));
            for (int j = 0; j <= rings; j++)
            {
                float t = j / (float)rings;
                float rad = Mathf.Lerp(r0, rMax, t * t);
                float ramp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((rad - r0) / 220f));
                float n = Mathf.PerlinNoise(ca * 2.2f + 10f, sa * 2.2f + 10f) * 0.6f
                        + Mathf.PerlinNoise(ca * 6f + rad * 0.004f, sa * 6f + 3f) * 0.3f
                        + Mathf.PerlinNoise(ca * 18f + rad * 0.01f, sa * 18f) * 0.1f;
                float h = ramp * (25f + n * 210f) - (j == 0 ? 0.5f : 0f);
                verts.Add(new Vector3(ca * rad, h, sa * rad));
                uvs.Add(new Vector2(i / (float)seg * 40f, t * 10f));
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < rings; j++)
            {
                int a = i * (rings + 1) + j, b = a + rings + 1;
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        var mesh = new Mesh { name = "Mountains" };
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        var m = new Material(GA.Tri(Surf.Rock, new Color(0.9f, 0.88f, 0.85f)));
        var grass = TexGen.Get(Surf.Grass);
        m.SetTexture("_TopTex", grass.alb);
        m.SetTexture("_TopBump", grass.nrm);
        m.SetVector("_Scale", new Vector4(14, 14, 10, 300));
        var g = GA.MeshObj(mesh, null, Vector3.zero, Vector3.one, m, false, 0, false);
        g.name = "Mountains";
        // distant forest on the slopes
        var pineMat = mLeafDark;
        for (int i = 0; i < 260; i++)
        {
            float a = Rf(0, Mathf.PI * 2);
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            float r0 = 203f / Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa));
            float rad = r0 + Rf(15f, 140f);
            var origin = new Vector3(ca * rad, 400f, sa * rad);
            if (!mesh.bounds.Contains(new Vector3(origin.x, 0, origin.z))) continue;
            float hgt = SampleHeight(verts, seg, rings, rMax, a, rad);
            float s = Rf(1.4f, 2.4f);
            GA.MeshObj(TexGen.Cone(), g.transform, new Vector3(ca * rad, hgt - 0.5f, sa * rad), new Vector3(2.2f * s, 7f * s, 2.2f * s), pineMat, false, 0, false);
        }
    }

    static float SampleHeight(List<Vector3> verts, int seg, int rings, float rMax, float a, float rad)
    {
        int i = Mathf.Clamp(Mathf.RoundToInt(a / (Mathf.PI * 2) * seg), 0, seg);
        float best = 0f, bestD = float.MaxValue;
        for (int j = 0; j <= rings; j++)
        {
            var v = verts[i * (rings + 1) + j];
            float d = Mathf.Abs(new Vector2(v.x, v.z).magnitude - rad);
            if (d < bestD) { bestD = d; best = v.y; }
        }
        return best;
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
        // random weapon finds on pallets (two near the start)
        crateSpots.Add(playerSpawn + new Vector3(3.5f, 0, 2f));
        crateSpots.Add(playerSpawn + new Vector3(-3.5f, 0, 2f));
        for (int tries = 0; tries < 10000 && crateSpots.Count < 26; tries++)
        {
            int x = r.Next(3, N - 3), z = r.Next(3, N - 3);
            if (!reachable[x, z]) continue;
            var p = CellCenter(x, z);
            bool ok = true;
            foreach (var o in crateSpots) if ((o - p).sqrMagnitude < 28f * 28f) { ok = false; break; }
            foreach (var o in pickupSpots) if ((o - p).sqrMagnitude < 6f * 6f) { ok = false; break; }
            if (ok) crateSpots.Add(p);
        }
        Physics.SyncTransforms();
        var pallet = GA.Tri(Surf.Wood, new Color(0.9f, 0.8f, 0.65f));
        foreach (var c in crateSpots)
        {
            float y = GroundY(c);
            var rot = Quaternion.Euler(0, Rf(0, 90), 0);
            GA.Prim(PrimitiveType.Cube, root, new Vector3(c.x, y + 0.07f, c.z), new Vector3(1.2f, 0.14f, 1.0f), pallet, true).transform.rotation = rot;
        }
    }

    // Ground height (sidewalks, plates) under a point.
    public static float GroundY(Vector3 p)
    {
        if (Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out var hit, 6f, GA.MaskWorld, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
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
