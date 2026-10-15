using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I;
    public enum State { Menu, Playing, Paused, GameOver }
    public State state;

    static bool autoStart;
    static int difficulty = 1;
    static readonly string[] DiffNames = { "Leicht", "Normal", "Schwer" };
    static readonly float[] DiffHp = { 0.7f, 1f, 1.35f };
    static readonly float[] DiffDmg = { 0.55f, 1f, 1.4f };
    static readonly float[] DiffCount = { 0.8f, 1f, 1.25f };
    public float DmgTakenMul => DiffDmg[difficulty];

    World world;
    Camera menuCam;
    float menuAngle;
    int menuPage;

    public int wave, score, kills, headshots;
    int combo;
    float comboT, intermission, spawnT, playTime, lockedTime, gameOverT = -1f;
    bool waveActive, hadLock, showMap, newHigh;
    int highscore;
    readonly List<EType> queue = new List<EType>();
    Enemy boss;

    struct FeedItem { public string text; public Color color; public float time; }
    readonly List<FeedItem> feed = new List<FeedItem>();
    string annText = "";
    Color annColor = Color.white;
    float annTime = -10f;

    struct Respawn { public float time; public Vector3 pos; public PType type; public int weapon; }
    readonly List<Respawn> respawns = new List<Respawn>();

    // UI
    float S = 1f;
    GUIStyle sTitle, sBig, sHuge, sMed, sSmall, sBtn, sSlider, sThumb;
    Texture2D vignetteTex, scopeTex, arrowTex, btnTex, btnHoverTex, ringTex;
    static readonly Color Accent = new Color(1f, 0.48f, 0.18f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

    public bool MouseActive => state == State.Playing && Cursor.lockState == CursorLockMode.Locked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I == null && FindAnyObjectByType<GameManager>() == null)
            new GameObject("Game").AddComponent<GameManager>();
    }

    void Awake()
    {
        I = this;
        Enemy.All.Clear();
        Pickup.All.Clear();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SFX.Init();
        Player.Sensitivity = PlayerPrefs.GetFloat("sens", 1.6f);
        AudioListener.volume = PlayerPrefs.GetFloat("vol", 0.8f);
        difficulty = PlayerPrefs.GetInt("diff", 1);
        highscore = PlayerPrefs.GetInt("hs", 0);
        QualitySettings.antiAliasing = 2;

        world = new GameObject("World").AddComponent<World>();
        world.Generate(1337);

        var cg = new GameObject("MenuCam");
        menuCam = cg.AddComponent<Camera>();
        menuCam.farClipPlane = 450f;
        menuCam.fieldOfView = 60f;
        cg.AddComponent<AudioListener>();
        UpdateMenuCam(0f);

        MakeTextures();
        if (autoStart) StartGame();
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }

    // ---------------------------------------------------------------- flow

    void StartGame()
    {
        autoStart = false;
        state = State.Playing;
        Time.timeScale = 1f;
        if (menuCam != null) Destroy(menuCam.gameObject);
        Player.Create(world.playerSpawn, 0f);
        SpawnMapPickups();
        wave = 0;
        waveActive = false;
        intermission = 6f;
        LockCursor();
        Announce("ÜBERLEBE DIE WELLEN", Color.white);
        Feed("Waffen-Verstecke sind auf der Karte (M) markiert", Gold);
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        hadLock = false;
        lockedTime = 0f;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Pause()
    {
        state = State.Paused;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        UnlockCursor();
    }

    void Resume()
    {
        state = State.Playing;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        LockCursor();
    }

    void Reload(bool start)
    {
        autoStart = start;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnPlayerDied()
    {
        gameOverT = 2.5f;
        if (score > highscore)
        {
            highscore = score;
            newHigh = true;
            PlayerPrefs.SetInt("hs", highscore);
            PlayerPrefs.Save();
        }
    }

    void Update()
    {
        Pathfinder.budget = 4;
        float dt = Time.deltaTime;

        if (state == State.Menu)
        {
            UnlockCursor();
            UpdateMenuCam(Time.unscaledDeltaTime);
            return;
        }

        if (state == State.Playing)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                lockedTime += Time.unscaledDeltaTime;
                if (lockedTime > 0.3f) hadLock = true;
            }
            else if (hadLock && gameOverT < 0f)
            {
                Pause();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                if (gameOverT < 0f) { Pause(); return; }
            }
            if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0) && gameOverT < 0f) LockCursor();

            showMap = Input.GetKey(KeyCode.M) || Input.GetKey(KeyCode.Tab);

            if (gameOverT >= 0f)
            {
                gameOverT -= dt;
                if (gameOverT < 0f)
                {
                    state = State.GameOver;
                    UnlockCursor();
                }
                return;
            }

            playTime += dt;
            comboT -= dt;
            if (comboT <= 0f) combo = 0;
            UpdateWaves(dt);
            UpdateRespawns();
        }
        else if (state == State.Paused)
        {
            if (Input.GetKeyDown(KeyCode.P)) Resume();
        }
    }

    void UpdateMenuCam(float dt)
    {
        if (menuCam == null) return;
        menuAngle += dt * 4f;
        float a = menuAngle * Mathf.Deg2Rad;
        menuCam.transform.position = new Vector3(Mathf.Sin(a) * 120f, 55f, Mathf.Cos(a) * 120f);
        menuCam.transform.LookAt(new Vector3(0, 8f, 0));
    }

    // ---------------------------------------------------------------- waves

    void UpdateWaves(float dt)
    {
        if (!waveActive)
        {
            intermission -= dt;
            if (intermission <= 0f) BeginWave();
            return;
        }
        spawnT -= dt;
        int cap = Mathf.Min(10 + wave * 2, 28);
        if (queue.Count > 0 && spawnT <= 0f && Enemy.All.Count < cap)
        {
            spawnT = Mathf.Max(0.35f, 1.4f - wave * 0.08f);
            SpawnNext();
        }
        if (queue.Count == 0 && Enemy.All.Count == 0) WaveCleared();
    }

    void BeginWave()
    {
        wave++;
        waveActive = true;
        queue.Clear();
        int n = Mathf.RoundToInt((5 + wave * 3) * DiffCount[difficulty]);
        for (int i = 0; i < n; i++) queue.Add(PickType());
        for (int i = 0; i < queue.Count; i++)
        {
            int j = Random.Range(i, queue.Count);
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }
        if (wave % 5 == 0)
            for (int b = 0; b < wave / 5; b++) queue.Insert(Mathf.Max(0, queue.Count - 3 - b * 4), EType.Boss);
        spawnT = 0f;
        Announce(wave % 5 == 0 ? "WELLE " + wave + " – BOSSWELLE" : "WELLE " + wave, wave % 5 == 0 ? new Color(1f, 0.3f, 0.25f) : Color.white);
        SFX.Play2D("wave", 0.8f);
    }

    EType PickType()
    {
        float r = Random.value;
        if (wave >= 3 && r < Mathf.Min(0.1f + wave * 0.015f, 0.28f)) return EType.Brute;
        if (wave >= 2 && r < 0.55f) return EType.Soldier;
        return EType.Runner;
    }

    void SpawnNext()
    {
        var t = queue[queue.Count - 1];
        queue.RemoveAt(queue.Count - 1);
        var e = Enemy.Spawn(t, FindSpawnPos(t == EType.Boss ? 45f : 35f), wave, DiffHp[difficulty]);
        if (t == EType.Boss)
        {
            boss = e;
            Announce("DER BOSS IST DA!", new Color(1f, 0.25f, 0.2f));
            SFX.Play2D("boss", 1f);
        }
    }

    public void SummonMinions(Vector3 pos, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var off = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * 5f;
            var c = World.ToCell(pos + off);
            if (!world.IsFree(c.x, c.y)) continue;
            var e = Enemy.Spawn(EType.Runner, World.CellCenter(c.x, c.y) + Vector3.up * 0.1f, wave, DiffHp[difficulty]);
            Fx.Smoke(e.transform.position + Vector3.up, 2.5f, 0.8f, 0.6f);
        }
    }

    Vector3 FindSpawnPos(float minDist)
    {
        var spots = world.spawnSpots;
        Vector3 pp = Player.I.transform.position;
        Vector3 fallback = spots[Random.Range(0, spots.Count)];
        for (int i = 0; i < 40; i++)
        {
            var s = spots[Random.Range(0, spots.Count)];
            float d = Vector2.Distance(new Vector2(s.x, s.z), new Vector2(pp.x, pp.z));
            if (d < minDist || d > 110f) continue;
            fallback = s;
            if (Physics.Linecast(s + Vector3.up * 1.6f, Player.I.EyePos, GA.MaskWorld)) break;
        }
        return fallback + Vector3.up * 0.1f;
    }

    void WaveCleared()
    {
        waveActive = false;
        intermission = 10f;
        int bonus = wave * 250;
        score += bonus;
        Announce("WELLE " + wave + " GESCHAFFT!  +" + bonus, Gold);
        SFX.Play2D("pickup", 0.8f, 0.8f);
        var pp = Player.I.transform.position;
        int spawned = 0;
        foreach (var s in world.pickupSpots)
        {
            if (spawned >= 2) break;
            if ((s - pp).sqrMagnitude > 40f * 40f) continue;
            Pickup.Create(spawned == 0 ? PType.Ammo : PType.Health, s + Vector3.up * 0.9f, 0, 30f);
            spawned++;
        }
    }

    public void OnEnemyKilled(Enemy e, bool head)
    {
        kills++;
        if (head) headshots++;
        combo++;
        comboT = 3f;
        float mul = 1f + Mathf.Min(combo - 1, 4) * 0.25f;
        int pts = Mathf.RoundToInt(e.score * (head ? 1.5f : 1f) * mul);
        score += pts;
        Feed(Enemy.Names[(int)e.type] + " eliminiert  +" + pts + (head ? "  KOPFSCHUSS" : ""), head ? Gold : Color.white);

        var pos = e.transform.position + Vector3.up * 0.9f;
        if (e.type == EType.Boss)
        {
            Pickup.Create(PType.Health, pos + Vector3.right * 1.5f, 0, 40f);
            Pickup.Create(PType.Armor, pos - Vector3.right * 1.5f, 0, 40f);
            Pickup.Create(PType.Ammo, pos + Vector3.forward * 1.5f, 0, 40f);
            Announce("BOSS BESIEGT!", Gold);
        }
        else if (Random.value < 0.22f)
        {
            float r = Random.value;
            Pickup.Create(r < 0.4f ? PType.Health : r < 0.85f ? PType.Ammo : PType.Armor, pos, 0, 20f);
        }
    }

    // ---------------------------------------------------------------- pickups

    void SpawnMapPickups()
    {
        foreach (var s in world.pickupSpots)
        {
            float r = Random.value;
            CreateMapPickup(s, r < 0.35f ? PType.Health : r < 0.8f ? PType.Ammo : PType.Armor, 0);
        }
        for (int i = 0; i < world.weaponSpots.Count; i++)
            CreateMapPickup(world.weaponSpots[i], PType.Weapon, world.weaponIds[i]);
    }

    void CreateMapPickup(Vector3 pos, PType t, int w)
    {
        var pk = Pickup.Create(t, pos + Vector3.up * 0.9f, w);
        pk.onTaken = () => respawns.Add(new Respawn { time = Time.time + (t == PType.Weapon ? 45f : 30f), pos = pos, type = t, weapon = w });
    }

    void UpdateRespawns()
    {
        for (int i = respawns.Count - 1; i >= 0; i--)
        {
            if (Time.time < respawns[i].time) continue;
            var r = respawns[i];
            respawns.RemoveAt(i);
            CreateMapPickup(r.pos, r.type, r.weapon);
        }
    }

    // ---------------------------------------------------------------- messages

    public void Feed(string text, Color c)
    {
        feed.Add(new FeedItem { text = text, color = c, time = Time.unscaledTime });
        if (feed.Count > 6) feed.RemoveAt(0);
    }

    public void Announce(string text, Color c)
    {
        annText = text;
        annColor = c;
        annTime = Time.unscaledTime;
    }

    // ---------------------------------------------------------------- textures

    void MakeTextures()
    {
        vignetteTex = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = x / 127f * 2f - 1f, dy = y / 127f * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                vignetteTex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0.25f, 1f, d)));
            }
        vignetteTex.Apply();

        const int SZ = 512;
        scopeTex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < SZ; y++)
            for (int x = 0; x < SZ; x++)
            {
                float dx = x - SZ / 2f + 0.5f, dy = y - SZ / 2f + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (SZ / 2f);
                float a = Mathf.SmoothStep(0.94f, 0.97f, d);
                bool line = (Mathf.Abs(dx) < 1.2f || Mathf.Abs(dy) < 1.2f) && d > 0.04f;
                bool thick = (Mathf.Abs(dx) < 3f || Mathf.Abs(dy) < 3f) && d > 0.45f;
                if (line || thick) a = 1f;
                var c = new Color(0, 0, 0, a);
                if (d < 0.012f) c = new Color(1f, 0.1f, 0.1f, 1f);
                scopeTex.SetPixel(x, y, c);
            }
        scopeTex.Apply();

        arrowTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                bool inside = y >= 6 && y <= 60 && Mathf.Abs(x - 31.5f) <= (60 - y) * 0.45f;
                if (inside && y < 22 && Mathf.Abs(x - 31.5f) < (22 - y) * 0.9f) inside = false;
                arrowTex.SetPixel(x, y, inside ? Color.white : new Color(1, 1, 1, 0));
            }
        arrowTex.Apply();

        ringTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f));
                ringTex.SetPixel(x, y, new Color(1, 1, 1, d < 30f ? 1f : Mathf.Clamp01(32f - d)));
            }
        ringTex.Apply();

        btnTex = Solid(new Color(0.1f, 0.12f, 0.16f, 0.92f));
        btnHoverTex = Solid(new Color(Accent.r, Accent.g, Accent.b, 0.95f));
    }

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    // ---------------------------------------------------------------- GUI helpers

    void Styles()
    {
        S = Screen.height / 1080f;
        if (sMed == null)
        {
            sTitle = Make(TextAnchor.UpperLeft);
            sHuge = Make(TextAnchor.MiddleCenter);
            sBig = Make(TextAnchor.MiddleCenter);
            sMed = Make(TextAnchor.UpperLeft);
            sSmall = Make(TextAnchor.UpperLeft);
            sBtn = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            sBtn.normal.background = btnTex; sBtn.hover.background = btnHoverTex; sBtn.active.background = btnHoverTex;
            sBtn.focused.background = btnTex;
            sBtn.normal.textColor = Color.white; sBtn.hover.textColor = Color.black; sBtn.active.textColor = Color.black;
            sBtn.focused.textColor = Color.white;
            sBtn.border = new RectOffset(0, 0, 0, 0);
            sSlider = new GUIStyle(GUI.skin.horizontalSlider);
            sThumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
        }
        sTitle.fontSize = Mathf.Max(10, (int)(104 * S));
        sHuge.fontSize = Mathf.Max(10, (int)(64 * S));
        sBig.fontSize = Mathf.Max(8, (int)(40 * S));
        sMed.fontSize = Mathf.Max(8, (int)(26 * S));
        sSmall.fontSize = Mathf.Max(7, (int)(19 * S));
        sBtn.fontSize = Mathf.Max(8, (int)(28 * S));
        sBtn.padding = new RectOffset((int)(26 * S), 0, 0, 0);
        sSlider.fixedHeight = Mathf.Max(6, 14 * S);
        sThumb.fixedHeight = Mathf.Max(10, 30 * S);
        sThumb.fixedWidth = Mathf.Max(8, 22 * S);
    }

    static GUIStyle Make(TextAnchor a)
    {
        var s = new GUIStyle(GUI.skin.label) { alignment = a, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
        s.normal.textColor = Color.white;
        return s;
    }

    void Text(Rect r, string t, GUIStyle s, Color c)
    {
        var old = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, c.a * 0.75f);
        GUI.Label(new Rect(r.x + 2 * S, r.y + 2 * S, r.width, r.height), t, s);
        s.normal.textColor = c;
        GUI.Label(r, t, s);
        s.normal.textColor = old;
    }

    static void Box(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    bool Btn(Rect r, string text)
    {
        if (GUI.Button(r, text, sBtn))
        {
            SFX.Play2D("click", 0.8f);
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- GUI

    void OnGUI()
    {
        Styles();
        GUI.depth = 0;
        switch (state)
        {
            case State.Menu: DrawMenu(); break;
            case State.Playing: DrawHUD(); break;
            case State.Paused: DrawHUD(); DrawPause(); break;
            case State.GameOver: DrawGameOver(); break;
        }
    }

    void DrawMenu()
    {
        float W = Screen.width, H = Screen.height;
        Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.2f));
        float panelW = Mathf.Min(W, 640 * S);
        Box(new Rect(0, 0, panelW, H), new Color(0.03f, 0.04f, 0.06f, 0.8f));
        Box(new Rect(panelW, 0, 4 * S, H), Accent);
        float x = 70 * S, y = 90 * S, bw = 470 * S, bh = 68 * S, gap = 84 * S;

        Text(new Rect(x, y, 900 * S, 120 * S), "FRONTLINE", sTitle, Color.white); y += 100 * S;
        Text(new Rect(x, y, 900 * S, 120 * S), "ARENA", sTitle, Accent); y += 135 * S;
        Text(new Rect(x, y, 600 * S, 40 * S), "Überlebe endlose Gegnerwellen", sMed, new Color(0.75f, 0.78f, 0.85f)); y += 80 * S;

        if (menuPage == 0)
        {
            if (Btn(new Rect(x, y, bw, bh), "SPIELEN")) StartGame();
            y += gap;
            if (Btn(new Rect(x, y, bw, bh), "SCHWIERIGKEIT:  " + DiffNames[difficulty].ToUpper()))
            {
                difficulty = (difficulty + 1) % 3;
                PlayerPrefs.SetInt("diff", difficulty);
                PlayerPrefs.Save();
            }
            y += gap;
            if (Btn(new Rect(x, y, bw, bh), "STEUERUNG")) menuPage = 1;
            y += gap;
            if (Btn(new Rect(x, y, bw, bh), "EINSTELLUNGEN")) menuPage = 2;
            y += gap + 20 * S;
            Text(new Rect(x, y, 600 * S, 40 * S), "HIGHSCORE:  " + highscore, sMed, Gold);
        }
        else if (menuPage == 1)
        {
            DrawControls(x, y);
            if (Btn(new Rect(x, H - 130 * S, bw, bh), "ZURÜCK")) menuPage = 0;
        }
        else
        {
            y = DrawSettings(x, y, bw);
            if (Btn(new Rect(x, H - 130 * S, bw, bh), "ZURÜCK")) menuPage = 0;
        }
        Text(new Rect(x, H - 45 * S, 600 * S, 30 * S), "Gemacht mit Unity  •  Maus & Tastatur", sSmall, new Color(1, 1, 1, 0.45f));
    }

    void DrawControls(float x, float y)
    {
        string[,] rows =
        {
            { "WASD", "Bewegen" }, { "Maus", "Zielen" }, { "Linksklick", "Schießen" }, { "Rechtsklick", "Zielen / Zoom" },
            { "Shift", "Sprinten" }, { "Leertaste", "Springen" }, { "R", "Nachladen" }, { "1 – 5 / Mausrad", "Waffe wechseln" },
            { "Q", "Letzte Waffe" }, { "M / Tab", "Karte" }, { "Esc / P", "Pause" }
        };
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            Text(new Rect(x, y, 260 * S, 34 * S), rows[i, 0], sMed, Accent);
            Text(new Rect(x + 250 * S, y, 300 * S, 34 * S), rows[i, 1], sMed, Color.white);
            y += 40 * S;
        }
    }

    float DrawSettings(float x, float y, float w)
    {
        Text(new Rect(x, y, w, 34 * S), "Maus-Empfindlichkeit:  " + Player.Sensitivity.ToString("0.0"), sMed, Color.white);
        y += 44 * S;
        float ns = GUI.HorizontalSlider(new Rect(x, y, w, 30 * S), Player.Sensitivity, 0.2f, 6f, sSlider, sThumb);
        if (Mathf.Abs(ns - Player.Sensitivity) > 0.001f)
        {
            Player.Sensitivity = Mathf.Round(ns * 10f) / 10f;
            PlayerPrefs.SetFloat("sens", Player.Sensitivity);
        }
        y += 60 * S;
        Text(new Rect(x, y, w, 34 * S), "Lautstärke:  " + Mathf.RoundToInt(AudioListener.volume * 100) + "%", sMed, Color.white);
        y += 44 * S;
        float nv = GUI.HorizontalSlider(new Rect(x, y, w, 30 * S), AudioListener.volume, 0f, 1f, sSlider, sThumb);
        if (Mathf.Abs(nv - AudioListener.volume) > 0.001f)
        {
            AudioListener.volume = nv;
            PlayerPrefs.SetFloat("vol", nv);
        }
        y += 60 * S;
        if (Event.current.type == EventType.MouseUp) PlayerPrefs.Save();
        return y;
    }

    void DrawPause()
    {
        float W = Screen.width, H = Screen.height;
        Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.6f));
        float bw = 470 * S, bh = 68 * S, x = W / 2 - bw / 2, y = H * 0.18f;
        Text(new Rect(0, y, W, 90 * S), "PAUSE", sHuge, Color.white);
        y += 120 * S;
        if (Btn(new Rect(x, y, bw, bh), "FORTSETZEN")) Resume();
        y += 84 * S;
        if (Btn(new Rect(x, y, bw, bh), "NEUSTART")) Reload(true);
        y += 84 * S;
        if (Btn(new Rect(x, y, bw, bh), "HAUPTMENÜ")) Reload(false);
        y += 110 * S;
        DrawSettings(x, y, bw);
    }

    void DrawGameOver()
    {
        float W = Screen.width, H = Screen.height;
        Box(new Rect(0, 0, W, H), new Color(0.08f, 0, 0, 0.75f));
        float y = H * 0.12f;
        Text(new Rect(0, y, W, 100 * S), "GEFALLEN", sHuge, new Color(1f, 0.3f, 0.25f));
        y += 100 * S;
        if (newHigh) Text(new Rect(0, y, W, 60 * S), "NEUER HIGHSCORE!", sBig, Gold);
        y += 80 * S;
        var p = Player.I;
        float acc = p != null && p.weapons.shots > 0 ? 100f * p.weapons.hits / p.weapons.shots : 0f;
        int secs = Mathf.FloorToInt(playTime);
        string[,] rows =
        {
            { "Punkte", score.ToString() }, { "Erreichte Welle", wave.ToString() }, { "Kills", kills.ToString() },
            { "Kopfschüsse", headshots.ToString() }, { "Trefferquote", acc.ToString("0") + "%" },
            { "Überlebt", (secs / 60) + ":" + (secs % 60).ToString("00") }, { "Highscore", highscore.ToString() }
        };
        float cw = 560 * S, x = W / 2 - cw / 2;
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            Box(new Rect(x, y, cw, 44 * S), new Color(0, 0, 0, i % 2 == 0 ? 0.35f : 0.2f));
            Text(new Rect(x + 20 * S, y + 6 * S, cw, 40 * S), rows[i, 0], sMed, new Color(0.8f, 0.8f, 0.85f));
            var r = new Rect(x, y + 6 * S, cw - 20 * S, 40 * S);
            var st = new GUIStyle(sMed) { alignment = TextAnchor.UpperRight };
            Text(r, rows[i, 1], st, Color.white);
            y += 46 * S;
        }
        y += 40 * S;
        float bw = 470 * S, bh = 68 * S;
        if (Btn(new Rect(W / 2 - bw / 2, y, bw, bh), "NOCHMAL SPIELEN")) Reload(true);
        y += 84 * S;
        if (Btn(new Rect(W / 2 - bw / 2, y, bw, bh), "HAUPTMENÜ")) Reload(false);
    }

    void DrawHUD()
    {
        var p = Player.I;
        if (p == null) return;
        float W = Screen.width, H = Screen.height;
        var wpn = p.weapons;

        // damage + low health
        float lowHp = p.hp < 30f && !p.dead ? 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
        float va = Mathf.Max(p.hurtFlash * 0.7f, lowHp);
        if (p.dead) va = 0.6f;
        if (va > 0.01f)
        {
            GUI.color = new Color(0.8f, 0f, 0f, va);
            GUI.DrawTexture(new Rect(0, 0, W, H), vignetteTex);
            GUI.color = Color.white;
        }

        bool scoped = wpn.cur == 3 && wpn.Zoomed && p.cam.fieldOfView < 35f;
        if (scoped)
        {
            float sz = H;
            GUI.DrawTexture(new Rect(W / 2 - sz / 2, 0, sz, sz), scopeTex);
            Box(new Rect(0, 0, W / 2 - sz / 2 + 1, H), Color.black);
            Box(new Rect(W / 2 + sz / 2 - 1, 0, W / 2 - sz / 2 + 1, H), Color.black);
        }
        else if (!p.dead) DrawCrosshair(W, H, wpn);

        // hit marker
        if (wpn.hitMarkerT > 0f)
        {
            Color hc = wpn.hitMarkerKill ? new Color(1f, 0.2f, 0.2f) : Color.white;
            hc.a = Mathf.Clamp01(wpn.hitMarkerT / 0.25f);
            var c = new Vector2(W / 2, H / 2);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, c);
            float g = 10 * S, l = 12 * S, t = 3 * S;
            Box(new Rect(c.x - t / 2, c.y - g - l, t, l), hc);
            Box(new Rect(c.x - t / 2, c.y + g, t, l), hc);
            Box(new Rect(c.x - g - l, c.y - t / 2, l, t), hc);
            Box(new Rect(c.x + g, c.y - t / 2, l, t), hc);
            GUI.matrix = m;
        }

        // damage direction
        float since = Time.time - p.lastHitTime;
        if (since < 1.2f && !p.dead)
        {
            Vector3 d = p.lastHitFrom - p.transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.5f)
            {
                float ang = Vector3.SignedAngle(p.transform.forward, d, Vector3.up);
                var m = GUI.matrix;
                var c = new Vector2(W / 2, H / 2);
                GUIUtility.RotateAroundPivot(ang, c);
                Box(new Rect(c.x - 50 * S, c.y - 170 * S, 100 * S, 9 * S), new Color(1f, 0.15f, 0.1f, 1f - since / 1.2f));
                GUI.matrix = m;
            }
        }

        // vitals (bottom-left)
        float x = 30 * S, y = H - 150 * S, bw = 360 * S;
        Box(new Rect(x - 12 * S, y - 12 * S, bw + 24 * S, 132 * S), new Color(0, 0, 0, 0.45f));
        Text(new Rect(x, y - 4 * S, 200 * S, 30 * S), "GESUNDHEIT", sSmall, new Color(1, 1, 1, 0.75f));
        Text(new Rect(x + bw - 80 * S, y - 4 * S, 80 * S, 30 * S), Mathf.CeilToInt(p.hp).ToString(), sSmall, Color.white);
        Bar(new Rect(x, y + 22 * S, bw, 18 * S), p.hp / p.maxHp, p.hp < 30 ? new Color(1f, 0.25f, 0.2f) : new Color(0.35f, 0.9f, 0.4f));
        Text(new Rect(x, y + 46 * S, 200 * S, 30 * S), "RÜSTUNG", sSmall, new Color(1, 1, 1, 0.75f));
        Text(new Rect(x + bw - 80 * S, y + 46 * S, 80 * S, 30 * S), Mathf.CeilToInt(p.armor).ToString(), sSmall, Color.white);
        Bar(new Rect(x, y + 72 * S, bw, 12 * S), p.armor / p.maxArmor, new Color(0.3f, 0.65f, 1f));
        Bar(new Rect(x, y + 96 * S, bw, 6 * S), p.stamina, new Color(1f, 0.85f, 0.35f, 0.8f));

        // ammo (bottom-right)
        var d0 = wpn.Cur;
        float ax = W - 400 * S, ay = H - 170 * S;
        Box(new Rect(ax - 12 * S, ay - 12 * S, 382 * S, 152 * S), new Color(0, 0, 0, 0.45f));
        Text(new Rect(ax, ay - 4 * S, 360 * S, 30 * S), d0.name.ToUpper(), sSmall, new Color(1, 1, 1, 0.8f));
        string reserveTxt = d0.reserveMax < 0 ? "∞" : wpn.reserve[wpn.cur].ToString();
        var bigL = new GUIStyle(sBig) { alignment = TextAnchor.MiddleLeft, fontSize = (int)(54 * S) };
        Text(new Rect(ax, ay + 22 * S, 200 * S, 60 * S), wpn.mag[wpn.cur].ToString(), bigL, wpn.mag[wpn.cur] == 0 ? new Color(1f, 0.3f, 0.3f) : Color.white);
        var midL = new GUIStyle(sMed) { alignment = TextAnchor.MiddleLeft };
        Text(new Rect(ax + 110 * S, ay + 30 * S, 200 * S, 50 * S), "/ " + reserveTxt, midL, new Color(1, 1, 1, 0.6f));
        if (wpn.reloading)
        {
            Bar(new Rect(ax, ay + 86 * S, 356 * S, 6 * S), wpn.ReloadProgress, Accent);
            Text(new Rect(ax + 220 * S, ay + 40 * S, 200 * S, 30 * S), "NACHLADEN", sSmall, Accent);
        }
        else if (wpn.mag[wpn.cur] == 0 && !wpn.HasReserve(wpn.cur))
            Text(new Rect(ax + 200 * S, ay + 40 * S, 200 * S, 30 * S), "KEINE MUNITION", sSmall, new Color(1f, 0.3f, 0.3f));
        for (int i = 0; i < 5; i++)
        {
            var r = new Rect(ax + i * 72 * S, ay + 100 * S, 66 * S, 30 * S);
            bool own = wpn.owned[i];
            Box(r, i == wpn.cur ? new Color(Accent.r, Accent.g, Accent.b, 0.9f) : own ? new Color(1, 1, 1, 0.18f) : new Color(1, 1, 1, 0.05f));
            var cs = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter };
            Text(r, (i + 1).ToString(), cs, own ? (i == wpn.cur ? Color.black : Color.white) : new Color(1, 1, 1, 0.25f));
        }

        // top center info
        var cTop = new GUIStyle(sMed) { alignment = TextAnchor.UpperCenter };
        Box(new Rect(W / 2 - 220 * S, 14 * S, 440 * S, 92 * S), new Color(0, 0, 0, 0.45f));
        Text(new Rect(W / 2 - 220 * S, 20 * S, 440 * S, 40 * S), wave > 0 ? "WELLE " + wave : "VORBEREITUNG", cTop, Accent);
        var sc = new GUIStyle(sBig) { alignment = TextAnchor.UpperCenter };
        Text(new Rect(W / 2 - 220 * S, 50 * S, 440 * S, 50 * S), score.ToString("N0"), sc, Color.white);
        float ty = 112 * S;
        if (!waveActive && gameOverT < 0f)
        {
            Text(new Rect(W / 2 - 300 * S, ty, 600 * S, 40 * S), "Nächste Welle in " + Mathf.CeilToInt(Mathf.Max(0, intermission)), cTop, Color.white);
            ty += 36 * S;
        }
        else if (waveActive)
        {
            Text(new Rect(W / 2 - 300 * S, ty, 600 * S, 40 * S), "Gegner übrig: " + (Enemy.All.Count + queue.Count), cTop, new Color(1, 1, 1, 0.85f));
            ty += 36 * S;
        }
        if (combo > 1)
        {
            float mul = 1f + Mathf.Min(combo - 1, 4) * 0.25f;
            Text(new Rect(W / 2 - 300 * S, ty, 600 * S, 40 * S), "COMBO x" + mul.ToString("0.00"), cTop, Gold);
            ty += 36 * S;
        }

        // boss bar
        if (boss != null && !boss.dead)
        {
            float bwid = 640 * S, bx = W / 2 - bwid / 2, by = ty + 10 * S;
            Text(new Rect(bx, by, bwid, 30 * S), "BOSS", cTop, new Color(1f, 0.3f, 0.25f));
            Bar(new Rect(bx, by + 34 * S, bwid, 16 * S), boss.hp / boss.maxHp, new Color(0.9f, 0.15f, 0.15f));
        }

        DrawMinimap(p);
        DrawFeed(W);

        // announcement
        float at = Time.unscaledTime - annTime;
        if (at < 3f && annText.Length > 0)
        {
            float a = at < 0.2f ? at / 0.2f : at > 2.3f ? 1f - (at - 2.3f) / 0.7f : 1f;
            Color c = annColor; c.a = a;
            Text(new Rect(0, H * 0.27f, W, 80 * S), annText, sHuge, c);
        }

        if (showMap) DrawFullMap(p);

        if (state == State.Playing && Cursor.lockState != CursorLockMode.Locked && gameOverT < 0f)
        {
            Box(new Rect(0, H / 2 - 50 * S, W, 100 * S), new Color(0, 0, 0, 0.6f));
            Text(new Rect(0, H / 2 - 30 * S, W, 60 * S), "KLICKEN ZUM SPIELEN", sBig, Color.white);
        }
    }

    void Bar(Rect r, float f, Color c)
    {
        Box(r, new Color(1, 1, 1, 0.12f));
        Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(f), r.height), c);
    }

    void DrawCrosshair(float W, float H, Weapons wpn)
    {
        float spread = wpn.CurrentSpread();
        float gap = (5f + spread * 900f) * S;
        float l = 11 * S, t = 2.5f * S;
        var c = new Vector2(W / 2, H / 2);
        Color col = new Color(1, 1, 1, 0.9f), sh = new Color(0, 0, 0, 0.6f);
        Rect[] rs =
        {
            new Rect(c.x - t / 2, c.y - gap - l, t, l), new Rect(c.x - t / 2, c.y + gap, t, l),
            new Rect(c.x - gap - l, c.y - t / 2, l, t), new Rect(c.x + gap, c.y - t / 2, l, t)
        };
        foreach (var r in rs) Box(new Rect(r.x - S, r.y - S, r.width + 2 * S, r.height + 2 * S), sh);
        foreach (var r in rs) Box(r, col);
        Box(new Rect(c.x - 1.5f * S, c.y - 1.5f * S, 3 * S, 3 * S), col);
    }

    void DrawMinimap(Player p)
    {
        float size = 230 * S, x = 20 * S, y = 20 * S;
        var r = new Rect(x, y, size, size);
        Box(new Rect(x - 4 * S, y - 4 * S, size + 8 * S, size + 8 * S), new Color(0, 0, 0, 0.6f));
        const float range = 60f;
        Vector3 pp = p.transform.position;
        float u = (pp.x + World.Half) / (World.Half * 2), v = (pp.z + World.Half) / (World.Half * 2);
        float span = range * 2 / (World.Half * 2);
        GUI.color = new Color(1, 1, 1, 0.9f);
        GUI.DrawTextureWithTexCoords(r, world.mapTex, new Rect(u - span / 2, v - span / 2, span, span));
        GUI.color = Color.white;
        float ppm = size / (range * 2);
        var c = new Vector2(x + size / 2, y + size / 2);
        foreach (var pk in Pickup.All)
        {
            if (pk == null) continue;
            Vector3 d = pk.transform.position - pp;
            if (Mathf.Abs(d.x) > range || Mathf.Abs(d.z) > range) continue;
            Color pc = pk.type == PType.Health ? new Color(1f, 0.3f, 0.3f) : pk.type == PType.Armor ? new Color(0.3f, 0.6f, 1f) :
                pk.type == PType.Ammo ? new Color(1f, 0.85f, 0.2f) : Weapons.Defs[pk.weapon].glow;
            float ds = (pk.type == PType.Weapon ? 9 : 5) * S;
            Box(new Rect(c.x + d.x * ppm - ds / 2, c.y - d.z * ppm - ds / 2, ds, ds), pc);
        }
        foreach (var e in Enemy.All)
        {
            Vector3 d = e.transform.position - pp;
            if (Mathf.Abs(d.x) > range || Mathf.Abs(d.z) > range) continue;
            float ds = (e.type == EType.Boss ? 14 : 7) * S;
            GUI.color = new Color(1f, 0.15f, 0.1f);
            GUI.DrawTexture(new Rect(c.x + d.x * ppm - ds / 2, c.y - d.z * ppm - ds / 2, ds, ds), ringTex);
            GUI.color = Color.white;
        }
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(p.yaw, c);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(c.x - 9 * S, c.y - 10 * S, 18 * S, 18 * S), arrowTex);
        GUI.matrix = m;
        var ns = new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter };
        Text(new Rect(x, y + 2 * S, size, 24 * S), "N", ns, new Color(1, 1, 1, 0.8f));
    }

    void DrawFullMap(Player p)
    {
        float W = Screen.width, H = Screen.height;
        float size = Mathf.Min(W, H) * 0.82f;
        float x = W / 2 - size / 2, y = H / 2 - size / 2;
        Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
        Box(new Rect(x - 6 * S, y - 6 * S, size + 12 * S, size + 12 * S), new Color(0, 0, 0, 0.8f));
        GUI.DrawTexture(new Rect(x, y, size, size), world.mapTex);
        float ppm = size / (World.Half * 2);
        Vector2 ToMap(Vector3 w) => new Vector2(x + (w.x + World.Half) * ppm, y + (World.Half - w.z) * ppm);

        var ls = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter };
        Text(new Rect(x, y + size * 0.14f - 15 * S, size, 30 * S), "WALD", ls, new Color(1, 1, 1, 0.6f));
        Text(new Rect(x, y + size * 0.86f - 15 * S, size, 30 * S), "INDUSTRIE", ls, new Color(1, 1, 1, 0.6f));
        Text(new Rect(x + size * 0.36f, y + size / 2 - 15 * S, size, 30 * S), "WÜSTE", ls, new Color(1, 1, 1, 0.6f));
        Text(new Rect(x - size * 0.36f, y + size / 2 - 15 * S, size, 30 * S), "FARM", ls, new Color(1, 1, 1, 0.6f));

        for (int i = 0; i < world.weaponSpots.Count; i++)
        {
            var mp = ToMap(world.weaponSpots[i]);
            var d = Weapons.Defs[world.weaponIds[i]];
            float s = 18 * S;
            GUI.color = d.glow;
            GUI.DrawTexture(new Rect(mp.x - s / 2, mp.y - s / 2, s, s), ringTex);
            GUI.color = Color.white;
            var ws = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter };
            Text(new Rect(mp.x - 150 * S, mp.y + 10 * S, 300 * S, 26 * S), d.name, ws, d.glow);
        }
        foreach (var e in Enemy.All)
        {
            var mp = ToMap(e.transform.position);
            float s = (e.type == EType.Boss ? 14 : 7) * S;
            GUI.color = new Color(1f, 0.15f, 0.1f);
            GUI.DrawTexture(new Rect(mp.x - s / 2, mp.y - s / 2, s, s), ringTex);
            GUI.color = Color.white;
        }
        var pm = ToMap(p.transform.position);
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(p.yaw, pm);
        GUI.DrawTexture(new Rect(pm.x - 12 * S, pm.y - 13 * S, 24 * S, 24 * S), arrowTex);
        GUI.matrix = m;
        var ts = new GUIStyle(sMed) { alignment = TextAnchor.UpperCenter };
        Text(new Rect(0, y - 46 * S, W, 40 * S), "KARTE", ts, Color.white);
    }

    void DrawFeed(float W)
    {
        float y = 20 * S;
        var st = new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight };
        for (int i = feed.Count - 1; i >= 0; i--)
        {
            float age = Time.unscaledTime - feed[i].time;
            if (age > 5f) continue;
            float a = age > 4f ? 1f - (age - 4f) : 1f;
            Color c = feed[i].color; c.a = a;
            Text(new Rect(W - 720 * S, y, 700 * S, 30 * S), feed[i].text, st, c);
            y += 30 * S;
        }
    }
}
