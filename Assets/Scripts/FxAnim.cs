using UnityEngine;

// Generic short-lived animated effect (scale, fade, velocity, light).
public class FxAnim : MonoBehaviour
{
    public float life = 0.3f, t;
    public Vector3 s0, s1, vel, spin;
    public float gravity, drag;
    public Material mat;
    public Color c0;
    public bool fade = true;
    public Light lt;
    public float lt0;

    void Update()
    {
        float dt = Time.deltaTime;
        t += dt;
        float k = Mathf.Clamp01(t / life);
        transform.localScale = Vector3.Lerp(s0, s1, k);
        vel.y -= gravity * dt;
        if (drag > 0) vel *= 1f / (1f + drag * dt);
        transform.position += vel * dt;
        if (spin != Vector3.zero) transform.Rotate(spin * dt);
        if (mat != null && fade) { var c = c0; c.a *= 1f - k; mat.color = c; }
        if (lt != null) lt.intensity = lt0 * (1f - k);
        if (t >= life) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
