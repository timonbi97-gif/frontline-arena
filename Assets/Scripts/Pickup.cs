using System.Collections.Generic;
using UnityEngine;

public enum PType { Health, Armor, Ammo, Weapon }

public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();

    public PType type;
    public int weapon;
    public float life = -1f;
    public System.Action onTaken;

    Transform vis;
    Material beamMat;
    float t, baseY;

    public static Pickup Create(PType type, Vector3 pos, int weapon = 0, float life = -1f)
    {
        var g = new GameObject("Pickup_" + type);
        g.transform.position = pos;
        var p = g.AddComponent<Pickup>();
        p.type = type; p.weapon = weapon; p.life = life;
        p.Build();
        All.Add(p);
        return p;
    }

    void Build()
    {
        vis = new GameObject("Vis").transform;
        vis.SetParent(transform, false);
        baseY = 0f;
        t = Random.value * 10f;
        switch (type)
        {
            case PType.Health:
                GA.Prim(PrimitiveType.Cube, vis, Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), GA.Lit(new Color(0.95f, 0.95f, 0.95f), 0.4f));
                GA.Prim(PrimitiveType.Cube, vis, Vector3.zero, new Vector3(0.62f, 0.4f, 0.12f), GA.Emissive(new Color(1f, 0.15f, 0.15f), 2f), false, 0, false);
                GA.Prim(PrimitiveType.Cube, vis, Vector3.zero, new Vector3(0.12f, 0.4f, 0.62f), GA.Emissive(new Color(1f, 0.15f, 0.15f), 2f), false, 0, false);
                GA.Prim(PrimitiveType.Cube, vis, Vector3.zero, new Vector3(0.4f, 0.62f, 0.12f), GA.Emissive(new Color(1f, 0.15f, 0.15f), 2f), false, 0, false);
                break;
            case PType.Armor:
                GA.Prim(PrimitiveType.Cube, vis, Vector3.zero, new Vector3(0.6f, 0.7f, 0.2f), GA.Lit(GA.C(0x2a5caa), 0.6f));
                GA.Prim(PrimitiveType.Cube, vis, new Vector3(0, 0.05f, 0), new Vector3(0.3f, 0.4f, 0.24f), GA.Emissive(new Color(0.3f, 0.7f, 1f), 2f), false, 0, false);
                break;
            case PType.Ammo:
                GA.Prim(PrimitiveType.Cube, vis, Vector3.zero, new Vector3(0.7f, 0.38f, 0.42f), GA.Lit(GA.C(0x4d5a34), 0.3f));
                GA.Prim(PrimitiveType.Cube, vis, Vector3.zero, new Vector3(0.72f, 0.1f, 0.44f), GA.Emissive(new Color(1f, 0.8f, 0.2f), 2f), false, 0, false);
                break;
            default:
                var d = Weapons.Defs[weapon];
                GA.Prim(PrimitiveType.Cube, vis, Vector3.zero, new Vector3(0.14f, 0.18f, 1.0f), GA.Lit(GA.C(0x2b2d31), 0.5f));
                GA.Prim(PrimitiveType.Cube, vis, new Vector3(0, -0.15f, -0.15f), new Vector3(0.1f, 0.25f, 0.12f), GA.Lit(GA.C(0x18191b), 0.5f));
                GA.Prim(PrimitiveType.Cube, vis, new Vector3(0, 0.1f, 0.1f), new Vector3(0.16f, 0.04f, 0.5f), GA.Emissive(d.glow, 3f), false, 0, false);
                beamMat = GA.Fx(new Color(d.glow.r, d.glow.g, d.glow.b, 0.18f));
                GA.Prim(PrimitiveType.Cylinder, transform, new Vector3(0, 12f, 0), new Vector3(0.7f, 12f, 0.7f), beamMat, false, 0, false);
                var l = gameObject.AddComponent<Light>();
                l.type = LightType.Point; l.color = d.glow; l.range = 7f; l.intensity = 2f; l.shadows = LightShadows.None;
                break;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        t += dt;
        vis.localPosition = new Vector3(0, baseY + Mathf.Sin(t * 2.5f) * 0.15f, 0);
        vis.localRotation = Quaternion.Euler(0, t * 90f, 0);
        if (life > 0f)
        {
            life -= dt;
            if (life < 4f) vis.gameObject.SetActive(Mathf.Repeat(life, 0.3f) > 0.12f);
            if (life <= 0f) { Destroy(gameObject); return; }
        }
        var p = Player.I;
        if (p == null || p.dead) return;
        Vector3 d = p.transform.position + Vector3.up * 0.9f - transform.position;
        if (d.sqrMagnitude < 2.2f * 2.2f) TryTake(p);
    }

    void TryTake(Player p)
    {
        bool ok = false;
        string msg = "";
        Color col = Color.white;
        switch (type)
        {
            case PType.Health:
                if (p.hp < p.maxHp) { p.hp = Mathf.Min(p.maxHp, p.hp + 35f); ok = true; msg = "+35 Gesundheit"; col = new Color(1f, 0.4f, 0.4f); }
                break;
            case PType.Armor:
                if (p.armor < p.maxArmor) { p.armor = Mathf.Min(p.maxArmor, p.armor + 50f); ok = true; msg = "+50 Rüstung"; col = new Color(0.4f, 0.7f, 1f); }
                break;
            case PType.Ammo:
                if (p.weapons.AddAmmo()) { ok = true; msg = "+ Munition"; col = new Color(1f, 0.85f, 0.3f); }
                break;
            default:
                bool isNew = p.weapons.Give(weapon);
                ok = true;
                var d = Weapons.Defs[weapon];
                msg = isNew ? d.name.ToUpper() + " GEFUNDEN!" : d.name + " Munition";
                col = d.glow;
                if (isNew) GameManager.I.Announce(d.name.ToUpper() + " FREIGESCHALTET", d.glow);
                break;
        }
        if (!ok) return;
        SFX.Play2D("pickup", 0.7f);
        GameManager.I.Feed(msg, col);
        onTaken?.Invoke();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (beamMat != null) Destroy(beamMat);
    }
}
