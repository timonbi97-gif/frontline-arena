using System.Collections.Generic;
using UnityEngine;

// All sounds are synthesized at startup, so no audio assets are needed.
public static class SFX
{
    const int SR = 22050;
    const float TAU = Mathf.PI * 2f;
    static Dictionary<string, AudioClip> clips;
    static AudioSource[] src2D, src3D;
    static AudioSource wind;
    static int i2, i3;
    static GameObject host;
    static System.Random rng = new System.Random(7);

    public static void Init()
    {
        if (host != null) return;
        host = new GameObject("SFX");
        Object.DontDestroyOnLoad(host);
        src2D = new AudioSource[16];
        for (int i = 0; i < src2D.Length; i++)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 0f;
            src2D[i] = s;
        }
        src3D = new AudioSource[32];
        for (int i = 0; i < src3D.Length; i++)
        {
            var g = new GameObject("s3d");
            g.transform.SetParent(host.transform);
            var s = g.AddComponent<AudioSource>();
            s.playOnAwake = false; s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 6f; s.maxDistance = 250f; s.dopplerLevel = 0f; s.spread = 40f;
            src3D[i] = s;
        }
        if (clips == null) Build();
        wind = host.AddComponent<AudioSource>();
        wind.clip = clips["wind"]; wind.loop = true; wind.volume = 0.35f; wind.spatialBlend = 0f;
        wind.Play();
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

