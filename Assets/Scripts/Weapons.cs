using UnityEngine;

public class Gun
{
    public int id;
    public WeaponDef d;
    public int mag, reserve;
    public GunParts parts;
    public Vector3 magRest, adsPos, hipPos;
}

// Player inventory: slot 1 primary, slot 2 pistol, slot 3 launcher, plus grenades.
public class Weapons : MonoBehaviour
{
    public readonly Gun[] slots = new Gun[3];
    public int cur, last = 1, grenades = 3;
    public const int MaxGrenades = 5;
    public bool reloading, Zoomed;
    public float ads, bloom, hitMarkerT;
    public bool hitMarkerKill;
    public int shots, hits;

    Player p;
    Transform holder;
    float nextFire, reloadT, kick, switchT, flashT, throwT, cycleT, cycleDur = 1f;
    int burstLeft;
    GameObject flashObj;
    Light flashLight;
    Vector2 sway;

    public Gun Cur => slots[cur];
    public WeaponDef Def => slots[cur].d;
    public bool Scoped => Zoomed && ads > 0.8f && Def.Scoped;
    public float ReloadProgress => reloading ? 1f - reloadT / Def.reload : 0f;
    public bool HasReserve(Gun g) => g.d.reserve < 0 || g.reserve > 0;

    public void Init(Player pl, int primary, int secondary)
    {
        p = pl;
        holder = new GameObject("ViewModel").transform;
        holder.SetParent(p.cam.transform, false);
        var fm = GA.Fx(new Color(3f, 2.3f, 1.3f, 1f));
        fm.mainTexture = TexGen.Flash();
        flashObj = GA.Prim(PrimitiveType.Quad, holder, Vector3.zero, Vector3.one * 0.18f, fm, false, 0, false);
        flashObj.SetActive(false);
        var lgo = new GameObject("MuzzleLight");
        lgo.transform.SetParent(holder, false);
        flashLight = lgo.AddComponent<Light>();
        flashLight.type = LightType.Point; flashLight.range = 10f; flashLight.intensity = 3.5f;
        flashLight.color = new Color(1f, 0.72f, 0.4f); flashLight.shadows = LightShadows.None; flashLight.enabled = false;

        slots[1] = Make(secondary);
        slots[0] = Make(primary);
        cur = 0;
        slots[0].parts.root.gameObject.SetActive(true);
        holder.localPosition = slots[0].hipPos;
    }

    Gun Make(int id)
    {
        var d = Arsenal.All[id];
        var g = new Gun { id = id, d = d, mag = d.mag };
        g.reserve = d.reserve < 0 ? -1 : Mathf.Min(d.reserve, Mathf.Max(d.mag * 3, 4));
        g.parts = GunBuilder.Build(d, holder, 0, true);
        g.magRest = g.parts.mag != null ? g.parts.mag.localPosition : Vector3.zero;
        var s = g.parts.sight;
        g.adsPos = new Vector3(-s.x, -s.y, g.parts.eyeDist - s.z);
        g.hipPos = d.cat == WCat.Pistol ? new Vector3(0.14f, -0.15f, 0.3f)
                 : d.rocket ? new Vector3(0.13f, -0.12f, 0.12f)
                 : new Vector3(0.16f, -0.17f, 0.24f);
        BuildArms(g.parts.root, g.parts.grip, g.parts.fore, d.cat == WCat.Pistol);
        g.parts.root.gameObject.SetActive(false);
        return g;
    }

