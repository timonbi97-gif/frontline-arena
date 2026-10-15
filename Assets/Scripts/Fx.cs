using System.Collections.Generic;
using UnityEngine;

public static class Fx
{
    static readonly Queue<GameObject> decals = new Queue<GameObject>();

    public static FxAnim Sphere(Vector3 pos, Color c, float size0, float size1, float life)
    {
        var m = GA.Fx(c);
        var g = GA.Prim(PrimitiveType.Sphere, null, pos, Vector3.one * size0, m, false, 0, false);
        var a = g.AddComponent<FxAnim>();
        a.life = life; a.s0 = Vector3.one * size0; a.s1 = Vector3.one * size1; a.mat = m; a.c0 = c;
        return a;
    }

    public static void AddLight(FxAnim a, Color c, float intensity, float range)
    {
        var l = a.gameObject.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        a.lt = l; a.lt0 = intensity;
    }

    public static void Debris(Vector3 pos, Color c, int count, float speed, float size = 0.12f, float life = 0.8f)
    {
        var m = GA.Lit(c);
        for (int i = 0; i < count; i++)
        {
            var g = GA.Prim(PrimitiveType.Cube, null, pos, Vector3.one * size, m, false, 0, false);
            g.transform.rotation = Random.rotation;
            var a = g.AddComponent<FxAnim>();
            a.life = life * Random.Range(0.6f, 1.2f);
            a.s0 = g.transform.localScale; a.s1 = Vector3.zero;
            a.vel = (Random.onUnitSphere + Vector3.up * 0.8f) * speed * Random.Range(0.4f, 1f);
            a.gravity = 15f; a.spin = Random.insideUnitSphere * 720f; a.fade = false;
        }
    }

    public static void Tracer(Vector3 a, Vector3 b, Color c)
    {
        var d = b - a; float len = d.magnitude;
        if (len < 0.6f) return;
        var m = GA.Fx(c);
        var g = GA.Prim(PrimitiveType.Cube, null, (a + b) * 0.5f, new Vector3(0.02f, 0.02f, len), m, false, 0, false);
        g.transform.rotation = Quaternion.LookRotation(d);
        var f = g.AddComponent<FxAnim>();
        f.life = 0.06f; f.s0 = f.s1 = g.transform.localScale; f.mat = m; f.c0 = c;
    }

    public static void Impact(Vector3 point, Vector3 normal, Collider col)
    {
        Color c = new Color(0.6f, 0.6f, 0.6f);
        var r = col != null ? col.GetComponent<Renderer>() : null;
        if (r != null && r.sharedMaterial != null) c = r.sharedMaterial.color;
        Debris(point + normal * 0.05f, c, 4, 3.5f, 0.07f, 0.5f);
        var s = Sphere(point + normal * 0.05f, new Color(1f, 0.85f, 0.5f, 0.9f), 0.15f, 0.35f, 0.07f);
        // bullet hole decal
        var q = GA.Prim(PrimitiveType.Quad, null, point + normal * 0.012f, Vector3.one * Random.Range(0.12f, 0.18f),
            GA.Lit(new Color(0.08f, 0.08f, 0.08f)), false, 0, false);
        q.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        decals.Enqueue(q);
        while (decals.Count > 70)
        {
            var old = decals.Dequeue();
            if (old != null) Object.Destroy(old);
        }
    }

    public static void Smoke(Vector3 pos, float size, float life, float alpha = 0.45f)
    {
        var a = Sphere(pos, new Color(0.55f, 0.55f, 0.55f, alpha), size * 0.4f, size, life);
        a.vel = Vector3.up * 0.8f + Random.insideUnitSphere * 0.4f;
    }

    public static void Explosion(Vector3 pos, float radius)
    {
        var core = Sphere(pos, new Color(1f, 0.85f, 0.4f, 1f), radius * 0.3f, radius * 1.3f, 0.22f);
        AddLight(core, new Color(1f, 0.6f, 0.2f), 8f, radius * 4f);
        Sphere(pos, new Color(1f, 0.4f, 0.1f, 0.8f), radius * 0.5f, radius * 1.6f, 0.4f);
        for (int i = 0; i < 6; i++)
            Smoke(pos + Random.insideUnitSphere * radius * 0.4f, radius * Random.Range(0.8f, 1.4f), Random.Range(1.2f, 2f), 0.5f);
        Debris(pos, new Color(0.25f, 0.22f, 0.2f), 14, 12f, 0.2f, 1.4f);
        Debris(pos, new Color(1f, 0.6f, 0.2f), 8, 14f, 0.1f, 0.6f);
        SFX.Play3D("explosion", pos, 1f, Random.Range(0.9f, 1.1f));
        var p = Player.I;
        if (p != null)
        {
            float d = Vector3.Distance(p.transform.position, pos);
            p.AddShake(Mathf.Clamp01(1f - d / (radius * 6f)) * 1.2f);
        }
    }
}
