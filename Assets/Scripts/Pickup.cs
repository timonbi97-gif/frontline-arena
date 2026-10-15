using System.Collections.Generic;
using UnityEngine;

public enum PType { Health, Armor, Ammo, Weapon }

// Supplies lying on the ground: medkit, armor vest, ammo cans, weapons.
public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();

    public PType type;
    public int weapon;
    public float life = -1f;
    public System.Action onTaken;

    Transform vis;
    float born;

    public static Pickup Create(PType type, Vector3 pos, int weapon = 0, float life = -1f)
    {
        if (Physics.Raycast(pos + Vector3.up * 1.2f, Vector3.down, out var hit, 5f, GA.MaskWorld, QueryTriggerInteraction.Ignore))
            pos.y = hit.point.y;
        var g = new GameObject("Pickup_" + type);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        var p = g.AddComponent<Pickup>();
        p.type = type; p.weapon = weapon; p.life = life; p.born = Time.time;
        p.Build();
        All.Add(p);
        return p;
    }

    void Build()
    {
        vis = new GameObject("Vis").transform;
        vis.SetParent(transform, false);
        var olive = GA.Lit(GA.C(0x3f4630), 0.3f);
        var dark = GA.Lit(GA.C(0x161617), 0.45f);
        switch (type)
        {
            case PType.Health:
                {
                    var white = GA.Lit(new Color(0.85f, 0.85f, 0.82f), 0.35f);
                    var red = GA.Emissive(new Color(0.8f, 0.05f, 0.05f), 0.6f);
                    GA.Prim(PrimitiveType.Cube, vis, new Vector3(0, 0.1f, 0), new Vector3(0.46f, 0.2f, 0.32f), white);
                    GA.Prim(PrimitiveType.Cube, vis, new Vector3(0, 0.201f, 0), new Vector3(0.2f, 0.004f, 0.06f), red, false, 0, false);
                    GA.Prim(PrimitiveType.Cube, vis, new Vector3(0, 0.201f, 0), new Vector3(0.06f, 0.004f, 0.2f), red, false, 0, false);
                    GA.Prim(PrimitiveType.Cube, vis, new Vector3(0, 0.23f, 0), new Vector3(0.14f, 0.03f, 0.03f), dark);
                    break;
                }
            case PType.Armor:
                {
                    var vest = GA.Cloth(0, new Color(0.8f, 0.85f, 0.75f));
                    GA.Prim(PrimitiveType.Cube, vis, new Vector3(0, 0.06f, 0), new Vector3(0.5f, 0.12f, 0.6f), vest);
                    GA.Prim(PrimitiveType.Cube, vis, new Vector3(0, 0.13f, 0.05f), new Vector3(0.36f, 0.04f, 0.4f), GA.Lit(GA.C(0x2e3324), 0.2f));
                    for (int i = -1; i <= 1; i++)
                        GA.Prim(PrimitiveType.Cube, vis, new Vector3(i * 0.12f, 0.16f, -0.18f), new Vector3(0.09f, 0.04f, 0.12f), olive);
                    break;
                }
            case PType.Ammo:
                {
                    var stencil = GA.Lit(new Color(0.75f, 0.65f, 0.2f), 0.2f);
                    for (int i = 0; i < 2; i++)
                    {
                        float x = i == 0 ? -0.12f : 0.12f;
                        GA.Prim(PrimitiveType.Cube, vis, new Vector3(x, 0.1f, 0), new Vector3(0.18f, 0.2f, 0.3f), olive);
                        GA.Prim(PrimitiveType.Cube, vis, new Vector3(x, 0.205f, 0), new Vector3(0.19f, 0.012f, 0.31f), olive);
                        GA.Prim(PrimitiveType.Cube, vis, new Vector3(x, 0.225f, 0), new Vector3(0.03f, 0.02f, 0.12f), dark);
                        GA.Prim(PrimitiveType.Cube, vis, new Vector3(x, 0.12f, 0.151f), new Vector3(0.12f, 0.03f, 0.002f), stencil, false, 0, false);
                    }
                    break;
                }
            default:
                {
                    var gp = GunBuilder.Build(Arsenal.All[weapon], vis, 0, false);
                    gp.root.localPosition = new Vector3(0, 0.035f, 0);
                    gp.root.localRotation = Quaternion.Euler(0, 0, 90);
                    break;
                }
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (life > 0f)
        {
            life -= dt;
            if (life < 4f) vis.gameObject.SetActive(Mathf.Repeat(life, 0.4f) > 0.12f);
            if (life <= 0f) { Destroy(gameObject); return; }
        }
        var p = Player.I;
        if (p == null || p.dead) return;
        Vector3 d = p.transform.position - transform.position;
        bool near = new Vector2(d.x, d.z).sqrMagnitude < (type == PType.Weapon ? 2.2f * 2.2f : 1.6f * 1.6f) && Mathf.Abs(d.y) < 1.8f;
        if (!near) return;
        if (type == PType.Weapon)
        {
            GameManager.I.ShowPrompt(this);
            if (Input.GetKeyDown(KeyCode.E) && GameManager.I.PromptTarget == this && Time.time - born > 0.4f) TryTake(p);
        }
        else TryTake(p);
    }

    void TryTake(Player p)
    {
        bool ok = false;
        string msg = "";
        Color col = Color.white;
        switch (type)
        {
            case PType.Health:
                if (p.hp < p.maxHp) { p.hp = Mathf.Min(p.maxHp, p.hp + 50f); ok = true; msg = "Verbandskasten  +50"; col = new Color(1f, 0.4f, 0.4f); }
                break;
            case PType.Armor:
                if (p.armor < p.maxArmor) { p.armor = Mathf.Min(p.maxArmor, p.armor + 50f); ok = true; msg = "Schutzweste  +50"; col = new Color(0.4f, 0.7f, 1f); }
                break;
            case PType.Ammo:
                if (p.weapons.AddAmmo()) { ok = true; msg = "Munition aufgefüllt"; col = new Color(1f, 0.85f, 0.3f); }
                break;
            default:
                var d = Arsenal.All[weapon];
                bool had = false;
                foreach (var s in p.weapons.slots) if (s != null && s.id == weapon) had = true;
                int dropped = p.weapons.GiveWeapon(weapon);
                ok = true;
                msg = had ? d.name + " Munition" : d.name.ToUpper() + " AUFGENOMMEN";
                col = new Color(1f, 0.85f, 0.5f);
                if (dropped >= 0) GameManager.I.DropWeapon(dropped, p.transform.position + p.transform.forward * 0.8f);
                break;
        }
        if (!ok) return;
        SFX.Play2D("pickup", 0.5f);
        SFX.Play2D("reload", 0.4f, 1.3f);
        GameManager.I.Feed(msg, col);
        onTaken?.Invoke();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        All.Remove(this);
    }
}
