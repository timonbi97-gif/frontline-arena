using System.Collections.Generic;
using UnityEngine;

public static class Fx
{
    static readonly Queue<GameObject> decals = new Queue<GameObject>();
    public static readonly Color BloodColor = new Color(0.32f, 0.02f, 0.02f, 1f);

    // Camera-facing textured quad.
    public static FxAnim Billboard(Vector3 pos, Texture2D tex, Color c, float size0, float size1, float life)
    {
        var m = GA.Fx(c);
        m.mainTexture = tex;
        var g = GA.Prim(PrimitiveType.Quad, null, pos, Vector3.one * size0, m, false, 0, false);
        var a = g.AddComponent<FxAnim>();
        a.life = life; a.s0 = Vector3.one * size0; a.s1 = Vector3.one * size1; a.mat = m; a.c0 = c;
        a.billboard = true; a.roll = Random.Range(0f, 360f);
        var cam = Camera.main;
        if (cam != null) g.transform.rotation = cam.transform.rotation;
        return a;
    }

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
            var g = GA.Prim(PrimitiveType.Cube, null, pos, Vector3.one * size * Random.Range(0.5f, 1.2f), m, false, 0, false);
            g.transform.rotation = Random.rotation;
            var a = g.AddComponent<FxAnim>();
            a.life = life * Random.Range(0.6f, 1.2f);
            a.s0 = g.transform.localScale; a.s1 = a.s0 * 0.6f;
            a.vel = (Random.onUnitSphere + Vector3.up * 0.8f) * speed * Random.Range(0.4f, 1f);
            a.gravity = 15f; a.spin = Random.insideUnitSphere * 720f; a.fade = false;
            a.bounceY = 0.05f;
        }
    }

    public static void Smoke(Vector3 pos, float size, float life, float alpha = 0.45f, float shade = 0.55f)
    {
        var a = Billboard(pos, TexGen.Smoke(), new Color(shade, shade, shade * 1.02f, alpha), size * 0.35f, size, life);
        a.vel = Vector3.up * 0.7f + Random.insideUnitSphere * 0.4f;
        a.drag = 0.6f;
        a.rollSpeed = Random.Range(-20f, 20f);
        a.fadeIn = true;
    }

    public static void MuzzleFlash(Vector3 pos, float size)
    {
        var a = Billboard(pos, TexGen.Flash(), new Color(3f, 2.3f, 1.3f, 1f), size, size * 1.3f, 0.05f);
        a.fade = true;
    }

    public static void Tracer(Vector3 a, Vector3 b, Color c)
    {
        var d = b - a; float len = d.magnitude;
        if (len < 1f) return;
        var m = GA.Fx(c);
        var g = GA.Prim(PrimitiveType.Cube, null, (a + b) * 0.5f, new Vector3(0.012f, 0.012f, len), m, false, 0, false);
        g.transform.rotation = Quaternion.LookRotation(d);
        var f = g.AddComponent<FxAnim>();
        f.life = 0.05f; f.s0 = f.s1 = g.transform.localScale; f.mat = m; f.c0 = c;
    }

    static void AddDecal(Vector3 point, Vector3 normal, Material m, float size)
    {
        var q = GA.Prim(PrimitiveType.Quad, null, point + normal * 0.012f, Vector3.one * size, m, false, 0, false);
        q.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        decals.Enqueue(q);
        while (decals.Count > 90)
        {
            var old = decals.Dequeue();
            if (old != null) Object.Destroy(old);
        }
    }

    public static void Impact(Vector3 point, Vector3 normal, Collider col)
    {
        Color c = new Color(0.6f, 0.58f, 0.55f);
        var r = col != null ? col.GetComponent<Renderer>() : null;
        if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color")) c = r.sharedMaterial.color;
        var dust = Color.Lerp(c, new Color(0.6f, 0.57f, 0.52f), 0.5f);
        var puff = Billboard(point + normal * 0.15f, TexGen.Smoke(), new Color(dust.r, dust.g, dust.b, 0.55f), 0.15f, 0.9f, 0.6f);
        puff.vel = normal * 0.8f + Vector3.up * 0.3f;
        puff.drag = 2f;
        Debris(point + normal * 0.05f, c * 0.8f, 3, 3f, 0.04f, 0.5f);
        Billboard(point + normal * 0.05f, TexGen.Flash(), new Color(1f, 0.85f, 0.55f, 0.9f), 0.12f, 0.2f, 0.04f);
        AddDecal(point, normal, GA.Decal(0, new Color(0.05f, 0.05f, 0.05f, 0.9f)), Random.Range(0.1f, 0.15f));
        if (Random.value < 0.5f) SFX.Play3D("impact", point, 0.5f, Random.Range(0.85f, 1.2f));
    }

    public static void Blood(Vector3 point, Vector3 dir, float scale = 1f)
    {
        var mist = Billboard(point, TexGen.Smoke(), new Color(0.45f, 0.03f, 0.03f, 0.75f), 0.15f * scale, 0.7f * scale, 0.35f);
        mist.vel = dir * 1.2f;
        mist.drag = 3f;
        var m = GA.Lit(BloodColor, 0.6f);
        for (int i = 0; i < 4; i++)
        {
            var g = GA.Prim(PrimitiveType.Sphere, null, point, Vector3.one * 0.035f, m, false, 0, false);
            var a = g.AddComponent<FxAnim>();
            a.life = 0.5f; a.s0 = g.transform.localScale; a.s1 = a.s0 * 0.5f; a.fade = false;
            a.vel = (dir + Random.insideUnitSphere * 0.6f) * Random.Range(2f, 4f);
            a.gravity = 12f;
        }
        if (Physics.Raycast(point, dir, out var hit, 3f, GA.MaskWorld, QueryTriggerInteraction.Ignore))
            AddDecal(hit.point, hit.normal, GA.Decal(1, new Color(0.35f, 0.02f, 0.02f, 0.85f)), Random.Range(0.5f, 0.9f) * scale);
    }

    public static void BloodPool(Vector3 pos, float size)
    {
        if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, GA.MaskWorld, QueryTriggerInteraction.Ignore))
            AddDecal(hit.point, hit.normal, GA.Decal(1, new Color(0.3f, 0.01f, 0.01f, 0.9f)), size);
    }

    public static void Shell(Vector3 pos, Vector3 vel, Color c, float length)
    {
        var g = GA.Prim(PrimitiveType.Cylinder, null, pos, new Vector3(0.011f, length, 0.011f), GA.Lit(c, 0.8f), false, 0, false);
        g.transform.rotation = Random.rotation;
        var a = g.AddComponent<FxAnim>();
        a.life = 1.2f; a.s0 = a.s1 = g.transform.localScale; a.fade = false;
        a.vel = vel; a.gravity = 9.8f; a.spin = Random.insideUnitSphere * 1400f;
        a.bounceY = 0.06f;
    }

    public static void Explosion(Vector3 pos, float radius)
    {
        var fire = Billboard(pos, TexGen.Flash(), new Color(3f, 2.1f, 1.1f, 1f), radius * 0.6f, radius * 2.4f, 0.25f);
        AddLight(fire, new Color(1f, 0.6f, 0.25f), 9f, radius * 5f);
        for (int i = 0; i < 5; i++)
        {
            var f = Billboard(pos + Random.insideUnitSphere * radius * 0.3f, TexGen.Smoke(), new Color(1f, 0.55f, 0.18f, 0.95f), radius * 0.5f, radius * 1.6f, Random.Range(0.25f, 0.45f));
            f.vel = Random.insideUnitSphere * radius * 2f + Vector3.up * radius;
            f.drag = 4f;
        }
        for (int i = 0; i < 9; i++)
        {
            var s = Billboard(pos + Random.insideUnitSphere * radius * 0.4f, TexGen.Smoke(), new Color(0.18f, 0.17f, 0.16f, 0.75f), radius * 0.6f, radius * Random.Range(2f, 3f), Random.Range(2.5f, 4.5f));
            s.vel = Random.insideUnitSphere * radius * 1.2f + Vector3.up * radius * 0.8f;
            s.drag = 1.6f;
            s.rollSpeed = Random.Range(-25f, 25f);
            s.fadeIn = true;
        }
        Debris(pos, new Color(0.22f, 0.2f, 0.18f), 14, 12f, 0.15f, 1.4f);
        for (int i = 0; i < 8; i++)
        {
            var sp = Billboard(pos, TexGen.Flash(), new Color(1f, 0.7f, 0.3f, 1f), 0.25f, 0.05f, 0.6f);
            sp.vel = (Random.onUnitSphere + Vector3.up) * Random.Range(8f, 16f);
            sp.gravity = 12f;
        }
        if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out var hit, radius, GA.MaskWorld, QueryTriggerInteraction.Ignore))
            AddDecal(hit.point, hit.normal, GA.Decal(0, new Color(0.05f, 0.04f, 0.04f, 0.85f)), radius * 1.4f);
        SFX.Play3D("explosion", pos, 1f, Random.Range(0.9f, 1.1f));
        var p = Player.I;
        if (p != null)
        {
            float d = Vector3.Distance(p.transform.position, pos);
            p.AddShake(Mathf.Clamp01(1f - d / (radius * 8f)) * 1.3f);
        }
    }
}
