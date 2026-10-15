using System.Collections.Generic;
using UnityEngine;

public enum WCat { Pistol, SMG, Rifle, LMG, Shotgun, DMR, Sniper, Launcher }
public enum Sight { Iron, RedDot, Holo, Acog, Scope }
public enum FireMode { Semi, Auto, Burst }
public enum StockT { None, Fixed, Skeleton, Wood, Bullpup }
public enum MagT { Grip, Straight, Curved, Box, Tube, Top, Cylinder, None }
public enum ActionT { Normal, Pump, Bolt }
public enum Col { Black, Steel, Tan, Olive, Wood }

public class WeaponDef
{
    public string name;
    public WCat cat;
    public FireMode fire = FireMode.Semi;
    public Sight sight = Sight.Iron;
    public StockT stock = StockT.Fixed;
    public MagT magT = MagT.Straight;
    public ActionT action = ActionT.Normal;
    public float dmg, rate, reload, spread, recoil;
    public float range = 400f, headMul = 2.5f, zoomFov = 0f, pitch = 1f;
    public int mag, reserve, pellets = 1;
    public string sfx = "rifle";
    public bool rocket, glauncher, dbl, brake, suppressor;
    // model dimensions (m)
    public float rl = 0.3f, rh = 0.07f, hl = 0.22f, bl = 0.12f, bt = 0.018f;
    public Col body = Col.Black, furn = Col.Black;

    public int Slot => cat == WCat.Pistol ? 1 : cat == WCat.Launcher ? 2 : 0;
    public bool Scoped => sight == Sight.Acog || sight == Sight.Scope;
    public float AdsFov => zoomFov > 0f ? zoomFov : sight == Sight.Iron ? 60f : 55f;
    public float ShellLen => cat == WCat.Pistol ? 0.018f : cat == WCat.Shotgun ? 0.05f : cat == WCat.Sniper || cat == WCat.DMR ? 0.045f : 0.028f;
    public Color ShellColor => cat == WCat.Shotgun ? new Color(0.65f, 0.1f, 0.08f) : new Color(0.75f, 0.55f, 0.2f);

    public static readonly string[] CatNames = { "Pistole", "Maschinenpistole", "Sturmgewehr", "Maschinengewehr", "Schrotflinte", "Präzisionsgewehr", "Scharfschützengewehr", "Werfer" };
    public string CatName => CatNames[(int)cat];
    public string FireName => fire == FireMode.Auto ? "AUTO" : fire == FireMode.Burst ? "FEUERSTOSS" : action == ActionT.Bolt ? "REPETIER" : action == ActionT.Pump ? "PUMP" : "EINZEL";
}