    void BuildArms(Transform root, Vector3 grip, Vector3 fore, bool pistol)
    {
        var sleeve = GA.Textured(TexGen.Camo(0), Color.white, 0.05f, 3f);
        var glove = GA.Lit(new Color(0.08f, 0.075f, 0.07f), 0.25f);
        Vector3 elbowR = grip + (pistol ? new Vector3(0.07f, -0.12f, -0.22f) : new Vector3(0.1f, -0.14f, -0.22f));
        Vector3 elbowL = fore + (pistol ? new Vector3(-0.11f, -0.12f, -0.2f) : new Vector3(-0.15f, -0.14f, -0.17f));
        GA.Segment(root, grip + new Vector3(0.008f, -0.025f, -0.025f), elbowR, 0.05f, sleeve, 0, false);
        GA.Segment(root, elbowR, elbowR + new Vector3(0.04f, -0.1f, -0.25f), 0.065f, sleeve, 0, false);
        GA.Segment(root, fore + new Vector3(-0.012f, -0.025f, -0.03f), elbowL, 0.05f, sleeve, 0, false);
        GA.Segment(root, elbowL, elbowL + new Vector3(-0.04f, -0.1f, -0.25f), 0.065f, sleeve, 0, false);
        GA.Prim(PrimitiveType.Cube, root, grip + new Vector3(0.01f, -0.005f, 0f), new Vector3(0.042f, 0.055f, 0.075f), glove, false, 0, false);
        GA.Prim(PrimitiveType.Cube, root, fore + new Vector3(-0.008f, -0.012f, 0f), new Vector3(0.045f, 0.045f, 0.085f), glove, false, 0, false);
    }

    // Puts a weapon into its slot. Returns the id of the weapon it replaced (or -1).
    public int GiveWeapon(int id)
    {
        var d = Arsenal.All[id];
        int s = d.Slot;
        if (slots[s] != null && slots[s].id == id)
        {
            if (d.reserve > 0) slots[s].reserve = Mathf.Min(d.reserve, slots[s].reserve + d.mag * 2);
            return -1;
        }
        int dropped = slots[s] != null ? slots[s].id : -1;
        if (slots[s] != null) Destroy(slots[s].parts.root.gameObject);
        slots[s] = Make(id);
        if (cur == s) { cur = -1; }
        Select(s);
        return dropped;
    }

    public bool AddAmmo()
    {
        bool any = false;
        foreach (var g in slots)
        {
            if (g == null || g.d.reserve < 0 || g.reserve >= g.d.reserve) continue;
            g.reserve = Mathf.Min(g.d.reserve, g.reserve + Mathf.Max(Mathf.CeilToInt(g.d.mag * 1.5f), 2));
            any = true;
        }
        if (grenades < MaxGrenades) { grenades++; any = true; }
        return any;
    }

