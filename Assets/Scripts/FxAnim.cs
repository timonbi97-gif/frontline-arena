using UnityEngine;

// Generic short-lived animated effect (scale, fade, velocity, light, optional camera-facing billboard).
public class FxAnim : MonoBehaviour
{
    public float life = 0.3f, t;
    public Vector3 s0, s1, vel, spin;
    public float gravity, drag;
    public Material mat;
    public Color c0;
    public bool fade = true, billboard, fadeIn;
    public float roll, rollSpeed;
    public Light lt;
    public float lt0;
    public float bounceY = float.NegativeInfinity;

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        t += dt;
        float k = Mathf.Clamp01(t / life);
        transform.localScale = Vector3.Lerp(s0, s1, 1f - (1f - k) * (1f - k));
        vel.y -= gravity * dt;
        if (drag > 0) vel *= 1f / (1f + drag * dt);
        var p = transform.position + vel * dt;
        if (p.y < bounceY && vel.y < 0)
        {
            p.y = bounceY;
            vel = new Vector3(vel.x * 0.5f, -vel.y * 0.35f, vel.z * 0.5f);
            spin *= 0.5f;
        }
        transform.position = p;
        if (billboard)
        {
            var cam = Camera.main;
            roll += rollSpeed * dt;
            if (cam != null) transform.rotation = cam.transform.rotation * Quaternion.Euler(0, 0, roll);
        }
        else if (spin != Vector3.zero) transform.Rotate(spin * dt);
        if (mat != null && fade)
        {
            var c = c0;
            float a = 1f - k;
            if (fadeIn) a *= Mathf.Clamp01(k * 6f);
            c.a *= a;
            mat.color = c;
        }
        if (lt != null) lt.intensity = lt0 * (1f - k);
        if (t >= life) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