public static class Arsenal
{
    public static readonly WeaponDef[] All =
    {
        // ---- pistols (slot 2, unlimited reserve)
        new WeaponDef { name = "P9", cat = WCat.Pistol, magT = MagT.Grip, dmg = 30, rate = 6, mag = 17, reserve = -1, reload = 1.4f, spread = 0.012f, recoil = 1.5f, sfx = "pistol", pitch = 1.05f, rl = 0.19f },
        new WeaponDef { name = "M1911", cat = WCat.Pistol, magT = MagT.Grip, dmg = 42, rate = 4.5f, mag = 8, reserve = -1, reload = 1.6f, spread = 0.011f, recoil = 2.2f, sfx = "pistol", pitch = 0.92f, rl = 0.21f, body = Col.Steel, furn = Col.Wood },
        new WeaponDef { name = "Desert Eagle", cat = WCat.Pistol, magT = MagT.Grip, dmg = 75, rate = 2.4f, mag = 7, reserve = -1, reload = 1.9f, spread = 0.014f, recoil = 4.5f, sfx = "pistol", pitch = 0.75f, rl = 0.26f, rh = 0.085f, body = Col.Steel },
        new WeaponDef { name = "Revolver .357", cat = WCat.Pistol, magT = MagT.Cylinder, dmg = 68, rate = 2.2f, mag = 6, reserve = -1, reload = 2.8f, spread = 0.012f, recoil = 4f, sfx = "pistol", pitch = 0.82f, rl = 0.2f, body = Col.Steel, furn = Col.Wood },
        new WeaponDef { name = "G18", cat = WCat.Pistol, fire = FireMode.Auto, magT = MagT.Grip, dmg = 22, rate = 17, mag = 33, reserve = -1, reload = 1.6f, spread = 0.02f, recoil = 1f, sfx = "pistol", pitch = 1.12f, rl = 0.19f },

        // ---- SMGs
        new WeaponDef { name = "MP5", cat = WCat.SMG, fire = FireMode.Auto, stock = StockT.Fixed, magT = MagT.Curved, dmg = 24, rate = 13, mag = 30, reserve = 240, reload = 2.1f, spread = 0.016f, recoil = 0.8f, sfx = "smg", rl = 0.28f, hl = 0.16f, bl = 0.04f },
        new WeaponDef { name = "UMP-45", cat = WCat.SMG, fire = FireMode.Auto, sight = Sight.RedDot, stock = StockT.Skeleton, dmg = 30, rate = 10, mag = 25, reserve = 240, reload = 2.2f, spread = 0.017f, recoil = 1f, sfx = "smg", pitch = 0.9f, rl = 0.3f, hl = 0.14f, bl = 0.05f },
        new WeaponDef { name = "Vector", cat = WCat.SMG, fire = FireMode.Auto, sight = Sight.Holo, stock = StockT.Skeleton, dmg = 22, rate = 18, mag = 30, reserve = 240, reload = 2f, spread = 0.018f, recoil = 0.7f, sfx = "smg", pitch = 1.1f, rl = 0.32f, rh = 0.08f, hl = 0.1f, bl = 0.03f },
        new WeaponDef { name = "P90", cat = WCat.SMG, fire = FireMode.Auto, sight = Sight.RedDot, stock = StockT.Bullpup, magT = MagT.Top, dmg = 22, rate = 15, mag = 50, reserve = 250, reload = 2.6f, spread = 0.016f, recoil = 0.75f, sfx = "smg", pitch = 1.15f, rl = 0.42f, rh = 0.08f, hl = 0.05f, bl = 0.05f },

        // ---- assault rifles
        new WeaponDef { name = "M4A1", cat = WCat.Rifle, fire = FireMode.Auto, sight = Sight.RedDot, stock = StockT.Skeleton, dmg = 30, rate = 12, mag = 30, reserve = 240, reload = 2.2f, spread = 0.012f, recoil = 1f, sfx = "rifle", rl = 0.3f, hl = 0.24f, bl = 0.14f, brake = true },
        new WeaponDef { name = "AK-47", cat = WCat.Rifle, fire = FireMode.Auto, stock = StockT.Wood, magT = MagT.Curved, dmg = 36, rate = 10, mag = 30, reserve = 240, reload = 2.5f, spread = 0.016f, recoil = 1.4f, sfx = "ak", rl = 0.3f, hl = 0.2f, bl = 0.17f, brake = true, body = Col.Steel, furn = Col.Wood },
        new WeaponDef { name = "G36C", cat = WCat.Rifle, fire = FireMode.Auto, sight = Sight.Holo, stock = StockT.Skeleton, dmg = 30, rate = 12.5f, mag = 30, reserve = 240, reload = 2.3f, spread = 0.013f, recoil = 0.95f, sfx = "rifle", pitch = 1.06f, rl = 0.3f, hl = 0.18f, bl = 0.06f },
        new WeaponDef { name = "SCAR-H", cat = WCat.Rifle, fire = FireMode.Auto, sight = Sight.Acog, zoomFov = 32, stock = StockT.Fixed, dmg = 42, rate = 9, mag = 20, reserve = 200, reload = 2.5f, spread = 0.012f, recoil = 1.6f, sfx = "rifle", pitch = 0.88f, rl = 0.32f, hl = 0.26f, bl = 0.1f, brake = true, body = Col.Tan, furn = Col.Tan },
        new WeaponDef { name = "AUG A3", cat = WCat.Rifle, fire = FireMode.Auto, sight = Sight.Acog, zoomFov = 40, stock = StockT.Bullpup, dmg = 30, rate = 11.5f, mag = 30, reserve = 240, reload = 2.6f, spread = 0.011f, recoil = 0.9f, sfx = "rifle", pitch = 1.02f, rl = 0.5f, hl = 0.1f, bl = 0.2f, body = Col.Olive, furn = Col.Olive },
        new WeaponDef { name = "FAMAS", cat = WCat.Rifle, fire = FireMode.Burst, sight = Sight.RedDot, stock = StockT.Bullpup, dmg = 30, rate = 16, mag = 25, reserve = 225, reload = 2.4f, spread = 0.011f, recoil = 0.8f, sfx = "rifle", pitch = 1.08f, rl = 0.5f, hl = 0.08f, bl = 0.16f },

        // ---- machine guns
        new WeaponDef { name = "M249", cat = WCat.LMG, fire = FireMode.Auto, sight = Sight.Holo, stock = StockT.Fixed, magT = MagT.Box, dmg = 30, rate = 13, mag = 100, reserve = 300, reload = 5f, spread = 0.022f, recoil = 1f, sfx = "lmg", pitch = 1.05f, rl = 0.4f, rh = 0.08f, hl = 0.22f, bl = 0.2f, brake = true },
        new WeaponDef { name = "PKM", cat = WCat.LMG, fire = FireMode.Auto, stock = StockT.Wood, magT = MagT.Box, dmg = 40, rate = 10, mag = 100, reserve = 300, reload = 5.5f, spread = 0.024f, recoil = 1.4f, sfx = "lmg", pitch = 0.9f, rl = 0.42f, rh = 0.08f, hl = 0.1f, bl = 0.3f, brake = true, body = Col.Steel, furn = Col.Wood },

        // ---- shotguns
        new WeaponDef { name = "M870", cat = WCat.Shotgun, action = ActionT.Pump, stock = StockT.Wood, magT = MagT.Tube, dmg = 15, pellets = 9, rate = 1.3f, mag = 6, reserve = 48, reload = 3f, spread = 0.055f, recoil = 5f, range = 70f, headMul = 1.5f, sfx = "shotgun", rl = 0.22f, hl = 0.18f, bl = 0.3f, bt = 0.022f, body = Col.Steel, furn = Col.Wood },
        new WeaponDef { name = "Benelli M4", cat = WCat.Shotgun, sight = Sight.RedDot, stock = StockT.Fixed, magT = MagT.Tube, dmg = 13, pellets = 9, rate = 3.5f, mag = 7, reserve = 48, reload = 3.2f, spread = 0.06f, recoil = 4.5f, range = 70f, headMul = 1.5f, sfx = "shotgun", pitch = 1.08f, rl = 0.25f, hl = 0.18f, bl = 0.28f, bt = 0.022f },
        new WeaponDef { name = "Doppelflinte", cat = WCat.Shotgun, stock = StockT.Wood, magT = MagT.None, dbl = true, dmg = 18, pellets = 10, rate = 5, mag = 2, reserve = 40, reload = 2.4f, spread = 0.07f, recoil = 6f, range = 60f, headMul = 1.5f, sfx = "shotgun", pitch = 0.9f, rl = 0.12f, hl = 0.22f, bl = 0.42f, bt = 0.022f, body = Col.Steel, furn = Col.Wood },

        // ---- marksman rifles
        new WeaponDef { name = "SVD Dragunov", cat = WCat.DMR, sight = Sight.Scope, zoomFov = 24, stock = StockT.Wood, dmg = 85, headMul = 3f, rate = 3.2f, mag = 10, reserve = 60, reload = 2.8f, spread = 0.004f, recoil = 3.5f, sfx = "sniper", pitch = 1.2f, rl = 0.3f, hl = 0.3f, bl = 0.3f, brake = true, body = Col.Steel, furn = Col.Wood },
        new WeaponDef { name = "M14 EBR", cat = WCat.DMR, sight = Sight.Acog, zoomFov = 30, stock = StockT.Fixed, dmg = 72, headMul = 3f, rate = 4, mag = 20, reserve = 80, reload = 2.6f, spread = 0.005f, recoil = 3f, sfx = "sniper", pitch = 1.3f, rl = 0.32f, hl = 0.28f, bl = 0.2f, brake = true },

        // ---- sniper rifles
        new WeaponDef { name = "AWM", cat = WCat.Sniper, action = ActionT.Bolt, sight = Sight.Scope, zoomFov = 13, stock = StockT.Fixed, dmg = 170, headMul = 3f, rate = 0.85f, mag = 5, reserve = 30, reload = 3.2f, spread = 0.0015f, recoil = 7f, sfx = "sniper", rl = 0.36f, hl = 0.22f, bl = 0.35f, brake = true, body = Col.Olive, furn = Col.Olive },
        new WeaponDef { name = "Barrett M82", cat = WCat.Sniper, sight = Sight.Scope, zoomFov = 10, stock = StockT.Fixed, dmg = 260, headMul = 3f, rate = 1.5f, mag = 10, reserve = 30, reload = 3.8f, spread = 0.002f, recoil = 10f, sfx = "sniper", pitch = 0.72f, rl = 0.42f, rh = 0.09f, hl = 0.3f, bl = 0.4f, bt = 0.024f, brake = true },

        // ---- launchers (slot 3)
        new WeaponDef { name = "RPG-7", cat = WCat.Launcher, rocket = true, magT = MagT.None, stock = StockT.None, dmg = 300, rate = 0.5f, mag = 1, reserve = 6, reload = 3.2f, spread = 0.004f, recoil = 8f, sfx = "rocket", body = Col.Olive, furn = Col.Wood },
        new WeaponDef { name = "M32 Granatwerfer", cat = WCat.Launcher, glauncher = true, sight = Sight.RedDot, magT = MagT.Cylinder, stock = StockT.Skeleton, dmg = 170, rate = 1.6f, mag = 6, reserve = 18, reload = 4.2f, spread = 0.006f, recoil = 4f, sfx = "gl", rl = 0.2f, hl = 0.0f, bl = 0.28f, bt = 0.026f },
    };

    public static int Find(string name)
    {
        for (int i = 0; i < All.Length; i++) if (All[i].name == name) return i;
        return 0;
    }

    public static List<int> OfCat(params WCat[] cats)
    {
        var l = new List<int>();
        for (int i = 0; i < All.Length; i++)
            foreach (var c in cats)
                if (All[i].cat == c) { l.Add(i); break; }
        return l;
    }

    public static int Random(params WCat[] cats)
    {
        var l = OfCat(cats);
        return l[UnityEngine.Random.Range(0, l.Count)];
    }

    public static readonly List<int> Primaries = OfCat(WCat.SMG, WCat.Rifle, WCat.LMG, WCat.Shotgun, WCat.DMR, WCat.Sniper);
    public static readonly List<int> Secondaries = OfCat(WCat.Pistol);
}
