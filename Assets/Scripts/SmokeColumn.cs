using UnityEngine;

// Continuous rising smoke (and optional flames) from a burning wreck.
public class SmokeColumn : MonoBehaviour
{
    public bool fire = true;
    public float size = 1f;
    float t, ft;

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        var cam = Camera.main;
        if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 260f * 260f) return;
        t -= dt;
        if (t <= 0f)
        {
            t = 0.35f;
            var s = Fx.Billboard(transform.position + Random.insideUnitSphere * 0.5f * size, TexGen.Smoke(),
                new Color(0.12f, 0.12f, 0.12f, 0.5f), 1.2f * size, 9f * size, Random.Range(7f, 10f));
            s.vel = new Vector3(0.6f, 2.2f, 0.25f) * size + Random.insideUnitSphere * 0.3f;
            s.rollSpeed = Random.Range(-10f, 10f);
            s.fadeIn = true;
        }
        if (!fire) return;
        ft -= dt;
        if (ft <= 0f)
        {
            ft = 0.08f;
            var f = Fx.Billboard(transform.position + Random.insideUnitSphere * 0.4f * size, TexGen.Smoke(),
                new Color(1f, Random.Range(0.4f, 0.65f), 0.15f, 0.9f), 0.8f * size, 0.2f * size, 0.5f);
            f.vel = Vector3.up * 2.5f * size;
        }
    }
}
