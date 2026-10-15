using System.Collections.Generic;
using UnityEngine;

public enum EType { Runner, Soldier, Brute, Boss }

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();
    public static readonly string[] Names = { "Sprinter", "Soldat", "Koloss", "BOSS" };

    public EType type;
    public float hp, maxHp;
    public bool dead;
    public int score;
    public Color partColor;
    public float scale;

    float speed, dmg, range, cd, prefDist;
    CharacterController cc;
    Transform armL, armR, legL, legR, headT, muzzle;
    Collider headCol;
    Renderer[] rends;
    MaterialPropertyBlock mpb;
    float flashT;
    readonly List<Vector3> path = new List<Vector3>();
    int pathI;
    float repathT, losT, forcePath;
    bool los;
    float vy, atkT, walkPhase, strafeT, strafeDir = 1f, stuckT, burstT, summonT, stompT, swingT, deathT;
    int burstLeft;
    Vector3 lastPos, deathAxis;

    public Vector3 Center => transform.position + Vector3.up * 0.9f * scale;

    public static Enemy Spawn(EType t, Vector3 pos, int wave, float diff)
    {
        var g = new GameObject(t.ToString());
        g.transform.position = pos;
        var e = g.AddComponent<Enemy>();
        e.Setup(t, wave, diff);
        All.Add(e);
        return e;
    }

    void Setup(EType t, int wave, float diff)
    {
        type = t;
        gameObject.layer = GA.LayerEnemy;
        float hpMul = (1f + 0.12f * (wave - 1)) * diff;
        float dmgMul = (1f + 0.05f * (wave - 1));
        Color body, accent, eye = new Color(1f, 0.85f, 0.2f);
        switch (t)
        {
            case EType.Runner:
                scale = 0.9f; maxHp = 45; speed = 7.4f; dmg = 9; range = 1.7f; cd = 0.8f; score = 100;
                body = GA.C(0xc23b22); accent = GA.C(0x5a1a10); break;
            case EType.Soldier:
                scale = 1f; maxHp = 75; speed = 4.2f; dmg = 7; range = 38f; cd = 1.6f; prefDist = 16f; score = 150;
                body = GA.C(0x4b5d32); accent = GA.C(0x2b2f24); eye = new Color(1f, 0.25f, 0.1f); break;
            case EType.Brute:
                scale = 1.55f; maxHp = 320; speed = 3.6f; dmg = 26; range = 2.5f; cd = 1.4f; score = 350;
                body = GA.C(0x6a3a8a); accent = GA.C(0x2e1a3d); eye = new Color(0.3f, 1f, 0.4f); break;
            default:
                scale = 2.8f; maxHp = 2400; speed = 3.3f; dmg = 13; range = 55f; cd = 2.4f; prefDist = 14f; score = 2500;
                body = GA.C(0x262626); accent = GA.C(0x7a0f0f); eye = new Color(1f, 0.1f, 0.05f); break;
        }
        maxHp *= hpMul; hp = maxHp;
        dmg *= dmgMul;
        speed *= Random.Range(0.92f, 1.08f);
        partColor = body;

        float s = scale;
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.5f * s; cc.radius = 0.42f * s; cc.center = new Vector3(0, 0.75f * s, 0);
        cc.stepOffset = 0.45f; cc.skinWidth = 0.05f * s; cc.slopeLimit = 50f;
        Build(body, accent, eye);
        rends = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        atkT = Random.Range(0.5f, 1.5f);
        repathT = Random.Range(0f, 0.5f);
        summonT = 15f;
        lastPos = transform.position;
    }

    void Build(Color body, Color accent, Color eye)
    {
        float s = scale;
        var mB = GA.Lit(body, 0.35f); var mA = GA.Lit(accent, 0.2f); var mE = GA.Emissive(eye, 3f);
        var dark = GA.Lit(GA.C(0x1b1b1b), 0.4f);
        var tr = transform;
        int L = GA.LayerEnemy;

        GA.Prim(PrimitiveType.Cube, tr, new Vector3(0, 1.05f * s, 0), new Vector3(0.75f, 0.7f, 0.42f) * s, mB, false, L);
        GA.Prim(PrimitiveType.Cube, tr, new Vector3(0, 0.68f * s, 0), new Vector3(0.62f, 0.18f, 0.36f) * s, mA, false, L);
        GA.Prim(PrimitiveType.Cube, tr, new Vector3(0, 1.1f * s, 0.215f * s), new Vector3(0.3f, 0.2f, 0.02f) * s, mE, false, L, false);

        legL = GA.Pivot(tr, new Vector3(-0.18f * s, 0.62f * s, 0));
        GA.Prim(PrimitiveType.Cube, legL, new Vector3(0, -0.3f * s, 0), new Vector3(0.22f, 0.62f, 0.26f) * s, mA, false, L);
        legR = GA.Pivot(tr, new Vector3(0.18f * s, 0.62f * s, 0));
        GA.Prim(PrimitiveType.Cube, legR, new Vector3(0, -0.3f * s, 0), new Vector3(0.22f, 0.62f, 0.26f) * s, mA, false, L);

        armL = GA.Pivot(tr, new Vector3(-0.5f * s, 1.33f * s, 0));
        GA.Prim(PrimitiveType.Cube, armL, new Vector3(0, -0.32f * s, 0), new Vector3(0.2f, 0.66f, 0.22f) * s, mB, false, L);
        armR = GA.Pivot(tr, new Vector3(0.5f * s, 1.33f * s, 0));
        GA.Prim(PrimitiveType.Cube, armR, new Vector3(0, -0.32f * s, 0), new Vector3(0.2f, 0.66f, 0.22f) * s, mB, false, L);

        headT = GA.Pivot(tr, new Vector3(0, 1.72f * s, 0));
        var head = GA.Prim(PrimitiveType.Cube, headT, Vector3.zero, Vector3.one * 0.46f * s, mB, true, L);
        head.name = "Head";
        headCol = head.GetComponent<Collider>();
        headCol.isTrigger = true;
        GA.Prim(PrimitiveType.Cube, headT, new Vector3(-0.1f * s, 0.04f * s, 0.23f * s), new Vector3(0.1f, 0.06f, 0.03f) * s, mE, false, L, false);
        GA.Prim(PrimitiveType.Cube, headT, new Vector3(0.1f * s, 0.04f * s, 0.23f * s), new Vector3(0.1f, 0.06f, 0.03f) * s, mE, false, L, false);

        switch (type)
        {
            case EType.Runner:
                GA.Prim(PrimitiveType.Cube, armL, new Vector3(0, -0.7f * s, 0.06f * s), new Vector3(0.06f, 0.18f, 0.06f) * s, mE, false, L, false);
                GA.Prim(PrimitiveType.Cube, armR, new Vector3(0, -0.7f * s, 0.06f * s), new Vector3(0.06f, 0.18f, 0.06f) * s, mE, false, L, false);
                GA.Prim(PrimitiveType.Cube, headT, new Vector3(0, 0.33f * s, 0), new Vector3(0.04f, 0.25f, 0.04f) * s, mA, false, L);
                break;
            case EType.Soldier:
                GA.Prim(PrimitiveType.Cube, headT, new Vector3(0, 0.17f * s, -0.02f * s), new Vector3(0.54f, 0.16f, 0.54f) * s, mA, false, L);
                GA.Prim(PrimitiveType.Cube, armR, new Vector3(0, -0.62f * s, 0.22f * s), new Vector3(0.1f, 0.12f, 0.62f) * s, dark, false, L);
                muzzle = GA.Pivot(armR, new Vector3(0, -0.62f * s, 0.56f * s), "Muzzle");
                GA.Prim(PrimitiveType.Cube, tr, new Vector3(0, 1.1f * s, -0.28f * s), new Vector3(0.5f, 0.5f, 0.18f) * s, mA, false, L);
                break;
            case EType.Brute:
                GA.Prim(PrimitiveType.Cube, armL, new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.26f, 0.4f) * s, mA, false, L);
                GA.Prim(PrimitiveType.Cube, armR, new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.26f, 0.4f) * s, mA, false, L);
                for (int i = -1; i <= 1; i++)
                    GA.Prim(PrimitiveType.Cube, tr, new Vector3(i * 0.2f * s, 1.3f * s, -0.26f * s), new Vector3(0.08f, 0.25f, 0.12f) * s, mE, false, L, false);
                GA.Prim(PrimitiveType.Cube, armL, new Vector3(0, -0.72f * s, 0), new Vector3(0.3f, 0.25f, 0.3f) * s, dark, false, L);
                GA.Prim(PrimitiveType.Cube, armR, new Vector3(0, -0.72f * s, 0), new Vector3(0.3f, 0.25f, 0.3f) * s, dark, false, L);
                break;
            default:
                GA.Prim(PrimitiveType.Cube, headT, new Vector3(-0.22f * s, 0.25f * s, 0), new Vector3(0.07f, 0.3f, 0.07f) * s, mE, false, L, false).transform.localRotation = Quaternion.Euler(0, 0, 25);
                GA.Prim(PrimitiveType.Cube, headT, new Vector3(0.22f * s, 0.25f * s, 0), new Vector3(0.07f, 0.3f, 0.07f) * s, mE, false, L, false).transform.localRotation = Quaternion.Euler(0, 0, -25);
                GA.Prim(PrimitiveType.Cylinder, armL, new Vector3(0, -0.6f * s, 0.2f * s), new Vector3(0.18f, 0.3f, 0.18f) * s, dark, false, L).transform.localRotation = Quaternion.Euler(90, 0, 0);
                GA.Prim(PrimitiveType.Cylinder, armR, new Vector3(0, -0.6f * s, 0.2f * s), new Vector3(0.18f, 0.3f, 0.18f) * s, dark, false, L).transform.localRotation = Quaternion.Euler(90, 0, 0);
                GA.Prim(PrimitiveType.Cube, tr, new Vector3(0, 1.45f * s, 0), new Vector3(1.1f, 0.15f, 0.5f) * s, mA, false, L);
                muzzle = GA.Pivot(tr, new Vector3(0, 1.1f * s, 0.45f * s), "Muzzle");
                break;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (dead) { DeathAnim(dt); return; }
        var p = Player.I;
        if (p == null) return;

        Vector3 pos = transform.position, pp = p.transform.position;
        Vector3 to = pp - pos; to.y = 0f;
        float dist = to.magnitude;
        Vector3 toN = dist > 0.01f ? to / dist : transform.forward;

        losT -= dt;
        if (losT <= 0f)
        {
            losT = 0.2f + Random.value * 0.15f;
            Vector3 eye = pos + Vector3.up * 1.6f * scale;
            los = dist < 150f && !Physics.Linecast(eye, p.EyePos, GA.MaskWorld, QueryTriggerInteraction.Ignore);
        }
        forcePath -= dt;

        bool ranged = type == EType.Soldier || type == EType.Boss;
        Vector3 moveDir = Vector3.zero;
        float spd = speed;

        if (p.dead) moveDir = Vector3.zero;
        else if (ranged && los && dist < range && forcePath <= 0f)
        {
            if (dist > prefDist + 4f) moveDir = toN;
            else if (dist < prefDist - 4f) moveDir = -toN * 0.8f;
            strafeT -= dt;
            if (strafeT <= 0f) { strafeT = Random.Range(1f, 2.5f); strafeDir = Random.value < 0.5f ? -1f : 1f; }
            moveDir += Vector3.Cross(Vector3.up, toN) * strafeDir * 0.7f;
            spd = speed * 0.8f;
        }
        else if (los && dist < 60f && forcePath <= 0f) moveDir = toN;
        else
        {
            repathT -= dt;
            if ((repathT <= 0f || pathI >= path.Count) && Pathfinder.TryUse())
            {
                repathT = Random.Range(0.9f, 1.6f);
                pathI = 0;
                if (!Pathfinder.Find(pos, pp, path)) path.Clear();
            }
            if (pathI < path.Count)
            {
                Vector3 d = path[pathI] - pos; d.y = 0f;
                if (d.magnitude < 1.2f * scale) pathI++;
                moveDir = d.normalized;
            }
            else moveDir = toN;
        }
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
        if (!ranged && dist < range * 0.75f) moveDir = Vector3.zero;

        if (cc.isGrounded && vy < 0f) vy = -2f;
        vy -= 20f * dt;
        cc.Move((moveDir * spd + Vector3.up * vy) * dt);

        Vector3 face = los || dist < 6f ? toN : moveDir;
        if (face.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * 8f);

        stuckT += dt;
        if (stuckT > 1f)
        {
            float moved = (transform.position - lastPos).magnitude;
            if (moveDir.sqrMagnitude > 0.1f && moved < spd * 0.15f)
            {
                repathT = 0f;
                forcePath = 2.5f;
                strafeDir = -strafeDir;
            }
            lastPos = transform.position;
            stuckT = 0f;
        }

        // attacks
        atkT -= dt;
        stompT -= dt;
        if (!p.dead)
        {
            float vdiff = Mathf.Abs(pp.y - pos.y);
            switch (type)
            {
                case EType.Runner:
                case EType.Brute:
                    if (dist < range + 0.5f && vdiff < 2.5f && atkT <= 0f)
                    {
                        atkT = cd; swingT = 1f;
                        p.Damage(dmg, pos);
                        SFX.Play3D("melee", pos, 0.9f, type == EType.Brute ? 0.7f : 1.1f);
                    }
                    break;
                case EType.Soldier:
                    if (los && dist < range && atkT <= 0f && burstLeft == 0)
                    {
                        burstLeft = 3; burstT = 0f;
                        atkT = cd + Random.Range(0f, 0.8f);
                    }
                    break;
                default:
                    if (los && dist < range && atkT <= 0f)
                    {
                        atkT = cd;
                        if (Random.value < 0.5f) Volley(p, 9, 60f);
                        else { burstLeft = 6; burstT = 0f; }
                    }
                    if (dist < 5.5f && stompT <= 0f)
                    {
                        stompT = 3f;
                        p.Damage(dmg * 2.2f, pos);
                        Fx.Smoke(pos + Vector3.up * 0.3f, 6f, 1f, 0.5f);
                        Fx.Debris(pos, GA.C(0x5a5048), 12, 9f, 0.25f, 1f);
                        SFX.Play3D("explosion", pos, 0.8f, 1.3f);
                        p.AddShake(0.8f);
                    }
                    summonT -= dt;
                    if (summonT <= 0f)
                    {
                        summonT = 18f;
                        GameManager.I.SummonMinions(pos, 3);
                    }
                    break;
            }
            if (burstLeft > 0)
            {
                burstT -= dt;
                if (burstT <= 0f)
                {
                    burstT = type == EType.Boss ? 0.1f : 0.14f;
                    burstLeft--;
                    Shoot(p, type == EType.Boss ? 0.05f : 0.04f, dist);
                }
            }
        }

        Animate(dt, moveDir.magnitude * spd);
    }

    void Shoot(Player p, float inacc, float dist)
    {
        Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f * scale + transform.forward * 0.5f * scale;
        float projSpeed = type == EType.Boss ? 30f : 32f;
        Vector3 target = p.transform.position + Vector3.up * 1.1f + p.Velocity * (dist / projSpeed) * 0.5f;
        Vector3 dir = (target - from).normalized + Random.insideUnitSphere * inacc;
        Projectile.Spawn(from, dir.normalized, projSpeed, dmg, false, 0f, new Color(1f, 0.3f, 0.1f), type == EType.Boss ? 0.35f : 0.22f);
        SFX.Play3D("eshot", from, 0.7f, type == EType.Boss ? 0.7f : 1f);
        Fx.Sphere(from, new Color(1f, 0.6f, 0.2f, 0.9f), 0.15f * scale, 0.35f * scale, 0.06f);
    }

    void Volley(Player p, int count, float arc)
    {
        Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 2f;
        Vector3 baseDir = (p.transform.position + Vector3.up * 1.1f - from).normalized;
        for (int i = 0; i < count; i++)
        {
            float a = -arc / 2f + arc * i / (count - 1);
            Vector3 dir = Quaternion.AngleAxis(a, Vector3.up) * baseDir;
            Projectile.Spawn(from, dir, 22f, dmg * 1.2f, false, 0f, new Color(1f, 0.15f, 0.4f), 0.5f);
        }
        SFX.Play3D("eshot", from, 1f, 0.5f);
    }

    void Animate(float dt, float v)
    {
        walkPhase += dt * v * 1.6f / scale;
        float a = Mathf.Sin(walkPhase) * Mathf.Clamp01(v / 2f) * 35f;
        legL.localRotation = Quaternion.Euler(a, 0, 0);
        legR.localRotation = Quaternion.Euler(-a, 0, 0);
        swingT = Mathf.MoveTowards(swingT, 0f, dt * 3f);
        if (type == EType.Soldier || type == EType.Boss)
        {
            float aim = los ? -85f : -30f;
            armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.Euler(aim, 0, 0), dt * 8f);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, Quaternion.Euler(type == EType.Boss ? aim : aim + 15f, type == EType.Boss ? 0 : 30f, 0), dt * 8f);
        }
        else
        {
            float sw = swingT > 0f ? -120f * Mathf.Sin(swingT * Mathf.PI) : 0f;
            float run = type == EType.Runner ? -40f : 0f;
            armL.localRotation = Quaternion.Euler(-a + run + sw * 0.5f, 0, 0);
            armR.localRotation = Quaternion.Euler(a + run + sw, 0, 0);
        }
        headT.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 1.3f + walkPhase * 0.2f) * 8f, 0);
        if (flashT > 0f)
        {
            flashT -= dt;
            if (flashT <= 0f) foreach (var r in rends) if (r != null) r.SetPropertyBlock(null);
        }
    }

    // Returns true if this hit killed the enemy.
    public bool Damage(float d, bool head, Vector3 point, Vector3 dir)
    {
        if (dead) return false;
        hp -= d;
        flashT = 0.08f;
        mpb.SetColor("_Color", Color.white);
        foreach (var r in rends) if (r != null) r.SetPropertyBlock(mpb);
        forcePath = 0f;
        if (hp <= 0f)
        {
            Die(head, dir);
            return true;
        }
        return false;
    }

    void Die(bool head, Vector3 dir)
    {
        dead = true;
        All.Remove(this);
        cc.enabled = false;
        headCol.enabled = false;
        foreach (var r in rends) if (r != null) r.SetPropertyBlock(null);
        GameManager.I.OnEnemyKilled(this, head);
        Fx.Debris(Center, partColor, 10 + (int)(scale * 6), 6f, 0.15f * scale, 1.2f);
        SFX.Play3D("edie", transform.position, 1f, Random.Range(0.85f, 1.1f) / Mathf.Sqrt(scale));
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        deathAxis = Vector3.Cross(Vector3.up, dir.normalized);
        if (type == EType.Boss)
        {
            for (int i = 0; i < 4; i++) Fx.Explosion(Center + Random.insideUnitSphere * 2f, 4f);
        }
    }

    void DeathAnim(float dt)
    {
        deathT += dt;
        if (deathT < 0.45f) transform.RotateAround(transform.position, deathAxis, dt / 0.45f * 90f);
        else if (deathT > 1.5f) transform.position += Vector3.down * dt * 1.2f;
        if (deathT > 3f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }
}
