using System.Collections.Generic;
using UnityEngine;

public enum EType { Runner, Soldier, Brute, Boss }

// Human soldier enemy: articulated body, burst fire with reaction time, reloads, grenades.
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();
    public static readonly string[] Names = { "Stürmer", "Schütze", "MG-Schütze", "Juggernaut" };

    public EType type;
    public float hp, maxHp;
    public bool dead;
    public int score;
    public Color partColor = Fx.BloodColor;
    public float scale = 1f;

    // stats
    float speed, dmg, range, prefDist, spreadDeg, burstGap, armor = 1f;
    int burstMin, burstMax, magSize, ammo;
    float reloadTime, grenadeCd, shotPitch = 1f;
    string shotSfx;
    public int weaponId;

    CharacterController cc;
    Transform hips, spine, headT, thighL, thighR, kneeL, kneeR, muzzle;
    Collider headCol;
    Renderer[] rends;
    MaterialPropertyBlock mpb;
    float flashT, flinch;
    readonly List<Vector3> path = new List<Vector3>();
    int pathI;
    float repathT, losT, forcePath, seenT = -99f, reactT, reloadT, grenT, meleeT;
    bool los, reloading;
    float vy, atkT, walkPhase, strafeT, strafeDir = 1f, stuckT, burstT, deathT, summonT;
    int burstLeft;
    Vector3 lastPos, deathAxis, lastSeenPos;

    public Vector3 Center => transform.position + Vector3.up * 1.0f * scale;

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
        float hpMul = (1f + 0.1f * (wave - 1)) * diff;
        float dmgMul = 1f + 0.04f * (wave - 1);
        int camo;
        switch (t)
        {
            case EType.Runner:
                scale = 0.97f; maxHp = 70; speed = 5.8f; dmg = 3f; range = 22f; prefDist = 8f; spreadDeg = 5.5f;
                burstMin = 3; burstMax = 5; burstGap = 0.09f; magSize = 25; reloadTime = 2.4f; score = 100;
                weaponId = Arsenal.Random(WCat.SMG); camo = 2; break;
            case EType.Soldier:
                scale = 1f; maxHp = 90; speed = 4f; dmg = 4.5f; range = 45f; prefDist = 20f; spreadDeg = 3.4f;
                burstMin = 2; burstMax = 3; burstGap = 0.14f; magSize = 30; reloadTime = 2.8f; grenadeCd = wave >= 3 ? 20f : 0f; score = 150;
                weaponId = Random.value < 0.8f ? Arsenal.Random(WCat.Rifle) : Arsenal.Random(WCat.Shotgun, WCat.DMR); camo = 0; break;
            case EType.Brute:
                scale = 1.05f; maxHp = 180; armor = 0.8f; speed = 3f; dmg = 4f; range = 50f; prefDist = 25f; spreadDeg = 4.6f;
                burstMin = 6; burstMax = 10; burstGap = 0.1f; magSize = 100; reloadTime = 5f; score = 350;
                weaponId = Arsenal.Random(WCat.LMG); camo = 1; break;
            default:
                scale = 1.3f; maxHp = 1200; armor = 0.6f; speed = 2.6f; dmg = 3.5f; range = 55f; prefDist = 12f; spreadDeg = 5f;
                burstMin = 15; burstMax = 25; burstGap = 0.07f; magSize = 250; reloadTime = 4.5f; grenadeCd = 14f; score = 2500;
                weaponId = Arsenal.Find("M249"); camo = 3; break;
        }
        var wd = Arsenal.All[weaponId];
        shotSfx = wd.sfx;
        shotPitch = wd.pitch;
        maxHp *= hpMul; hp = maxHp;
        dmg *= dmgMul;
        speed *= Random.Range(0.92f, 1.08f);
        ammo = magSize;

        float s = scale;
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.55f * s; cc.radius = 0.36f * s; cc.center = new Vector3(0, 0.775f * s, 0);
        cc.stepOffset = 0.45f; cc.skinWidth = 0.04f * s; cc.slopeLimit = 50f;
        Build(camo);
        rends = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        atkT = Random.Range(0.5f, 1.5f);
        repathT = Random.Range(0f, 0.5f);
        grenT = Random.Range(4f, grenadeCd + 4f);
        summonT = 20f;
        lastPos = transform.position;
    }

    // ---------------------------------------------------------------- body

    void Build(int camo)
    {
        int L = GA.LayerEnemy;
        float s = scale;
        var cloth = GA.Cloth(camo, Color.white);
        Color gearCol = type == EType.Boss ? GA.C(0x1c1d1f) : type == EType.Brute ? GA.C(0x4a4436) : GA.C(0x3a3f2c);
        var gear = GA.Lit(gearCol, 0.15f);
        var skin = GA.Lit(new Color(0.72f, 0.54f, 0.43f), 0.3f);
        var glove = GA.Lit(new Color(0.08f, 0.08f, 0.08f), 0.2f);
        var boot = GA.Lit(new Color(0.12f, 0.1f, 0.08f), 0.3f);
        var gun = GA.Lit(new Color(0.07f, 0.07f, 0.075f), 0.55f);
        var helmetM = GA.Lit(type == EType.Boss ? GA.C(0x18191b) : type == EType.Runner ? GA.C(0x2a2a2a) : gearCol * 1.1f, 0.25f);

        var root = new GameObject("Body").transform;
        root.SetParent(transform, false);
        root.localScale = Vector3.one * s;
        root.gameObject.layer = L;

        hips = GA.Pivot(root, new Vector3(0, 0.95f, 0), "Hips");
        GA.Prim(PrimitiveType.Cube, hips, new Vector3(0, 0, 0), new Vector3(0.34f, 0.2f, 0.22f), cloth, false, L);

        thighL = GA.Pivot(hips, new Vector3(-0.1f, -0.05f, 0), "ThighL");
        thighR = GA.Pivot(hips, new Vector3(0.1f, -0.05f, 0), "ThighR");
        foreach (var th in new[] { thighL, thighR })
        {
            GA.Prim(PrimitiveType.Cube, th, new Vector3(0, -0.21f, 0), new Vector3(0.15f, 0.44f, 0.17f), cloth, false, L);
            var knee = GA.Pivot(th, new Vector3(0, -0.43f, 0), "Knee");
            GA.Prim(PrimitiveType.Cube, knee, new Vector3(0, -0.2f, 0), new Vector3(0.13f, 0.42f, 0.14f), cloth, false, L);
            GA.Prim(PrimitiveType.Cube, knee, new Vector3(0, 0.0f, 0.07f), new Vector3(0.14f, 0.1f, 0.06f), gear, false, L);
            GA.Prim(PrimitiveType.Cube, knee, new Vector3(0, -0.44f, 0.04f), new Vector3(0.14f, 0.1f, 0.27f), boot, false, L);
            if (th == thighL) kneeL = knee; else kneeR = knee;
        }

        spine = GA.Pivot(hips, new Vector3(0, 0.08f, 0), "Spine");
        GA.Prim(PrimitiveType.Cube, spine, new Vector3(0, 0.28f, 0), new Vector3(0.4f, 0.5f, 0.23f), cloth, false, L);
        float vestW = type == EType.Boss ? 0.56f : type == EType.Brute ? 0.5f : 0.45f;
        float vestD = type == EType.Boss ? 0.42f : type == EType.Brute ? 0.34f : 0.29f;
        GA.Prim(PrimitiveType.Cube, spine, new Vector3(0, 0.3f, 0), new Vector3(vestW, 0.42f, vestD), gear, false, L);
        for (int i = -1; i <= 1; i++)
            GA.Prim(PrimitiveType.Cube, spine, new Vector3(i * 0.11f, 0.18f, vestD / 2 + 0.03f), new Vector3(0.09f, 0.12f, 0.06f), gear, false, L);
        if (type != EType.Runner)
            GA.Prim(PrimitiveType.Cube, spine, new Vector3(0, 0.32f, -vestD / 2 - 0.08f), new Vector3(0.3f, 0.36f, 0.16f), gear, false, L);
        if (type == EType.Boss)
        {
            GA.Prim(PrimitiveType.Cube, spine, new Vector3(-0.3f, 0.5f, 0), new Vector3(0.22f, 0.14f, 0.3f), gear, false, L);
            GA.Prim(PrimitiveType.Cube, spine, new Vector3(0.3f, 0.5f, 0), new Vector3(0.22f, 0.14f, 0.3f), gear, false, L);
            GA.Prim(PrimitiveType.Cube, hips, new Vector3(0, -0.12f, 0.12f), new Vector3(0.3f, 0.2f, 0.06f), gear, false, L);
        }

        headT = GA.Pivot(spine, new Vector3(0, 0.6f, 0), "Head");
        GA.Prim(PrimitiveType.Cube, headT, new Vector3(0, -0.04f, 0), new Vector3(0.1f, 0.08f, 0.1f), skin, false, L);
        bool mask = type == EType.Runner || type == EType.Boss;
        GA.Prim(PrimitiveType.Sphere, headT, new Vector3(0, 0.1f, 0.01f), new Vector3(0.2f, 0.25f, 0.23f), mask ? GA.Lit(new Color(0.07f, 0.07f, 0.07f), 0.1f) : skin, false, L);
        if (type == EType.Boss)
            GA.Prim(PrimitiveType.Cube, headT, new Vector3(0, 0.12f, 0.11f), new Vector3(0.2f, 0.07f, 0.05f), GA.Lit(new Color(0.02f, 0.02f, 0.03f), 0.95f), false, L);
        else
            GA.Prim(PrimitiveType.Cube, headT, new Vector3(0, 0.13f, 0.115f), new Vector3(0.16f, 0.045f, 0.02f), GA.Lit(new Color(0.03f, 0.03f, 0.03f), 0.9f), false, L, false);
        if (type == EType.Runner)
            GA.Prim(PrimitiveType.Sphere, headT, new Vector3(0, 0.19f, 0.0f), new Vector3(0.23f, 0.13f, 0.25f), helmetM, false, L);
        else
        {
            GA.Prim(PrimitiveType.Sphere, headT, new Vector3(0, 0.19f, -0.01f), new Vector3(0.28f, 0.2f, 0.3f), helmetM, false, L);
            if (type != EType.Boss)
                GA.Prim(PrimitiveType.Cube, headT, new Vector3(0, 0.25f, 0.13f), new Vector3(0.06f, 0.05f, 0.04f), gun, false, L, false);
        }
        var hb = GA.Prim(PrimitiveType.Cube, headT, new Vector3(0, 0.12f, 0.01f), new Vector3(0.3f, 0.36f, 0.32f), gun, true, L);
        hb.name = "Head";
        hb.GetComponent<Renderer>().enabled = false;
        headCol = hb.GetComponent<Collider>();
        headCol.isTrigger = true;

        // real weapon model held at the shoulder, arms built as segments reaching it
        var gunOrigin = new Vector3(0.12f, 0.38f, 0.24f);
        var gp = GunBuilder.Build(Arsenal.All[weaponId], spine, L, false);
        gp.root.localPosition = gunOrigin;
        muzzle = gp.muzzle;

        Vector3 shR = new Vector3(0.22f, 0.5f, 0), shL = new Vector3(-0.22f, 0.5f, 0);
        Vector3 handR = gunOrigin + gp.grip, handL = gunOrigin + gp.fore;
        Vector3 elR = new Vector3(0.3f, 0.24f, handR.z - 0.2f), elL = new Vector3(-0.14f, 0.28f, Mathf.Max(0.1f, handL.z - 0.22f));
        GA.Segment(spine, shR, elR, 0.11f, cloth, L);
        GA.Segment(spine, elR, handR, 0.095f, cloth, L);
        GA.Segment(spine, shL, elL, 0.11f, cloth, L);
        GA.Segment(spine, elL, handL, 0.095f, cloth, L);
        GA.Prim(PrimitiveType.Cube, spine, handR, Vector3.one * 0.085f, glove, false, L);
        GA.Prim(PrimitiveType.Cube, spine, handL, Vector3.one * 0.085f, glove, false, L);
        GA.Prim(PrimitiveType.Cube, spine, shR + new Vector3(0, -0.02f, 0), new Vector3(0.14f, 0.12f, 0.15f), gear, false, L);
        GA.Prim(PrimitiveType.Cube, spine, shL + new Vector3(0, -0.02f, 0), new Vector3(0.14f, 0.12f, 0.15f), gear, false, L);
    }

    // ---------------------------------------------------------------- AI

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
            losT = 0.18f + Random.value * 0.12f;
            Vector3 eye = pos + Vector3.up * 1.6f * scale;
            bool nowLos = dist < 160f && !Physics.Linecast(eye, p.EyePos, GA.MaskWorld, QueryTriggerInteraction.Ignore);
            if (nowLos && !los) reactT = Random.Range(0.7f, 1.3f) * (type == EType.Runner ? 0.8f : 1f);
            los = nowLos;
            if (los) { seenT = Time.time; lastSeenPos = pp; }
        }
        forcePath -= dt;
        reactT -= dt;

        Vector3 moveDir = Vector3.zero;
        float spd = speed;
        bool engaging = los && dist < range && forcePath <= 0f;

        if (p.dead) moveDir = Vector3.zero;
        else if (reloading)
        {
            // fall back while reloading
            moveDir = -toN * 0.6f + Vector3.Cross(Vector3.up, toN) * strafeDir * 0.6f;
            spd = speed * 0.7f;
        }
        else if (engaging)
        {
            if (dist > prefDist + 4f) moveDir = toN;
            else if (dist < prefDist - 4f) moveDir = -toN * 0.7f;
            strafeT -= dt;
            if (strafeT <= 0f) { strafeT = Random.Range(1.2f, 3f); strafeDir = Random.value < 0.5f ? -1f : 1f; }
            moveDir += Vector3.Cross(Vector3.up, toN) * strafeDir * (type == EType.Runner ? 0.9f : 0.55f);
            spd = speed * (burstLeft > 0 ? 0.45f : 0.75f);
        }
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
                if (d.magnitude < 1.2f) pathI++;
                moveDir = d.normalized;
            }
            else moveDir = toN;
        }
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        if (cc.isGrounded && vy < 0f) vy = -2f;
        vy -= 20f * dt;
        cc.Move((moveDir * spd + Vector3.up * vy) * dt);

        Vector3 face = los || dist < 6f ? toN : moveDir;
        if (face.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * 7f);

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

        // combat
        atkT -= dt;
        meleeT -= dt;
        grenT -= dt;
        if (reloading)
        {
            reloadT -= dt;
            if (reloadT <= 0f) { reloading = false; ammo = magSize; }
        }
        if (!p.dead)
        {
            if (dist < 1.9f && meleeT <= 0f && Mathf.Abs(pp.y - pos.y) < 2f)
            {
                meleeT = 1.2f;
                p.Damage(10f, pos);
                flinch = -1f;
                SFX.Play3D("melee", pos, 0.9f);
            }
            else if (engaging && reactT <= 0f && !reloading && atkT <= 0f && burstLeft == 0)
            {
                burstLeft = Random.Range(burstMin, burstMax + 1);
                burstT = 0f;
                atkT = burstLeft * burstGap + Random.Range(1.0f, 2.2f);
            }
            if (burstLeft > 0 && !reloading)
            {
                burstT -= dt;
                if (burstT <= 0f)
                {
                    burstT = burstGap;
                    burstLeft--;
                    Shoot(p, dist);
                    if (--ammo <= 0) { reloading = true; reloadT = reloadTime; burstLeft = 0; strafeDir = Random.value < 0.5f ? -1f : 1f; }
                }
            }
            if (grenadeCd > 0f && grenT <= 0f)
            {
                bool hiding = !los && Time.time - seenT < 6f;
                float gd = Vector3.Distance(pos, lastSeenPos);
                if ((hiding || (los && Random.value < 0.3f)) && gd > 8f && gd < 30f)
                {
                    grenT = grenadeCd + Random.Range(0f, 4f);
                    Grenade.Throw(pos + Vector3.up * 1.7f * scale + transform.forward * 0.4f, lastSeenPos, 60f, false);
                }
                else grenT = 1.5f;
            }
            if (type == EType.Boss)
            {
                summonT -= dt;
                if (summonT <= 0f)
                {
                    summonT = 22f;
                    GameManager.I.SummonMinions(pos, 2);
                }
            }
        }

        Animate(dt, (transform.position - pos).magnitude / dt);
    }

    void Shoot(Player p, float dist)
    {
        Vector3 from = muzzle.position;
        Vector3 target = p.transform.position + Vector3.up * Random.Range(0.9f, 1.6f);
        float spread = spreadDeg;
        spread += Mathf.Min(p.Velocity.magnitude * 0.35f, 2.5f);
        if (p.Crouching) spread *= 1.3f;
        Vector3 dir = (target - from).normalized;
        dir = Quaternion.Euler(Random.Range(-spread, spread) * 0.6f, Random.Range(-spread, spread), 0) * dir;
        dir = Quaternion.AngleAxis(Random.Range(-spread, spread) * 0.5f, Vector3.Cross(dir, Vector3.up)) * dir;
        Projectile.Bullet(from, dir, 160f, dmg, false);
        SFX.Play3D(shotSfx, from, 0.9f, shotPitch * Random.Range(0.93f, 1.07f));
        Fx.MuzzleFlash(from, 0.35f * scale);
        flinch = Mathf.Max(flinch, 0.25f);
    }

    void Animate(float dt, float v)
    {
        v = Mathf.Min(v, 10f);
        walkPhase += dt * v * 1.9f;
        float amt = Mathf.Clamp01(v / 2.5f);
        float sw = Mathf.Sin(walkPhase) * 32f * amt;
        thighL.localRotation = Quaternion.Euler(sw - 4f, 0, 0);
        thighR.localRotation = Quaternion.Euler(-sw - 4f, 0, 0);
        kneeL.localRotation = Quaternion.Euler(Mathf.Max(0f, -Mathf.Cos(walkPhase)) * 55f * amt + 6f, 0, 0);
        kneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Cos(walkPhase)) * 55f * amt + 6f, 0, 0);
        hips.localPosition = new Vector3(0, 0.95f - Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f * amt - 0.03f, 0);
        flinch = Mathf.MoveTowards(flinch, 0f, dt * 4f);
        float lean = 6f + amt * 8f;
        spine.localRotation = Quaternion.Euler(lean - flinch * 10f, Mathf.Sin(walkPhase * 0.5f) * 4f * amt, 0);
        headT.localRotation = Quaternion.Euler(-lean * 0.6f, Mathf.Sin(Time.time * 0.9f + walkPhase * 0.1f) * 6f, 0);
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
        hp -= head ? d : d * armor;
        flinch = 1f;
        if (type == EType.Boss || type == EType.Brute)
        {
            flashT = 0.05f;
            mpb.SetColor("_Color", new Color(1.6f, 1.4f, 1.3f));
            foreach (var r in rends) if (r != null) r.SetPropertyBlock(mpb);
        }
        Fx.Blood(point, dir, head ? 1.2f : 0.8f);
        forcePath = 0f;
        if (!los) { seenT = Time.time; lastSeenPos = Player.I != null ? Player.I.transform.position : lastSeenPos; repathT = 0f; }
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
        SFX.Play3D("edie", transform.position, 0.9f, Random.Range(0.85f, 1.1f));
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        deathAxis = Vector3.Cross(Vector3.up, dir.normalized);
        if (type == EType.Boss)
            for (int i = 0; i < 3; i++) Fx.Explosion(Center + Random.insideUnitSphere * 1.5f, 3f);
    }

    void DeathAnim(float dt)
    {
        deathT += dt;
        float k = Mathf.Clamp01(deathT / 0.35f);
        kneeL.localRotation = Quaternion.Slerp(kneeL.localRotation, Quaternion.Euler(80, 0, 0), k);
        kneeR.localRotation = Quaternion.Slerp(kneeR.localRotation, Quaternion.Euler(60, 0, 0), k);
        spine.localRotation = Quaternion.Slerp(spine.localRotation, Quaternion.Euler(25, 10, 0), k);
        headT.localRotation = Quaternion.Slerp(headT.localRotation, Quaternion.Euler(30, 20, 0), k);
        if (deathT > 0.15f && deathT < 0.65f) transform.RotateAround(transform.position, deathAxis, dt / 0.5f * 88f);
        if (deathT > 0.65f && deathT - dt <= 0.65f) Fx.BloodPool(Center, 1.2f * scale);
        if (deathT > 6f) transform.position += Vector3.down * dt * 0.6f;
        if (deathT > 8f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }
}
