using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Creates the materials and scene the game needs, then builds WebGL into ../docs (GitHub Pages).
public static class BuildScript
{
    const string MatDir = "Assets/Resources/Mats";
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Frontline/Setup Scene and Assets")]
    public static void Setup()
    {
        Directory.CreateDirectory(MatDir);
        MakeMat("Lit", "Standard", m => { m.SetFloat("_Glossiness", 0.1f); m.enableInstancing = true; });
        MakeMat("LitEmissive", "Standard", m =>
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.enableInstancing = true;
        });
        MakeMat("Unlit", "Unlit/Color", null);
        MakeMat("Fx", "Sprites/Default", null);
        MakeMat("Sky", "Skybox/Procedural", m =>
        {
            m.SetFloat("_SunSize", 0.04f);
            m.SetFloat("_AtmosphereThickness", 0.85f);
            m.SetColor("_SkyTint", new Color(0.5f, 0.55f, 0.65f));
            m.SetColor("_GroundColor", new Color(0.55f, 0.58f, 0.62f));
            m.SetFloat("_Exposure", 1.25f);
        });
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Game").AddComponent<GameManager>();
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 230f;
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Sky.mat");
        RenderSettings.ambientMode = AmbientMode.Trilight;
        Lightmapping.bakedGI = false;
        Lightmapping.realtimeGI = false;
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    static void MakeMat(string name, string shader, System.Action<Material> cfg)
    {
        string path = $"{MatDir}/{name}.mat";
        var sh = Shader.Find(shader);
        if (sh == null) { Debug.LogError("Shader missing: " + shader); return; }
        var m = new Material(sh);
        cfg?.Invoke(m);
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
    }

    [MenuItem("Frontline/Build WebGL")]
    public static void BuildWebGL()
    {
        Setup();
        PlayerSettings.companyName = "timonbi97";
        PlayerSettings.productName = "Frontline Arena";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.template = "PROJECT:Frontline";
        PlayerSettings.runInBackground = true;

        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs"));
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("BUILD RESULT: " + report.summary.result + "  size: " + report.summary.totalSize);
        File.WriteAllText(Path.Combine(outDir, ".nojekyll"), "");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
