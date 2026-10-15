using UnityEngine;

public class WeaponDef
{
    public string name, sfx;
    public float dmg, rate, reload, spread, recoil;
    public float range = 300f, zoomFov = 55f, headMul = 2f;
    public bool auto, rocket;
    public int mag, reserveMax, pellets = 1;
    public Color tracer = new Color(1f, 0.9f, 0.55f, 0.8f);
    public Color glow;
}

public class Weapons : MonoBehaviour
{
    public static readonly WeaponDef[] Defs =
    {
        new WeaponDef { name = "Pistole", dmg = 26, rate = 4.5f, mag = 12, reserveMax = -1, reload = 1.1f, spread = 0.008f, recoil = 1.4f, sfx = "pistol", glow = Color.white },
        new WeaponDef { name = "Sturmgewehr", dmg = 19, rate = 10.5f, auto = true, mag = 30, reserveMax = 240, reload = 1.8f, spread = 0.018f, recoil = 0.9f, zoomFov = 50f, sfx = "rifle", glow = Color.white },
        new WeaponDef { name = "Schrotflinte", dmg = 14, rate = 1.25f, mag = 7, reserveMax = 42, reload = 2.2f, spread = 0.07f, pellets = 9, recoil = 5f, range = 70f, headMul = 1.5f, sfx = "shotgun", glow = new Color(1f, 0.55f, 0.2f) },
        new WeaponDef { name = "Scharfschützengewehr", dmg = 120, rate = 0.9f, mag = 5, reserveMax = 30, reload = 2.4f, spread = 0.002f, recoil = 6f, zoomFov = 18f, headMul = 3f, sfx = "sniper", glow = new Color(0.3f, 0.9f, 1f), tracer = new Color(0.6f, 0.95f, 1f, 0.9f) },
        new WeaponDef { name = "Raketenwerfer", dmg = 170, rate = 0.85f, mag = 3, reserveMax = 15, reload = 2.6f, spread = 0.004f, recoil = 6f, rocket = true, sfx = "rocket", glow = new Color(1f, 0.25f, 0.25f) },
    };

    public readonly bool[] owned = new bool[5];
    public readonly int[] mag = new int[5], reserve = new int[5];
    public int cur, last;
    public bool reloading, Zoomed;
    public float bloom, hitMarkerT;
    public bool hitMarkerKill;
    public int shots, hits;

    Player p;
    Transform holder;
    readonly Transform[] models = new Transform[5];
    readonly Transform[] muzzles = new Transform[5];
    float nextFire, reloadT, kick, switchT, flashT;
    GameObject flashObj;
    Light flashLight;
    Vector2 sway;

    static readonly Vector3 HolderPos = new Vector3(0.22f, -0.21f, 0.36f);
    static readonly Vector3 AdsPos = new Vector3(0f, -0.115f, 0.3f);

    public WeaponDef Cur => Defs[cur];
    public float ReloadProgress => reloading ? 1f - reloadT / Cur.reload : 0f;
    public bool HasReserve(int i) => Defs[i].reserveMax < 0 || reserve[i] > 0;

    public void Init(Player pl)
    {
        p = pl;
        holder = new GameObject("ViewModel").transform;
        holder.SetParent(p.cam.transform, false);
        holder.localPosition = HolderPos;
        for (int i = 0; i < 5; i++)
        {
            models[i] = BuildModel(i, out muzzles[i]);
            models[i].gameObject.SetActive(false);
        }
        flashObj = GA.Prim(PrimitiveType.Sphere, holder, Vector3.zero, Vector3.one * 0.12f, GA.Emissive(new Color(1f, 0.8f, 0.4f), 6f), false, 0, false);
        flashObj.SetActive(false);
        var lgo = new GameObject("MuzzleLight");
        lgo.transform.SetParent(holder, false);
        flashLight = lgo.AddComponent<Light>();
        flashLight.type = LightType.Point; flashLight.range = 9f; flashLight.intensity = 3f;
        flashLight.color = new Color(1f, 0.75f, 0.4f); flashLight.shadows = LightShadows.None; flashLight.enabled = false;

        owned[0] = owned[1] = true;
        for (int i = 0; i < 5; i++) mag[i] = Defs[i].mag;
        reserve[1] = 120;
        cur = 1; last = 0;
        models[cur].gameObject.SetActive(true);
    }

    // ---------------------------------------------------------------- models

