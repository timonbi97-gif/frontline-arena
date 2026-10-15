using UnityEngine;

// Enemy bullets (fast tracers with near-miss whiz) and player rockets.
public class Projectile : MonoBehaviour
{
    Vector3 vel;
    float dmg, splash, radius, smokeT, life = 5f;
    bool fromPlayer, whizzed, isBullet;
    Vector3 origin;

    public static void Bullet(Vector3 pos, Vector3 dir, float speed, float dmg, bool fromPlayer)
    {
        var m = GA.Emissive(new Color(1f, 0.75f, 0.35f), 4f);
        var g = GA.Prim(PrimitiveType.Cube, null, pos, new Vector3(0.025f, 0.025f, 2.2f), m, false, 0, false);
        g.transform.rotation = Quaternion.LookRotation(dir);
        var p = g.AddComponent<Projectile>();
        p.vel = dir * speed; p.dmg = dmg; p.fromPlayer = fromPlayer; p.radius = 0.04f; p.origin = pos;
        p.isBullet = true; p.life = 1.6f;
    }

    public static void Rocket(Vector3 pos, Vector3 dir, float speed, float dmg, float splash)
    {
        var g = new GameObject("Rocket");
        g.transform.position = pos;
        g.transform.rotation = Quaternion.LookRotation(dir);
        GA.Prim(PrimitiveType.Cylinder, g.transform, Vector3.zero, new Vector3(0.1f, 0.35f, 0.1f), GA.Lit(GA.C(0x4d5a34), 0.3f), false, 0, false)
            .transform.localRotation = Quaternion.Euler(90, 0, 0);
        GA.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 0, 0.35f), new Vector3(0.1f, 0.1f, 0.2f), GA.Lit(GA.C(0x3a3a3a), 0.4f), false, 0, false);
        GA.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 0, -0.38f), Vector3.one * 0.16f, GA.Emissive(new Color(1f, 0.6f, 0.2f), 8f), false, 0, false);
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.25f); l.range = 8f; l.intensity = 2.5f; l.shadows = LightShadows.None;
        var p = g.AddComponent<Projectile>();
        p.vel = dir * speed; p.dmg = dmg; p.fromPlayer = true; p.splash = splash; p.radius = 0.12f; p.origin = pos;
    }

    float gravity;

    // 40mm grenade: ballistic arc, explodes on impact.
    public static void LauncherGrenade(Vector3 pos, Vector3 dir, float speed, float dmg)
    {
        var g = new GameObject("40mm");
        g.transform.position = pos;
        g.transform.rotation = Quaternion.LookRotation(dir);
        GA.Prim(PrimitiveType.Cylinder, g.transform, Vector3.zero, new Vector3(0.04f, 0.04f, 0.04f), GA.Lit(new Color(0.45f, 0.42f, 0.2f), 0.5f), false, 0, false)
            .transform.localRotation = Quaternion.Euler(90, 0, 0);
        GA.Prim(PrimitiveType.Sphere, g.transform, new Vector3(0, 0, 0.04f), Vector3.one * 0.04f, GA.Lit(new Color(0.2f, 0.22f, 0.15f), 0.4f), false, 0, false);
        var p = g.AddComponent<Projectile>();
        p.vel = dir * speed; p.dmg = dmg; p.fromPlayer = true; p.splash = 5.5f; p.radius = 0.05f; p.origin = pos;
        p.gravity = 9.8f; p.life = 6f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        life -= dt;
        if (gravity > 0f)
        {
            vel.y -= gravity * dt;
            transform.rotation = Quaternion.LookRotation(vel);
        }
        Vector3 step = vel * dt;
        float len = step.magnitude;
        Vector3 start = transform.position;
        int mask = fromPlayer ? GA.MaskPlayerShot : GA.MaskEnemyShot;
        var q = fromPlayer ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
        RaycastHit hit = default;
        bool hitSomething = len > 0f && Physics.SphereCast(start, radius, step / len, out hit, len, mask, q);
        Vector3 end = hitSomething ? hit.point : start + step;

        if (isBullet && !fromPlayer && !whizzed) CheckWhiz(start, end, hitSomething && hit.collider.GetComponent<Player>() != null);

        if (hitSomething) { Hit(hit); return; }
        transform.position = end;
        if (splash > 0f && gravity <= 0f)
        {
            smokeT -= dt;
            if (smokeT <= 0f)
            {
                smokeT = 0.025f;
                Fx.Smoke(transform.position - vel.normalized * 0.45f, 0.8f, 1.2f, 0.35f, 0.7f);
            }
        }
        if (life <= 0f)
        {
            if (splash > 0f) Explode(transform.position, splash, dmg, true, true);
            Destroy(gameObject);
        }
    }

    void CheckWhiz(Vector3 a, Vector3 b, bool hitPlayer)
    {
        var p = Player.I;
        if (p == null || p.dead || hitPlayer) return;
        Vector3 eye = p.EyePos;
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(eye - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
        float d = Vector3.Distance(a + ab * t, eye);
        if (d < 2.5f && t > 0f && t < 1f)
        {
            whizzed = true;
            SFX.Play2D("whiz", Mathf.Lerp(0.8f, 0.25f, d / 2.5f), Random.Range(0.85f, 1.2f));
            p.Suppress(0.25f);
        }
    }

    void Hit(RaycastHit hit)
    {
        if (splash > 0f)
        {
            Explode(hit.point + hit.normal * 0.3f, splash, dmg, true, true);
        }
        else
        {
            var pl = hit.collider.GetComponent<Player>();
            if (pl != null) pl.Damage(dmg, origin);
            else Fx.Impact(hit.point, hit.normal, hit.collider);
        }
        Destroy(gameObject);
    }

    public static void Explode(Vector3 pos, float radius, float dmg, bool hurtsPlayer, bool hurtsEnemies)
    {
        Fx.Explosion(pos, radius * 0.5f);
        bool anyHit = false, kill = false;
        if (hurtsEnemies)
            for (int i = Enemy.All.Count - 1; i >= 0; i--)
            {
                if (i >= Enemy.All.Count) continue;
                var e = Enemy.All[i];
                float d = Vector3.Distance(e.Center, pos) - e.scale * 0.4f;
                if (d >= radius) continue;
                if (Physics.Linecast(pos, e.Center, GA.MaskWorld, QueryTriggerInteraction.Ignore)) continue;
                float k = 1f - Mathf.Max(0f, d) / radius * 0.75f;
                kill |= e.Damage(dmg * k, false, e.Center, (e.Center - pos).normalized);
                anyHit = true;
            }
        var p = Player.I;
        if (p != null)
        {
            if (anyHit) p.weapons.RegisterHit(kill, false);
            if (hurtsPlayer)
            {
                Vector3 c = p.transform.position + Vector3.up;
                float d = Vector3.Distance(c, pos);
                if (d < radius && !Physics.Linecast(pos, c, GA.MaskWorld, QueryTriggerInteraction.Ignore))
                    p.Damage(dmg * 0.5f * (1f - d / radius), pos);
            }
        }
    }
}
