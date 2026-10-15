using UnityEngine;

// Camera image effect: bloom, ACES tonemapping, grading, vignette, grain.
[RequireComponent(typeof(Camera))]
public class PostFX : MonoBehaviour
{
    static Material mat;
    public float desat;
    public float exposure = 1.15f;
    readonly RenderTexture[] chain = new RenderTexture[6];

    void Awake()
    {
        GetComponent<Camera>().allowHDR = true;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null)
        {
            var b = Resources.Load<Material>("Mats/Post");
            if (b == null) { Graphics.Blit(src, dst); return; }
            mat = new Material(b) { hideFlags = HideFlags.DontUnloadUnusedAsset };
        }
        mat.SetFloat("_Threshold", 1.0f);
        mat.SetFloat("_Intensity", 0.35f);
        mat.SetFloat("_Exposure", exposure);
        mat.SetFloat("_Saturation", 1.08f);
        mat.SetFloat("_Contrast", 1.12f);
        mat.SetFloat("_Vignette", 0.9f);
        mat.SetFloat("_Grain", 0.025f);
        mat.SetFloat("_Desat", desat);
        mat.SetColor("_Tint", new Color(1.02f, 1f, 0.96f));

        int w = Mathf.Max(1, src.width / 2), h = Mathf.Max(1, src.height / 2);
        var fmt = src.format;
        chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
        Graphics.Blit(src, chain[0], mat, 0);
        int n = 1;
        for (; n < chain.Length; n++)
        {
            w /= 2; h /= 2;
            if (w < 8 || h < 8) break;
            chain[n] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(chain[n - 1], chain[n], mat, 1);
        }
        for (int i = n - 1; i > 0; i--) Graphics.Blit(chain[i], chain[i - 1], mat, 2);
        mat.SetTexture("_Bloom", chain[0]);
        Graphics.Blit(src, dst, mat, 3);
        for (int i = 0; i < n; i++)
        {
            RenderTexture.ReleaseTemporary(chain[i]);
            chain[i] = null;
        }
    }
}