    Transform BuildModel(int i, out Transform muzzle)
    {
        var root = new GameObject("W" + i).transform;
        root.SetParent(holder, false);
        Color metal = GA.C(0x2b2d31), dark = GA.C(0x18191b), wood = GA.C(0x6b4426), olive = GA.C(0x4d5a34);
        switch (i)
        {
            case 0:
                Part(root, PrimitiveType.Cube, new Vector3(0, 0.03f, 0.05f), new Vector3(0.07f, 0.08f, 0.26f), metal);
                Part(root, PrimitiveType.Cube, new Vector3(0, -0.06f, -0.04f), new Vector3(0.06f, 0.15f, 0.07f), dark, Quaternion.Euler(15, 0, 0));
                Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.035f, 0.19f), new Vector3(0.03f, 0.03f, 0.03f), dark, Quaternion.Euler(90, 0, 0));
                muzzle = Pivot(root, new Vector3(0, 0.035f, 0.23f));
                break;
            case 1:
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, 0.1f), new Vector3(0.08f, 0.1f, 0.55f), metal);
                Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.02f, 0.5f), new Vector3(0.035f, 0.16f, 0.035f), dark, Quaternion.Euler(90, 0, 0));
                Part(root, PrimitiveType.Cube, new Vector3(0, -0.12f, 0.14f), new Vector3(0.06f, 0.18f, 0.08f), dark, Quaternion.Euler(12, 0, 0));
                Part(root, PrimitiveType.Cube, new Vector3(0, -0.02f, -0.26f), new Vector3(0.07f, 0.11f, 0.22f), dark);
                Part(root, PrimitiveType.Cube, new Vector3(0, 0.075f, 0.05f), new Vector3(0.035f, 0.04f, 0.14f), dark);
                Part(root, PrimitiveType.Cube, new Vector3(0, -0.1f, -0.06f), new Vector3(0.05f, 0.12f, 0.06f), dark, Quaternion.Euler(15, 0, 0));
                muzzle = Pivot(root, new Vector3(0, 0.02f, 0.68f));
                break;
            case 2:
                Part(root, PrimitiveType.Cylinder, new Vector3(-0.024f, 0.01f, 0.3f), new Vector3(0.045f, 0.3f, 0.045f), metal, Quaternion.Euler(90, 0, 0));
                Part(root, PrimitiveType.Cylinder, new Vector3(0.024f, 0.01f, 0.3f), new Vector3(0.045f, 0.3f, 0.045f), metal, Quaternion.Euler(90, 0, 0));
                Part(root, PrimitiveType.Cube, new Vector3(0, -0.035f, -0.14f), new Vector3(0.08f, 0.12f, 0.36f), wood);
                Part(root, PrimitiveType.Cube, new Vector3(0, -0.04f, 0.24f), new Vector3(0.08f, 0.06f, 0.18f), wood);
                muzzle = Pivot(root, new Vector3(0, 0.01f, 0.62f));
                break;
            case 3:
                Part(root, PrimitiveType.Cube, new Vector3(0, 0, 0.05f), new Vector3(0.07f, 0.09f, 0.5f), olive);
                Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.015f, 0.6f), new Vector3(0.03f, 0.32f, 0.03f), dark, Quaternion.Euler(90, 0, 0));
                Part(root, PrimitiveType.Cylinder, new Vector3(0, 0.095f, 0.05f), new Vector3(0.065f, 0.14f, 0.065f), dark, Quaternion.Euler(90, 0, 0));
                Part(root, PrimitiveType.Cube, new Vector3(0, -0.02f, -0.3f), new Vector3(0.07f, 0.12f, 0.3f), olive);
                Part(root, PrimitiveType.Cube, new Vector3(0, -0.1f, 0.05f), new Vector3(0.05f, 0.1f, 0.07f), dark);
                muzzle = Pivot(root, new Vector3(0, 0.015f, 0.93f));
                break;
            default:
                Part(root, PrimitiveType.Cylinder, new Vector3(0.02f, 0.04f, 0.12f), new Vector3(0.17f, 0.45f, 0.17f), olive, Quaternion.Euler(90, 0, 0));
                Part(root, PrimitiveType.Cylinder, new Vector3(0.02f, 0.04f, 0.56f), new Vector3(0.19f, 0.03f, 0.19f), dark, Quaternion.Euler(90, 0, 0));
                Part(root, PrimitiveType.Cube, new Vector3(0.02f, -0.1f, 0.0f), new Vector3(0.05f, 0.14f, 0.07f), dark);
                Part(root, PrimitiveType.Cube, new Vector3(-0.07f, 0.11f, 0.1f), new Vector3(0.04f, 0.07f, 0.12f), dark);
                muzzle = Pivot(root, new Vector3(0.02f, 0.04f, 0.6f));
                break;
        }
        return root;
    }

    static void Part(Transform parent, PrimitiveType t, Vector3 pos, Vector3 scale, Color c, Quaternion? rot = null)
    {
        var g = GA.Prim(t, parent, pos, scale, GA.Lit(c, 0.45f), false, 0, false);
        if (rot.HasValue) g.transform.localRotation = rot.Value;
    }

    static Transform Pivot(Transform parent, Vector3 pos)
    {
        var t = new GameObject("Muzzle").transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    // ---------------------------------------------------------------- logic

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || p.dead) return;
        bool active = GameManager.I.MouseActive;

        for (int i = 0; i < 5; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
        float wheel = Input.mouseScrollDelta.y;
        if (active && Mathf.Abs(wheel) > 0.01f) Cycle(wheel > 0 ? -1 : 1);
        if (Input.GetKeyDown(KeyCode.Q)) Select(last);
        if (Input.GetKeyDown(KeyCode.R)) StartReload();

        if (reloading)
        {
            reloadT -= dt;
            if (reloadT <= 0f) FinishReload();
        }
        switchT = Mathf.MoveTowards(switchT, 0f, dt * 4f);
        Zoomed = active && Input.GetMouseButton(1) && !reloading && switchT < 0.3f;
        p.targetFov = Zoomed ? Cur.zoomFov : Player.BaseFov;
        bloom = Mathf.MoveTowards(bloom, 0f, dt * 0.12f);
        hitMarkerT -= dt;

        bool trigger = active && (Cur.auto ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0));
        if (trigger && Time.time >= nextFire && switchT <= 0.1f) Fire();

        // viewmodel animation
        kick = Mathf.Lerp(kick, 0f, dt * 14f);
        if (active)
        {
            sway = Vector2.Lerp(sway, new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 0.006f, dt * 10f);
        }
        Vector3 pos = Zoomed ? AdsPos : HolderPos;
        float bobAmt = Zoomed ? 0.15f : 1f;
        pos += new Vector3(Mathf.Cos(Time.time * 5f) * 0.004f, p.BobOffset * 0.012f * bobAmt, 0);
        pos += new Vector3(-sway.x, -sway.y, -0.07f * kick);
        float rl = reloading ? Mathf.Sin(ReloadProgress * Mathf.PI) : 0f;
        pos.y -= switchT * 0.35f + rl * 0.15f;
        Quaternion rot = Quaternion.Euler(-kick * 7f + rl * 25f, 0, rl * 20f);
        if (p.Sprinting && !Zoomed)
        {
            pos += new Vector3(-0.05f, -0.06f, -0.04f);
            rot *= Quaternion.Euler(12f, -28f, 8f);
        }
        holder.localPosition = Vector3.Lerp(holder.localPosition, pos, dt * 18f);
        holder.localRotation = Quaternion.Slerp(holder.localRotation, rot, dt * 14f);

        flashT -= dt;
        bool fl = flashT > 0f;
        if (flashObj.activeSelf != fl) flashObj.SetActive(fl);
        flashLight.enabled = fl;

        bool scoped = cur == 3 && Zoomed && p.cam.fieldOfView < 35f;
        if (models[cur].gameObject.activeSelf == scoped) models[cur].gameObject.SetActive(!scoped);
    }

    public float CurrentSpread()
    {
        var d = Cur;
        float s = d.spread + bloom;
        if (!p.Grounded) s += 0.03f;
        else if (p.Moving) s += d.spread * 1.2f + 0.004f;
        if (p.Sprinting) s += 0.02f;
        if (cur == 3 && !Zoomed) s += 0.05f;
        if (Zoomed) s *= cur == 3 ? 0.05f : 0.45f;
        return s;
    }

    void Fire()
    {
        var d = Cur;
        if (reloading) return;
        if (mag[cur] <= 0)
        {
            SFX.Play2D("empty", 0.6f);
            nextFire = Time.time + 0.3f;
            StartReload();
            return;
        }
        mag[cur]--;
        nextFire = Time.time + 1f / d.rate;
        shots++;
        SFX.Play2D(d.sfx, 0.7f, Random.Range(0.94f, 1.06f));
        kick = 1f;
        flashT = 0.05f;
        flashObj.transform.position = muzzles[cur].position;
        flashLight.transform.position = muzzles[cur].position;
        p.pitch -= d.recoil * (Zoomed ? 0.25f : 0.4f);
        p.recoil += d.recoil * 0.6f;
        p.AddShake(d.recoil * 0.015f);

        Transform cam = p.cam.transform;
        Vector3 muzzle = muzzles[cur].position;
        float spread = CurrentSpread();
        bool anyHit = false, kill = false, head = false;

        if (d.rocket)
        {
            Vector3 aim = cam.position + cam.forward * 300f;
            if (Physics.Raycast(cam.position, cam.forward, out var ah, 300f, GA.MaskPlayerShot, QueryTriggerInteraction.Collide))
                aim = ah.point;
            Vector3 start = cam.position + cam.forward * 0.5f + cam.right * 0.12f - cam.up * 0.08f;
            Projectile.Spawn(start, (aim - start).normalized, 45f, d.dmg, true, 6f, new Color(1f, 0.5f, 0.1f), 0.25f);
        }
        else
        {
            for (int i = 0; i < d.pellets; i++)
            {
                Vector2 rc = Random.insideUnitCircle * spread;
                Vector3 dir = (cam.forward + cam.right * rc.x + cam.up * rc.y).normalized;
                Vector3 end = cam.position + dir * d.range;
                if (Physics.Raycast(cam.position, dir, out var hit, d.range, GA.MaskPlayerShot, QueryTriggerInteraction.Collide))
                {
                    end = hit.point;
                    var e = hit.collider.GetComponentInParent<Enemy>();
                    if (e != null)
                    {
                        if (!e.dead)
                        {
                            bool hs = hit.collider.name == "Head";
                            float dmg = d.dmg * (hs ? d.headMul : 1f);
                            if (d.pellets > 1) dmg *= Mathf.Clamp(1.3f - hit.distance / 35f, 0.3f, 1f);
                            kill |= e.Damage(dmg, hs, hit.point, dir);
                            anyHit = true;
                            head |= hs;
                            Fx.Debris(hit.point, e.partColor, 3, 4f, 0.07f, 0.45f);
                        }
                    }
                    else Fx.Impact(hit.point, hit.normal, hit.collider);
                }
                if (i < 3) Fx.Tracer(muzzle, end, d.tracer);
            }
        }
        if (anyHit) RegisterHit(kill, head);
        bloom = Mathf.Min(bloom + d.spread * 0.5f, d.spread * 3f);
        if (mag[cur] == 0 && HasReserve(cur)) StartReload();
    }

    public void RegisterHit(bool kill, bool head)
    {
        hits++;
        hitMarkerT = 0.25f;
        hitMarkerKill = kill;
        SFX.Play2D(head ? "headshot" : "hit", 0.55f);
    }

    public void StartReload()
    {
        if (reloading || mag[cur] >= Cur.mag || !HasReserve(cur)) return;
        reloading = true;
        reloadT = Cur.reload;
        SFX.Play2D("reload", 0.6f);
    }

    void FinishReload()
    {
        reloading = false;
        int need = Cur.mag - mag[cur];
        if (Cur.reserveMax < 0) mag[cur] = Cur.mag;
        else
        {
            int take = Mathf.Min(need, reserve[cur]);
            mag[cur] += take;
            reserve[cur] -= take;
        }
    }

    public void Select(int i)
    {
        if (i < 0 || i > 4 || !owned[i] || i == cur) return;
        last = cur;
        models[cur].gameObject.SetActive(false);
        cur = i;
        models[cur].gameObject.SetActive(true);
        reloading = false;
        switchT = 1f;
        SFX.Play2D("swap", 0.6f);
    }

    void Cycle(int dir)
    {
        for (int k = 1; k < 5; k++)
        {
            int i = ((cur + dir * k) % 5 + 5) % 5;
            if (owned[i]) { Select(i); return; }
        }
    }

    // Returns true if the weapon was newly unlocked.
    public bool Give(int i)
    {
        bool isNew = !owned[i];
        owned[i] = true;
        var d = Defs[i];
        if (isNew) mag[i] = d.mag;
        if (d.reserveMax > 0) reserve[i] = Mathf.Min(d.reserveMax, reserve[i] + d.mag * 2);
        if (isNew) Select(i);
        return isNew;
    }

    public bool AddAmmo()
    {
        bool any = false;
        for (int i = 0; i < 5; i++)
        {
            var d = Defs[i];
            if (!owned[i] || d.reserveMax < 0 || reserve[i] >= d.reserveMax) continue;
            reserve[i] = Mathf.Min(d.reserveMax, reserve[i] + Mathf.CeilToInt(d.mag * 1.5f));
            any = true;
        }
        return any;
    }

    public void OnDeath()
    {
        holder.gameObject.SetActive(false);
    }
}