    // Builds a clip; reverb adds outdoor slap-back echoes (delayed, low-passed copies).
    static void Add(string name, float dur, System.Func<float, float> f, float reverb = 0f, float gain = 1f)
    {
        int n = Mathf.CeilToInt(dur * SR);
        var dry = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            dry[i] = f(t) * Mathf.Clamp01((dur - t) / 0.02f);
        }
        float[] outBuf = dry;
        if (reverb > 0f)
        {
            float[] delays = { 0.07f, 0.16f, 0.29f, 0.45f, 0.68f };
            float[] gains = { 0.45f, 0.32f, 0.22f, 0.14f, 0.08f };
            int tail = Mathf.CeilToInt(1.1f * SR);
            outBuf = new float[n + tail];
            System.Array.Copy(dry, outBuf, n);
            for (int k = 0; k < delays.Length; k++)
            {
                int d = Mathf.RoundToInt(delays[k] * SR);
                float lp = 0f, cut = 0.25f / (k + 1);
                for (int i = 0; i < n + tail - d; i++)
                {
                    float x = i < n ? dry[i] : 0f;
                    lp += (x - lp) * cut;
                    outBuf[i + d] += lp * gains[k] * reverb * 2.2f;
                }
            }
            int len = outBuf.Length;
            for (int i = len - 2000; i < len; i++) outBuf[i] *= (len - i) / 2000f;
        }
        for (int i = 0; i < outBuf.Length; i++) outBuf[i] = Mathf.Clamp(outBuf[i] * gain, -1f, 1f);
        var c = AudioClip.Create(name, outBuf.Length, 1, SR, false);
        c.SetData(outBuf, 0);
        clips[name] = c;
    }

    // Gunshot: sharp crack + low body thump, optional distance low-pass.
    static void Shot(string name, float dur, float cut, float dec, float boom, float boomDec, float gain, float reverb, float distant = 0f)
    {
        float lp = 0f, lp2 = 0f;
        Add(name, dur, t =>
        {
            lp += (Noise() - lp) * cut;
            float crack = lp * Mathf.Exp(-t * dec) * 2f;
            float body = Mathf.Sin(TAU * boom * t * (1f - t * 0.8f)) * Mathf.Exp(-t * boomDec) * 1.1f;
            float s = crack + body;
            if (distant > 0f) { lp2 += (s - lp2) * (1f - distant); s = lp2 * (1f + distant * 2f); }
            return s;
        }, reverb, gain);
    }

    static void Build()
    {
        clips = new Dictionary<string, AudioClip>();

        Shot("pistol", 0.35f, 0.55f, 30f, 140f, 22f, 0.75f, 0.7f);
        Shot("rifle", 0.28f, 0.65f, 34f, 110f, 26f, 0.75f, 0.8f);
        Shot("shotgun", 0.6f, 0.32f, 10f, 70f, 9f, 0.95f, 0.9f);
        Shot("sniper", 0.7f, 0.6f, 9f, 85f, 7f, 0.95f, 1f);
        Shot("erifle", 0.25f, 0.5f, 30f, 120f, 24f, 0.7f, 0.9f);
        Shot("esmg", 0.18f, 0.55f, 45f, 160f, 35f, 0.6f, 0.7f);
        Shot("emg", 0.26f, 0.45f, 28f, 95f, 22f, 0.8f, 1f);
        Shot("far", 0.4f, 0.4f, 18f, 80f, 14f, 0.5f, 1f, 0.9f);
        Shot("smg", 0.2f, 0.6f, 42f, 150f, 32f, 0.65f, 0.6f);
        Shot("ak", 0.3f, 0.55f, 28f, 95f, 20f, 0.8f, 0.9f);
        Shot("lmg", 0.3f, 0.5f, 26f, 90f, 18f, 0.85f, 1f);
        { float lp = 0; Add("gl", 0.35f, t => { lp += (Noise() - lp) * 0.15f; return lp * Mathf.Exp(-t * 20f) * 2f + Mathf.Sin(TAU * 60f * t) * Mathf.Exp(-t * 14f) * 0.9f; }, 0.5f); }
        Add("pump", 0.45f, t => { float s = 0f; foreach (float t0 in new[] { 0f, 0.2f }) if (t > t0) s += Noise() * Mathf.Exp(-(t - t0) * 90f) + Mathf.Sin(TAU * 700f * t) * Mathf.Exp(-(t - t0) * 50f) * 0.4f; return s * 0.6f; });
        Add("bolt", 0.7f, t => { float s = 0f; foreach (float t0 in new[] { 0f, 0.18f, 0.4f, 0.55f }) if (t > t0) s += Noise() * Mathf.Exp(-(t - t0) * 120f) + Mathf.Sin(TAU * 1300f * t) * Mathf.Exp(-(t - t0) * 70f) * 0.3f; return s * 0.5f; });

        { float lp = 0; Add("rocket", 0.9f, t => { lp += (Noise() - lp) * 0.12f; float env = t < 0.04f ? t / 0.04f : Mathf.Exp(-(t - 0.04f) * 3f); return lp * env * 2.6f + Mathf.Sin(TAU * 80f * t) * Mathf.Exp(-t * 16f) * 0.7f; }, 0.6f); }
        { float lp = 0; Add("explosion", 1.6f, t => { lp += (Noise() - lp) * 0.06f; return lp * Mathf.Exp(-t * 2.4f) * 3.6f + Mathf.Sin(TAU * (48f - 16f * t) * t) * Mathf.Exp(-t * 3f) * 0.9f; }, 1f); }
        { float lp = 0; Add("farboom", 1.6f, t => { lp += (Noise() - lp) * 0.025f; return lp * Mathf.Exp(-t * 2f) * 5f; }, 1f, 0.6f); }
        { float lp = 0, ph = 0; Add("whiz", 0.25f, t => { lp += (Noise() - lp) * 0.4f; ph += TAU * (3000f - 8000f * t) / SR; float env = Mathf.Sin(Mathf.Clamp01(t / 0.25f) * Mathf.PI); return (lp * 0.7f + Mathf.Sin(ph) * 0.15f) * env * env; }); }
        Add("hit", 0.08f, t => Mathf.Sin(TAU * 1500f * t) * Mathf.Exp(-t * 45f) * 0.45f);
        Add("headshot", 0.25f, t => (Mathf.Sin(TAU * 1800f * t) + Mathf.Sin(TAU * 2700f * t)) * Mathf.Exp(-t * 14f) * 0.28f);
        { float lp = 0; Add("flesh", 0.12f, t => { lp += (Noise() - lp) * 0.3f; return lp * Mathf.Exp(-t * 35f) * 1.5f + Mathf.Sin(TAU * 140f * t) * Mathf.Exp(-t * 30f) * 0.5f; }); }
        { float lp = 0; Add("impact", 0.1f, t => { lp += (Noise() - lp) * 0.7f; return lp * Mathf.Exp(-t * 60f) * 0.9f; }); }
        { float lp = 0; Add("hurt", 0.3f, t => { lp += (Noise() - lp) * 0.2f; return lp * Mathf.Exp(-t * 12f) * 1.5f + Mathf.Sin(TAU * 85f * t) * Mathf.Exp(-t * 10f) * 0.7f; }); }
        Add("pickup", 0.3f, t => { float f = t < 0.08f ? 660f : t < 0.16f ? 880f : 1320f; return Mathf.Sin(TAU * f * t) * 0.3f * Mathf.Exp(-(t % 0.08f) * 8f); });
        Add("reload", 0.9f, t =>
        {
            float s = 0f;
            foreach (float t0 in new[] { 0f, 0.35f, 0.62f })
                if (t > t0) s += Noise() * Mathf.Exp(-(t - t0) * 140f) + Mathf.Sin(TAU * 1100f * t) * Mathf.Exp(-(t - t0) * 70f) * 0.35f;
            return s * 0.55f;
        });
        Add("empty", 0.06f, t => Noise() * Mathf.Exp(-t * 150f) * 0.5f + Mathf.Sin(TAU * 2200f * t) * Mathf.Exp(-t * 90f) * 0.3f);
        Add("swap", 0.15f, t => Noise() * Mathf.Exp(-t * 60f) * 0.25f + Mathf.Sin(TAU * 600f * t) * Mathf.Exp(-t * 40f) * 0.2f);
        Add("tink", 0.15f, t => (Mathf.Sin(TAU * 2600f * t) + 0.6f * Mathf.Sin(TAU * 4100f * t)) * Mathf.Exp(-t * 30f) * 0.3f);
        Add("shell", 0.12f, t => (Mathf.Sin(TAU * 3400f * t) + 0.5f * Mathf.Sin(TAU * 5200f * t)) * Mathf.Exp(-t * 40f) * 0.12f);
        { float lp = 0; Add("step", 0.12f, t => { lp += (Noise() - lp) * 0.25f; return lp * Mathf.Exp(-t * 35f) * 0.9f; }); }
        { float lp = 0; Add("edie", 0.4f, t => { lp += (Noise() - lp) * 0.12f; return lp * Mathf.Exp(-t * 7f) * 1.6f + Mathf.Sin(TAU * (180f - 200f * t) * t) * Mathf.Exp(-t * 6f) * 0.4f; }); }
        { float lp = 0; Add("melee", 0.28f, t => { lp += (Noise() - lp) * 0.1f; float env = t < 0.05f ? t / 0.05f : Mathf.Exp(-(t - 0.05f) * 14f); return lp * env * 2.5f + Mathf.Sin(TAU * 70f * t) * Mathf.Exp(-t * 15f) * 0.6f; }); }
        Add("wave", 2.2f, t =>
        {
            float env = Mathf.Min(1f, t / 0.3f) * Mathf.Clamp01((2.2f - t) / 0.9f);
            float vib = 1f + 0.004f * Mathf.Sin(TAU * 5f * t);
            return (Mathf.Sin(TAU * 110f * vib * t) + 0.6f * Mathf.Sin(TAU * 165f * vib * t) + 0.35f * Mathf.Sin(TAU * 220f * t)) * 0.18f * env;
        }, 0.8f);
        { float lp = 0; Add("boss", 1.8f, t => { lp += (Noise() - lp) * 0.05f; float env = Mathf.Min(1f, t / 0.2f) * Mathf.Exp(-t * 1.2f); return (Mathf.Sin(TAU * (50f + 12f * Mathf.Sin(TAU * 7f * t)) * t) * 0.6f + lp * 2f) * env; }, 0.6f); }
        Add("click", 0.05f, t => Mathf.Sin(TAU * 1200f * t) * Mathf.Exp(-t * 80f) * 0.3f);
        Add("bird", 0.5f, t =>
        {
            float ph = t * 14f; float seg = ph - Mathf.Floor(ph);
            float f = 3200f + 900f * Mathf.Sin(TAU * seg);
            return Mathf.Sin(TAU * f * t) * Mathf.Sin(seg * Mathf.PI) * 0.12f * (Mathf.Floor(ph) % 2 == 0 ? 1f : 0f);
        });

        // seamless wind loop
        {
            const float dur = 8f;
            int n = (int)(dur * SR);
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                lp += (Noise() - lp) * 0.02f;
                lp2 += (lp - lp2) * 0.05f;
                float gust = 0.6f + 0.4f * Mathf.Sin(TAU * t / dur) * Mathf.Sin(TAU * 3f * t / dur);
                d[i] = lp2 * 6f * gust;
            }
            int fade = SR;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                d[i] = d[i] * k + d[n - fade + i] * (1f - k);
            }
            var trimmed = new float[n - fade];
            System.Array.Copy(d, trimmed, n - fade);
            var c = AudioClip.Create("wind", trimmed.Length, 1, SR, false);
            c.SetData(trimmed, 0);
            clips["wind"] = c;
        }
    }
}
