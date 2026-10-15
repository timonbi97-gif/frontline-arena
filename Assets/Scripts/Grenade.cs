using System.Collections.Generic;
using UnityEngine;

// Frag grenade: ballistic arc, bounces, explodes after a fuse.
public class Grenade : MonoBehaviour
{
    public static readonly List<Grenade> All = new List<Grenade>();
    const float G = 15f;

    Vector3 vel;
    float fuse = 2.6f, dmg;
    bool fromPlayer;
    Vector3 spin;

    public static void Throw(Vector3 from, Vector3 target, float dmg, bool fromPlayer)
    {
        Vector3 d = target - from;
        Vector3 flat = new Vector3(d.x, 0, d.z);
        float t = Mathf.Clamp(flat.magnitude / 15f, 0.7f, 1.8f);
        Vector3 v = d / t + Vector3.up * 0.5f * G * t;
        v += Random.insideUnitSphere * 1.2f;

        var g = new GameObject("Grenade");
        g.transform.position = from;
        var body = GA.Lit(new Color(0.2f, 0.24f, 0.16f), 0.3f);
        GA.Prim(PrimitiveType.Sphere, g.transform, Vector3.zero, new Vector3(0.09f, 0.11f, 0.09f), body, false, 0, true);
        GA.Prim(PrimitiveType.Cube, g.transform, new Vector3(0, 0.065f, 0), new Vector3(0.03f, 0.03f, 0.05f), GA.Lit(new Color(0.5f, 0.5f, 0.5f), 0.6f), false, 0, false);
        var gr = g.AddComponent<Grenade>();
        gr.vel = v; gr.dmg = dmg; gr.fromPlayer = fromPlayer;
        gr.spin = Random.insideUnitSphere * 600f;
        All.Add(gr);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        fuse -= dt;
        vel.y -= G * dt;
        Vector3 step = vel * dt;
        float len = step.magnitude;
        if (len > 0f && Physics.SphereCast(transform.position, 0.06f, step / len, out var hit, len, GA.MaskWorld, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point + hit.normal * 0.07f;
            vel = Vector3.Reflect(vel, hit.normal) * 0.4f;
            if (vel.magnitude > 1.5f) SFX.Play3D("tink", transform.position, 0.6f, Random.Range(0.8f, 1.1f));
            spin *= 0.5f;
            if (hit.normal.y > 0.6f && vel.magnitude < 1f) vel = Vector3.zero;
        }
        else transform.position += step;
        transform.Rotate(spin * dt);
        if (fuse <= 0f)
        {
            Projectile.Explode(transform.position + Vector3.up * 0.2f, 7.5f, dmg, true, fromPlayer);
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        All.Remove(this);
    }
}
