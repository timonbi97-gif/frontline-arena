using UnityEngine;

public class Projectile : MonoBehaviour
{
    Vector3 vel;
    float dmg, splash, radius, smokeT, life = 5f;
    bool fromPlayer;
    Vector3 origin;

    public static void Spawn(Vector3 pos, Vector3 dir, float speed, float dmg, bool fromPlayer, float splash, Color c, float size)
    {
        GameObject g;
        if (splash > 0f)
        {
            g = new GameObject("Rocket");
            g.transform.position = pos;
            g.transform.rotation = Quaternion.LookRotation(dir);
            GA.Prim(PrimitiveType.Cylinder, g.transform, Vector3.zero, new Vector3(0.12f, 0.3f, 0.12f), GA.Lit(GA.C(0x4d5a34), 0.3f), false, 0, false)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);
            GA.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 0, -0.32f), Vector3.one * 0.18f, GA.Emissive(c, 5f), false, 0, false);
            var l = g.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = 6f; l.intensity = 2f; l.shadows = LightShadows.None;
        }
        else
        {
            g = GA.Prim(PrimitiveType.Sphere, null, pos, Vector3.one * size, GA.Emissive(c, 4f), false, 0, false);
        }
        var p = g.AddComponent<Projectile>();
        p.vel = dir * speed;
        p.dmg = dmg;
        p.fromPlayer = fromPlayer;
        p.splash = splash;
        p.radius = Mathf.Max(0.05f, size * 0.5f);
        p.origin = pos;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        life -= dt;
        Vector3 step = vel * dt;
        float len = step.magnitude;
        int mask = fromPlayer ? GA.MaskPlayerShot : GA.MaskEnemyShot;
        var q = fromPlayer ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        if (len > 0f && Physics.SphereCast(transform.position, radius, step / len, out var hit, len, mask, q))
        {
            Hit(hit);
            return;
        }
        transform.position += step;
        if (splash > 0f)
        {
            smokeT -= dt;
            if (smokeT <= 0f)
            {
                smokeT = 0.03f;
                Fx.Smoke(transform.position - vel.normalized * 0.4f, 0.7f, 0.6f, 0.35f);
            }
        }
        if (life <= 0f)
        {
            if (splash > 0f) Explode(transform.position, splash, dmg, true);
            Destroy(gameObject);
        }
    }

    void Hit(RaycastHit hit)
    {
        if (splash > 0f)
        {
            Explode(hit.point + hit.normal * 0.3f, splash, dmg, true);
        }
        else
        {
            var pl = hit.collider.GetComponent<Player>();
            if (pl != null) pl.Damage(dmg, origin);
            else Fx.Debris(hit.point, new Color(1f, 0.5f, 0.2f), 3, 3f, 0.06f, 0.3f);
        }
        Destroy(gameObject);
    }

    public static void Explode(Vector3 pos, float radius, float dmg, bool hurtsPlayer)
    {
        Fx.Explosion(pos, radius * 0.5f);
        bool anyHit = false, kill = false;
        for (int i = Enemy.All.Count - 1; i >= 0; i--)
        {
            if (i >= Enemy.All.Count) continue;
            var e = Enemy.All[i];
            float d = Vector3.Distance(e.Center, pos) - e.scale * 0.4f;
            if (d >= radius) continue;
            float k = 1f - Mathf.Max(0f, d) / radius * 0.7f;
            kill |= e.Damage(dmg * k, false, pos, (e.Center - pos).normalized);
            anyHit = true;
        }
        var p = Player.I;
        if (p != null)
        {
            if (anyHit) p.weapons.RegisterHit(kill, false);
            if (hurtsPlayer)
            {
                float d = Vector3.Distance(p.transform.position + Vector3.up, pos);
                if (d < radius) p.Damage(dmg * 0.3f * (1f - d / radius), pos);
            }
        }
    }
}