    // ---------------------------------------------------------------- update

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || p.dead) return;
        bool active = GameManager.I.MouseActive;
        var g = Cur;
        var d = g.d;

        for (int i = 0; i < 3; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
        float wheel = Input.mouseScrollDelta.y;
        if (active && Mathf.Abs(wheel) > 0.01f) Cycle(wheel > 0 ? -1 : 1);
        if (Input.GetKeyDown(KeyCode.Q)) Select(last);
        if (Input.GetKeyDown(KeyCode.R)) StartReload();
        if (Input.GetKeyDown(KeyCode.G) && active) ThrowGrenade();
        g = Cur; d = g.d;

        if (reloading)
        {
            reloadT -= dt;
            if (reloadT <= 0f) FinishReload();
        }
        switchT = Mathf.MoveTowards(switchT, 0f, dt * 3.5f);
        throwT = Mathf.MoveTowards(throwT, 0f, dt * 2.5f);
        cycleT = Mathf.MoveTowards(cycleT, 0f, dt);
        Zoomed = active && Input.GetMouseButton(1) && !reloading && switchT < 0.3f && throwT <= 0f && !p.Sprinting;
        float adsSpeed = d.cat == WCat.Pistol ? 7f : d.cat == WCat.SMG ? 6f : d.cat == WCat.Sniper || d.cat == WCat.LMG ? 3.5f : 5f;
        ads = Mathf.MoveTowards(ads, Zoomed ? 1f : 0f, dt * adsSpeed);
        p.targetFov = !Zoomed ? Player.BaseFov : d.Scoped ? (ads > 0.8f ? d.zoomFov : 58f) : d.AdsFov;
        bloom = Mathf.MoveTowards(bloom, 0f, dt * 0.1f);
        hitMarkerT -= dt;

        bool ready = Time.time >= nextFire && switchT <= 0.1f && throwT <= 0f && cycleT <= 0f && !reloading;
        if (active && ready && burstLeft == 0)
        {
            bool trig = d.fire == FireMode.Auto ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
            if (trig)
            {
                if (d.fire == FireMode.Burst) burstLeft = 3;
                else Fire();
            }
        }
        if (burstLeft > 0 && Time.time >= nextFire && !reloading)
        {
            Fire();
            burstLeft--;
            if (burstLeft == 0) nextFire = Time.time + 0.28f;
            if (g.mag == 0) burstLeft = 0;
        }

        Animate(dt, g, d, active);
    }

    void Animate(float dt, Gun g, WeaponDef d, bool active)
    {
        kick = Mathf.Lerp(kick, 0f, dt * 13f);
        if (active)
            sway = Vector2.Lerp(sway, new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 0.004f * (1f - ads * 0.8f), dt * 10f);
        float a = Mathf.SmoothStep(0f, 1f, ads);
        Vector3 pos = Vector3.Lerp(g.hipPos, g.adsPos, a);
        float bobAmt = 1f - a * 0.9f;
        pos += new Vector3(Mathf.Cos(Time.time * 1.6f) * 0.002f, Mathf.Sin(Time.time * 2.1f) * 0.002f + p.BobOffset * 0.01f, 0) * bobAmt;
        pos += new Vector3(-sway.x, -sway.y, -0.045f * kick * (d.cat == WCat.Sniper ? 1.6f : 1f));
        float rl = reloading ? Mathf.Sin(ReloadProgress * Mathf.PI) : 0f;
        float cyc = cycleT > 0f ? Mathf.Sin((1f - cycleT / cycleDur) * Mathf.PI) : 0f;
        pos.y -= switchT * 0.3f + rl * 0.05f + throwT * 0.25f;
        pos.z -= cyc * 0.03f;
        Quaternion rot = Quaternion.Euler(-kick * (2f + 3f * (1f - a)) + rl * 16f + cyc * 6f, rl * -10f, rl * 26f + cyc * 14f);
        if (p.Sprinting && ads < 0.1f)
        {
            pos += new Vector3(-0.06f, -0.05f, -0.05f);
            rot *= Quaternion.Euler(10f, -32f, 12f);
        }
        holder.localPosition = Vector3.Lerp(holder.localPosition, pos, dt * 20f);
        holder.localRotation = Quaternion.Slerp(holder.localRotation, rot, dt * 14f);

        // magazine out / in
        if (g.parts.mag != null && d.magT != MagT.Tube)
        {
            float magOut = 0f;
            if (reloading)
            {
                float k = ReloadProgress;
                magOut = k < 0.15f ? 0f : k < 0.35f ? (k - 0.15f) / 0.2f : k < 0.6f ? 1f : k < 0.8f ? 1f - (k - 0.6f) / 0.2f : 0f;
            }
            Vector3 dir = d.magT == MagT.Top ? new Vector3(0, 0.12f, -0.05f)
                        : d.magT == MagT.Cylinder ? new Vector3(0.06f, -0.02f, 0)
                        : d.rocket ? new Vector3(0, 0.05f, 0.3f)
                        : new Vector3(0, -0.22f, -0.05f);
            g.parts.mag.localPosition = g.magRest + dir * magOut;
            bool show = magOut < 0.98f;
            if (d.rocket) show = g.mag > 0 && !(reloading && ReloadProgress < 0.6f);
            g.parts.mag.gameObject.SetActive(show);
        }

        flashT -= dt;
        bool fl = flashT > 0f;
        if (flashObj.activeSelf != fl) flashObj.SetActive(fl);
        flashLight.enabled = fl;
        if (fl) flashObj.transform.rotation = p.cam.transform.rotation * Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        bool hide = Scoped;
        if (g.parts.root.gameObject.activeSelf == hide) g.parts.root.gameObject.SetActive(!hide);
    }

    public float CurrentSpread()
    {
        var d = Def;
        float s = d.spread + bloom;
        if (!p.Grounded) s += 0.04f;
        else if (p.Moving) s += d.spread * 1.2f + 0.005f;
        if (p.Sprinting) s += 0.03f;
        if (p.Crouching) s *= 0.7f;
        if ((d.cat == WCat.Sniper || d.cat == WCat.DMR) && !Zoomed) s += 0.05f;
        float adsMul = d.cat == WCat.Shotgun ? 0.75f : d.Scoped ? 0.06f : 0.3f;
        return s * Mathf.Lerp(1f, adsMul, ads);
    }

    void Fire()
    {
        var g = Cur;
        var d = g.d;
        if (g.mag <= 0)
        {
            SFX.Play2D("empty", 0.6f);
            nextFire = Time.time + 0.3f;
            StartReload();
            return;
        }
        g.mag--;
        nextFire = Time.time + 1f / d.rate;
        shots++;
        SFX.Play2D(d.sfx, 0.75f, d.pitch * Random.Range(0.96f, 1.04f));
        kick = 1f;
        flashT = d.rocket || d.glauncher ? 0.08f : 0.04f;
        Vector3 mz = g.parts.muzzle.position;
        flashObj.transform.position = mz;
        flashObj.transform.localScale = Vector3.one * (d.pellets > 1 ? 0.3f : d.rocket ? 0.5f : d.cat == WCat.Pistol ? 0.14f : 0.2f) * (d.suppressor ? 0.3f : 1f);
        flashLight.transform.position = mz;
        float recoilMul = Mathf.Lerp(1f, 0.65f, ads) * (p.Crouching ? 0.75f : 1f);
        p.pitch -= d.recoil * 0.45f * recoilMul;
        p.yaw += Random.Range(-0.35f, 0.3f) * d.recoil * recoilMul;
        p.recoil += d.recoil * 0.45f * recoilMul;
        p.AddShake(d.recoil * 0.01f);

        Transform cam = p.cam.transform;
        float spread = CurrentSpread();
        bool anyHit = false, kill = false, head = false;

        if (d.rocket || d.glauncher)
        {
            Vector3 aim = cam.position + cam.forward * 300f;
            if (Physics.Raycast(cam.position, cam.forward, out var ah, 300f, GA.MaskPlayerShot, QueryTriggerInteraction.Collide))
                aim = ah.point;
            Vector3 start = cam.position + cam.forward * 0.6f + cam.right * 0.08f - cam.up * 0.05f;
            if (d.rocket)
            {
                Projectile.Rocket(start, (aim - start).normalized, 55f, d.dmg, 7f);
                Fx.Smoke(cam.position - cam.forward * 0.8f + cam.right * 0.2f, 2.5f, 1.5f, 0.5f, 0.8f);
            }
            else Projectile.LauncherGrenade(start, (cam.forward + cam.up * 0.06f).normalized, 42f, d.dmg);
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
                            if (d.pellets > 1) dmg *= Mathf.Clamp(1.3f - hit.distance / 30f, 0.25f, 1f);
                            kill |= e.Damage(dmg, hs, hit.point, dir);
                            anyHit = true;
                            head |= hs;
                        }
                    }
                    else Fx.Impact(hit.point, hit.normal, hit.collider);
                }
                if (i < 2 && (d.fire != FireMode.Auto || shots % 3 == 0)) Fx.Tracer(mz + cam.forward * 0.1f, end, new Color(1f, 0.85f, 0.55f, 0.5f));
            }
            if (d.action == ActionT.Normal && !d.dbl && d.magT != MagT.Cylinder)
                EjectShell(g);
        }
        if (Random.value < 0.35f || d.pellets > 1) Fx.Smoke(mz + cam.forward * 0.15f, 0.35f, 0.9f, 0.18f, 0.8f);
        if (anyHit) RegisterHit(kill, head);
        bloom = Mathf.Min(bloom + d.spread * 0.45f, d.spread * 3f);

        if (d.action != ActionT.Normal && g.mag > 0)
        {
            cycleDur = cycleT = d.action == ActionT.Pump ? 0.55f : 0.95f;
            Invoke(nameof(CycleSound), 0.15f);
        }
        if (g.mag == 0 && HasReserve(g)) StartReload();
    }

    void EjectShell(Gun g)
    {
        var cam = p.cam.transform;
        Fx.Shell(g.parts.eject.position, cam.right * Random.Range(1.5f, 2.6f) + cam.up * Random.Range(1f, 2f) + p.Velocity, g.d.ShellColor, g.d.ShellLen);
        if (Random.value < 0.4f) Invoke(nameof(ShellSound), 0.45f);
    }

    void CycleSound()
    {
        var g = Cur;
        SFX.Play2D(g.d.action == ActionT.Pump ? "pump" : "bolt", 0.7f);
        if (g.d.action != ActionT.Normal) EjectShell(g);
    }

    void ShellSound() => SFX.Play2D("shell", 0.5f, Random.Range(0.9f, 1.2f));

    void ThrowGrenade()
    {
        if (grenades <= 0 || throwT > 0f || reloading) return;
        grenades--;
        throwT = 1f;
        Transform cam = p.cam.transform;
        Vector3 target = cam.position + cam.forward * 22f;
        if (Physics.Raycast(cam.position, cam.forward, out var hit, 30f, GA.MaskWorld, QueryTriggerInteraction.Ignore))
            target = hit.point;
        Grenade.Throw(cam.position + cam.forward * 0.5f + cam.right * 0.2f, target + Vector3.up * 0.5f, 200f, true);
        SFX.Play2D("swap", 0.7f, 0.7f);
    }

    public void RegisterHit(bool kill, bool head)
    {
        hits++;
        hitMarkerT = 0.25f;
        hitMarkerKill = kill;
        SFX.Play2D(head ? "headshot" : "hit", 0.45f);
    }

    public void StartReload()
    {
        var g = Cur;
        if (reloading || g.mag >= g.d.mag || !HasReserve(g)) return;
        reloading = true;
        burstLeft = 0;
        reloadT = g.d.reload;
        SFX.Play2D("reload", 0.6f, Mathf.Clamp(2.3f / g.d.reload, 0.6f, 1.4f));
    }

    void FinishReload()
    {
        reloading = false;
        var g = Cur;
        int need = g.d.mag - g.mag;
        if (g.d.reserve < 0) g.mag = g.d.mag;
        else
        {
            int take = Mathf.Min(need, g.reserve);
            g.mag += take;
            g.reserve -= take;
        }
    }

    public void Select(int i)
    {
        if (i < 0 || i > 2 || slots[i] == null || i == cur) return;
        if (cur >= 0 && slots[cur] != null)
        {
            last = cur;
            var o = slots[cur];
            if (o.parts.mag != null) o.parts.mag.localPosition = o.magRest;
            o.parts.root.gameObject.SetActive(false);
        }
        cur = i;
        slots[cur].parts.root.gameObject.SetActive(true);
        reloading = false;
        burstLeft = 0;
        cycleT = 0f;
        switchT = 1f;
        ads = 0f;
        SFX.Play2D("swap", 0.6f);
    }

    void Cycle(int dir)
    {
        for (int k = 1; k < 3; k++)
        {
            int i = ((cur + dir * k) % 3 + 3) % 3;
            if (slots[i] != null) { Select(i); return; }
        }
    }

    public void OnDeath()
    {
        holder.gameObject.SetActive(false);
    }
}
