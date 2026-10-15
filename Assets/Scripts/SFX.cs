using System.Collections.Generic;
using UnityEngine;

// All sounds are synthesized at startup, so no audio assets are needed.
public static class SFX
{
    const int SR = 22050;
    static Dictionary<string, AudioClip> clips;
    static AudioSource[] src2D, src3D;
    static int i2, i3;
    static GameObject host;
    static System.Random rng = new System.Random(7);

    public static void Init()
    {
        if (host != null) return;
        host = new GameObject("SFX");
        Object.DontDestroyOnLoad(host);
        src2D = new AudioSource[14];
        for (int i = 0; i < src2D.Length; i++)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 0f;
            src2D[i] = s;
        }
        src3D = new AudioSource[28];
        for (int i = 0; i < src3D.Length; i++)
        {
            var g = new GameObject("s3d");
            g.transform.SetParent(host.transform);
            var s = g.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 4f; s.maxDistance = 90f; s.dopplerLevel = 0f;
            src3D[i] = s;
        }
        if (clips == null) Build();
    }

    public static void Play2D(string name, float vol = 1f, float pitch = 1f)
    {
        if (clips == null || !clips.TryGetValue(name, out var c)) return;
        var s = src2D[i2 = (i2 + 1) % src2D.Length];
        s.pitch = pitch; s.volume = vol; s.clip = c; s.Play();
    }

    public static void Play3D(string name, Vector3 pos, float vol = 1f, float pitch = 1f)
    {
        if (clips == null || !clips.TryGetValue(name, out var c)) return;
        var s = src3D[i3 = (i3 + 1) % src3D.Length];
        s.transform.position = pos;
        s.pitch = pitch; s.volume = vol; s.clip = c; s.Play();
    }

    static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

    static void Add(string name, float dur, System.Func<float, float> f)
    {
        int n = Mathf.CeilToInt(dur * SR);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float fade = Mathf.Clamp01((dur - t) / 0.02f); // avoid clicks at end
            d[i] = Mathf.Clamp(f(t) * fade, -1f, 1f);
        }
        var c = AudioClip.Create(name, n, 1, SR, false);
        c.SetData(d, 0);
        clips[name] = c;
    }

    static void Shot(string name, float dur, float cut, float dec, float boom, float boomDec, float gain)
    {
        float lp = 0f;
        Add(name, dur, t =>
        {
            lp += (Noise() - lp) * cut;
            float crack = lp * Mathf.Exp(-t * dec) * 1.8f;
            float body = Mathf.Sin(2f * Mathf.PI * boom * t * (1f - t * 0.6f)) * Mathf.Exp(-t * boomDec);
            return gain * (crack + body);
        });
    }

    static void Build()
    {
        clips = new Dictionary<string, AudioClip>();
        const float TAU = Mathf.PI * 2f;

        Shot("pistol", 0.3f, 0.5f, 26f, 150f, 20f, 0.7f);
        Shot("rifle", 0.22f, 0.6f, 36f, 120f, 28f, 0.6f);
        Shot("shotgun", 0.65f, 0.28f, 8f, 75f, 8f, 0.95f);
        Shot("sniper", 1.0f, 0.55f, 6f, 95f, 5f, 0.85f);

        { float lp = 0; Add("rocket", 0.8f, t => { lp += (Noise() - lp) * 0.12f; float env = t < 0.04f ? t / 0.04f : Mathf.Exp(-(t - 0.04f) * 3.5f); return lp * env * 2.5f + Mathf.Sin(TAU * 90f * t) * Mathf.Exp(-t * 18f) * 0.5f; }); }
        { float lp = 0; Add("explosion", 1.8f, t => { lp += (Noise() - lp) * 0.07f; return lp * Mathf.Exp(-t * 2.2f) * 3.2f + Mathf.Sin(TAU * (55f - 18f * t) * t) * Mathf.Exp(-t * 3f) * 0.7f; }); }
        { float ph = 0; Add("eshot", 0.25f, t => { ph += TAU * (900f * Mathf.Exp(-t * 7f) + 120f) / SR; return Mathf.Sign(Mathf.Sin(ph)) * 0.22f * Mathf.Exp(-t * 11f); }); }
        Add("hit", 0.08f, t => Mathf.Sin(TAU * 1500f * t) * Mathf.Exp(-t * 45f) * 0.5f);
        Add("headshot", 0.3f, t => (Mathf.Sin(TAU * 1800f * t) + Mathf.Sin(TAU * 2700f * t)) * Mathf.Exp(-t * 12f) * 0.3f);
        { float lp = 0; Add("hurt", 0.3f, t => { lp += (Noise() - lp) * 0.2f; return lp * Mathf.Exp(-t * 12f) * 1.5f + Mathf.Sin(TAU * 85f * t) * Mathf.Exp(-t * 10f) * 0.7f; }); }
        Add("pickup", 0.32f, t => { float f = t < 0.08f ? 660f : t < 0.16f ? 880f : 1320f; return Mathf.Sin(TAU * f * t) * 0.35f * Mathf.Exp(-(t % 0.08f) * 8f); });
        Add("reload", 0.42f, t =>
        {
            float a = t > 0.0f ? Noise() * Mathf.Exp(-t * 140f) : 0f;
            float b = t > 0.22f ? Noise() * Mathf.Exp(-(t - 0.22f) * 110f) + Mathf.Sin(TAU * 900f * t) * Mathf.Exp(-(t - 0.22f) * 60f) * 0.4f : 0f;
            return (a + b) * 0.6f;
        });
        Add("empty", 0.06f, t => Noise() * Mathf.Exp(-t * 150f) * 0.5f + Mathf.Sin(TAU * 2200f * t) * Mathf.Exp(-t * 90f) * 0.3f);
        Add("swap", 0.12f, t => Noise() * Mathf.Exp(-t * 60f) * 0.25f + Mathf.Sin(TAU * 600f * t) * Mathf.Exp(-t * 40f) * 0.2f);
        { float ph = 0; Add("edie", 0.55f, t => { ph += TAU * (420f * Mathf.Exp(-t * 4f) + 40f) / SR; return (Mathf.Sign(Mathf.Sin(ph)) * 0.25f + Noise() * 0.2f) * Mathf.Exp(-t * 5f); }); }
        { float lp = 0; Add("melee", 0.28f, t => { lp += (Noise() - lp) * 0.1f; float env = t < 0.05f ? t / 0.05f : Mathf.Exp(-(t - 0.05f) * 14f); return lp * env * 2.5f + Mathf.Sin(TAU * 70f * t) * Mathf.Exp(-t * 15f) * 0.6f; }); }
        Add("wave", 1.6f, t =>
        {
            float env = Mathf.Min(1f, t / 0.12f) * Mathf.Clamp01((1.6f - t) / 0.6f);
            return (Mathf.Sin(TAU * 196f * t) + 0.7f * Mathf.Sin(TAU * 294f * t) + 0.5f * Mathf.Sin(TAU * 392f * t) + 0.3f * Mathf.Sin(TAU * 588f * t)) * 0.16f * env;
        });
        { float lp = 0; Add("boss", 1.8f, t => { lp += (Noise() - lp) * 0.05f; float env = Mathf.Min(1f, t / 0.2f) * Mathf.Exp(-t * 1.2f); return (Mathf.Sin(TAU * (50f + 12f * Mathf.Sin(TAU * 7f * t)) * t) * 0.6f + lp * 2f) * env; }); }
        Add("click", 0.05f, t => Mathf.Sin(TAU * 1200f * t) * Mathf.Exp(-t * 80f) * 0.3f);
    }
}
