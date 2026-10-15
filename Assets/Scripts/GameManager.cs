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
    static readonly float[] DiffHp = { 0.7f, 0.9f, 1.15f };
    static readonly float[] DiffDmg = { 0.45f, 0.75f, 1.1f };
    static readonly float[] DiffCount = { 0.7f, 0.9f, 1.15f };
    public float DmgTakenMul => DiffDmg[difficulty];

    // loadout chosen in the armory
    public static int Primary
    {
        get { int v = PlayerPrefs.GetInt("prim", Arsenal.Find("M4A1")); return Arsenal.Primaries.Contains(v) ? v : Arsenal.Find("M4A1"); }
        set { PlayerPrefs.SetInt("prim", value); PlayerPrefs.Save(); }
    }
    public static int Secondary
    {
        get { int v = PlayerPrefs.GetInt("sec", Arsenal.Find("P9")); return Arsenal.Secondaries.Contains(v) ? v : Arsenal.Find("P9"); }
        set { PlayerPrefs.SetInt("sec", value); PlayerPrefs.Save(); }
    }

    // weapon pickup prompt
    public Pickup PromptTarget { get; private set; }
    float promptTime = -1f;
    public void ShowPrompt(Pickup p)
    {
        if (PromptTarget != null && PromptTarget != p && promptTime == Time.frameCount && Player.I != null &&
            (PromptTarget.transform.position - Player.I.transform.position).sqrMagnitude < (p.transform.position - Player.I.transform.position).sqrMagnitude)
            return;
        PromptTarget = p;
        promptTime = Time.frameCount;
    }

    public void DropWeapon(int id, Vector3 pos, float life = 60f)
    {
        Pickup.Create(PType.Weapon, pos + Vector3.up * 0.5f, id, life);
    }

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
    Texture2D vignetteTex, scopeTex, acogTex, arrowTex, btnTex, btnHoverTex, ringTex;
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
        Grenade.All.Clear();
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
        menuCam.farClipPlane = 1400f;
        menuCam.fieldOfView = 55f;
        cg.AddComponent<AudioListener>();
        cg.AddComponent<PostFX>();
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
            UpdateAmbience(dt);
        }
        else if (state == State.Paused)
        {
            if (Input.GetKeyDown(KeyCode.P)) Resume();
        }
    }

    float ambT = 5f, birdT = 3f;

    // Distant battle noise and birds for atmosphere.
    void UpdateAmbience(float dt)
    {
        ambT -= dt;
        if (ambT <= 0f)
        {
            ambT = Random.Range(3f, 11f);
            if (Random.value < 0.75f)
            {
                int n = Random.Range(1, 6);
                for (int i = 0; i < n; i++) Invoke(nameof(FarShot), i * Random.Range(0.08f, 0.2f));
            }
            else SFX.Play2D("farboom", Random.Range(0.25f, 0.5f), Random.Range(0.7f, 1f));
        }
        birdT -= dt;
        if (birdT <= 0f)
        {
            birdT = Random.Range(4f, 14f);
            if (!waveActive || Enemy.All.Count < 3) SFX.Play2D("bird", Random.Range(0.15f, 0.35f), Random.Range(0.85f, 1.2f));
        }
    }

    void FarShot() => SFX.Play2D("far", Random.Range(0.12f, 0.3f), Random.Range(0.85f, 1.1f));

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
        int cap = Mathf.Min(5 + wave, 16);
        if (queue.Count > 0 && spawnT <= 0f && Enemy.All.Count < cap)
        {
            spawnT = Mathf.Max(0.9f, 2.2f - wave * 0.08f);
            SpawnNext();
        }
        if (queue.Count == 0 && Enemy.All.Count == 0) WaveCleared();
    }

    void BeginWave()
    {
        wave++;
        waveActive = true;
        queue.Clear();
        int n = Mathf.Max(3, Mathf.RoundToInt((3 + wave * 2) * DiffCount[difficulty]));
        for (int i = 0; i < n; i++) queue.Add(PickType());
        for (int i = 0; i < queue.Count; i++)
        {
            int j = Random.Range(i, queue.Count);
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }
        if (wave % 5 == 0)
            for (int b = 0; b < wave / 5; b++) queue.Insert(Mathf.Max(0, queue.Count - 3 - b * 4), EType.Boss);
        spawnT = 0f;
        Announce(wave % 5 == 0 ? "WELLE " + wave + " - BOSSWELLE" : "WELLE " + wave, wave % 5 == 0 ? new Color(1f, 0.3f, 0.25f) : Color.white);
        SFX.Play2D("wave", 0.8f);
    }

    EType PickType()
    {
        float r = Random.value;
        if (wave >= 4 && r < Mathf.Min(0.06f + wave * 0.012f, 0.22f)) return EType.Brute;
        if (wave >= 2 && r < 0.5f) return EType.Soldier;
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
            Announce("JUGGERNAUT GESICHTET!", new Color(1f, 0.25f, 0.2f));
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
            var cp = World.CellCenter(c.x, c.y); var e = Enemy.Spawn(EType.Runner, new Vector3(cp.x, World.GroundY(cp) + 0.1f, cp.z), wave, DiffHp[difficulty]);
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
        return new Vector3(fallback.x, World.GroundY(fallback) + 0.1f, fallback.z);
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
        else if (Random.value < 0.35f)
        {
            float r = Random.value;
            Pickup.Create(r < 0.4f ? PType.Health : r < 0.85f ? PType.Ammo : PType.Armor, pos + e.transform.right, 0, 25f);
        }
        if (e.type != EType.Boss && Random.value < 0.4f)
            DropWeapon(e.weaponId, e.transform.position - e.transform.right * 0.6f, 35f);
        else if (e.type == EType.Boss)
            DropWeapon(Arsenal.Random(WCat.Launcher, WCat.Sniper), e.transform.position + Vector3.forward, 60f);
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
        foreach (var c in world.crateSpots)
            CreateMapPickup(c, PType.Weapon, -1);
    }

    static int RandomMapWeapon()
    {
        float r = Random.value;
        if (r < 0.08f) return Arsenal.Random(WCat.Launcher);
        if (r < 0.2f) return Arsenal.Random(WCat.Pistol);
        return Arsenal.Primaries[Random.Range(0, Arsenal.Primaries.Count)];
    }

    // w = -1 means "random weapon each time".
    void CreateMapPickup(Vector3 pos, PType t, int w)
    {
        int id = w < 0 ? RandomMapWeapon() : w;
        var pk = Pickup.Create(t, pos + Vector3.up * 0.9f, id);
        pk.onTaken = () => respawns.Add(new Respawn { time = Time.time + (t == PType.Weapon ? 40f : 25f), pos = pos, type = t, weapon = w });
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
                // clear lens with a soft dark edge, black housing outside
                float a = Mathf.SmoothStep(0.88f, 0.97f, d) * 0.7f + (d > 0.97f ? 1f : 0f);
                a = Mathf.Clamp01(a);
                bool line = (Mathf.Abs(dx) < 0.9f || Mathf.Abs(dy) < 0.9f) && d < 0.97f;
                bool thick = (Mathf.Abs(dx) < 3f || Mathf.Abs(dy) < 3f) && d > 0.5f;
                bool dot = false;
                for (int k = 1; k <= 5; k++)
                {
                    float m = k * 18f;
                    if ((Mathf.Abs(dx) < 2.2f && Mathf.Abs(Mathf.Abs(dy) - m) < 2.2f) || (Mathf.Abs(dy) < 2.2f && Mathf.Abs(Mathf.Abs(dx) - m) < 2.2f)) dot = true;
                }
                if (line || thick || dot) a = 1f;
                var c = new Color(0, 0, 0, a);
                if (d < 0.006f) c = new Color(1f, 0.1f, 0.1f, 1f);
                scopeTex.SetPixel(x, y, c);
            }
        scopeTex.Apply();

        acogTex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < SZ; y++)
            for (int x = 0; x < SZ; x++)
            {
                float dx = x - SZ / 2f + 0.5f, dy = y - SZ / 2f + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / (SZ / 2f);
                float a = Mathf.Clamp01(Mathf.SmoothStep(0.85f, 0.96f, d) * 0.7f + (d > 0.96f ? 1f : 0f));
                var c = new Color(0, 0, 0, a);
                // red chevron with apex at the center
                float cy = -dy;
                if (cy >= 0f && cy < 22f && Mathf.Abs(Mathf.Abs(dx) - cy * 0.8f) < 2.2f) c = new Color(1f, 0.15f, 0.05f, 1f);
                // post and bullet-drop stadia below
                if (Mathf.Abs(dx) < 1f && cy > 24f && cy < 150f) c = new Color(0, 0, 0, 1f);
                for (int k = 1; k <= 4; k++)
                    if (Mathf.Abs(cy - (30f + k * 25f)) < 1f && Mathf.Abs(dx) < 18f - k * 3f) c = new Color(0, 0, 0, 1f);
                acogTex.SetPixel(x, y, c);
            }
        acogTex.Apply();

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
            if (Btn(new Rect(x, y, bw, bh), "WAFFENKAMMER")) menuPage = 3;
            y += gap;
            if (Btn(new Rect(x, y, bw, bh), "STEUERUNG")) menuPage = 1;
            y += gap;
            if (Btn(new Rect(x, y, bw, bh), "EINSTELLUNGEN")) menuPage = 2;
            y += gap + 10 * S;
            Text(new Rect(x, y, 600 * S, 40 * S), "HIGHSCORE:  " + highscore, sMed, Gold);
            y += 40 * S;
            Text(new Rect(x, y, 600 * S, 30 * S), "Ausrüstung: " + Arsenal.All[Primary].name + "  +  " + Arsenal.All[Secondary].name, sSmall, new Color(1, 1, 1, 0.7f));
        }
        else if (menuPage == 1)
        {
            DrawControls(x, y);
            if (Btn(new Rect(x, H - 130 * S, bw, bh), "ZURÜCK")) menuPage = 0;
        }
        else if (menuPage == 3)
        {
            DrawArmory(x, y, bw);
            if (Btn(new Rect(x, H - 130 * S, bw, bh), "ZURÜCK")) menuPage = 0;
        }
        else
        {
            y = DrawSettings(x, y, bw);
            if (Btn(new Rect(x, H - 130 * S, bw, bh), "ZURÜCK")) menuPage = 0;
        }
        Text(new Rect(x, H - 45 * S, 600 * S, 30 * S), "Gemacht mit Unity  |  Maus & Tastatur", sSmall, new Color(1, 1, 1, 0.45f));
    }

    void DrawControls(float x, float y)
    {
        string[,] rows =
        {
            { "WASD", "Bewegen" }, { "Maus", "Zielen" }, { "Linksklick", "Schießen" }, { "Rechtsklick", "Visier / Zielfernrohr" },
            { "Shift", "Sprinten" }, { "Leertaste", "Springen" }, { "R", "Nachladen" }, { "1 / 2 / 3", "Haupt / Pistole / Werfer" },
            { "E", "Waffe aufnehmen" }, { "Q", "Letzte Waffe" }, { "C / Strg", "Ducken" }, { "G", "Granate werfen" },
            { "M / Tab", "Karte" }, { "Esc / P", "Pause" }
        };
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            Text(new Rect(x, y, 260 * S, 34 * S), rows[i, 0], sMed, Accent);
            Text(new Rect(x + 230 * S, y, 340 * S, 34 * S), rows[i, 1], sMed, Color.white);
            y += 37 * S;
        }
    }

    // Loadout selection: primary + pistol, with stats.
    void DrawArmory(float x, float y, float w)
    {
        float bh = 54 * S;
        Text(new Rect(x, y - 20 * S, w, 30 * S), "HAUPTWAFFE", sSmall, Accent);
        y += 14 * S;
        int prim = Arsenal.Primaries.IndexOf(Primary);
        if (Btn(new Rect(x, y, 60 * S, bh), " <")) Primary = Arsenal.Primaries[(prim - 1 + Arsenal.Primaries.Count) % Arsenal.Primaries.Count];
        if (Btn(new Rect(x + w - 60 * S, y, 60 * S, bh), " >")) Primary = Arsenal.Primaries[(prim + 1) % Arsenal.Primaries.Count];
        var pd = Arsenal.All[Primary];
        var cs = new GUIStyle(sMed) { alignment = TextAnchor.MiddleCenter };
        Text(new Rect(x + 60 * S, y, w - 120 * S, bh), pd.name, cs, Color.white);
        y += bh + 6 * S;
        Text(new Rect(x, y, w, 26 * S), pd.CatName + "  |  " + pd.FireName + "  |  " + SightName(pd.sight), sSmall, new Color(1, 1, 1, 0.65f));
        y += 30 * S;
        y = StatBars(x, y, w, pd);

        y += 16 * S;
        Text(new Rect(x, y, w, 30 * S), "PISTOLE", sSmall, Accent);
        y += 30 * S;
        int sec = Arsenal.Secondaries.IndexOf(Secondary);
        if (Btn(new Rect(x, y, 60 * S, bh), " <")) Secondary = Arsenal.Secondaries[(sec - 1 + Arsenal.Secondaries.Count) % Arsenal.Secondaries.Count];
        if (Btn(new Rect(x + w - 60 * S, y, 60 * S, bh), " >")) Secondary = Arsenal.Secondaries[(sec + 1) % Arsenal.Secondaries.Count];
        var sd = Arsenal.All[Secondary];
        Text(new Rect(x + 60 * S, y, w - 120 * S, bh), sd.name, cs, Color.white);
        y += bh + 6 * S;
        y = StatBars(x, y, w, sd);
        y += 10 * S;
        Text(new Rect(x, y, w + 60 * S, 26 * S), Arsenal.All.Length + " Waffen - weitere liegen auf der Karte (E zum Aufnehmen)", sSmall, new Color(1, 1, 1, 0.5f));
    }

    static string SightName(Sight s) => s == Sight.Iron ? "Kimme & Korn" : s == Sight.RedDot ? "Rotpunkt" : s == Sight.Holo ? "Holo-Visier" : s == Sight.Acog ? "ACOG 4x" : "Zielfernrohr";

    float StatBars(float x, float y, float w, WeaponDef d)
    {
        float dmg = Mathf.Clamp01(d.dmg * d.pellets / 180f);
        float rate = Mathf.Clamp01(d.rate / 18f);
        float acc = Mathf.Clamp01(1f - d.spread / 0.07f);
        float mag = Mathf.Clamp01(d.mag / 100f);
        string[] names = { "Schaden", "Feuerrate", "Präzision", "Magazin" };
        float[] vals = { dmg, rate, acc, mag };
        for (int i = 0; i < 4; i++)
        {
            Text(new Rect(x, y, 160 * S, 24 * S), names[i], sSmall, new Color(1, 1, 1, 0.8f));
            Bar(new Rect(x + 150 * S, y + 7 * S, w - 150 * S, 10 * S), vals[i], Accent);
            y += 26 * S;
        }
        return y;
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

        if (wpn.Scoped && !p.dead)
        {
            // see-through scope: clear lens in the middle, black housing around it
            float sz = H * (wpn.Def.sight == Sight.Acog ? 0.9f : 1f);
            float sx = W / 2 - sz / 2, sy = H / 2 - sz / 2;
            GUI.DrawTexture(new Rect(sx, sy, sz, sz), wpn.Def.sight == Sight.Acog ? acogTex : scopeTex);
            Box(new Rect(0, 0, sx + 1, H), Color.black);
            Box(new Rect(sx + sz - 1, 0, W - sx - sz + 1, H), Color.black);
            if (sy > 0) { Box(new Rect(0, 0, W, sy + 1), Color.black); Box(new Rect(0, sy + sz - 1, W, H - sy - sz + 1), Color.black); }
        }
        else if (!p.dead && wpn.ads < 0.5f) DrawCrosshair(W, H, wpn);

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
        var g0 = wpn.Cur;
        var d0 = g0.d;
        float ax = W - 420 * S, ay = H - 200 * S;
        Box(new Rect(ax - 12 * S, ay - 12 * S, 402 * S, 182 * S), new Color(0, 0, 0, 0.45f));
        Text(new Rect(ax, ay - 4 * S, 380 * S, 30 * S), d0.name.ToUpper(), sSmall, Color.white);
        var rs = new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight };
        Text(new Rect(ax, ay - 4 * S, 378 * S, 30 * S), d0.FireName, rs, new Color(1, 1, 1, 0.55f));
        string reserveTxt = d0.reserve < 0 ? "--" : g0.reserve.ToString();
        var bigL = new GUIStyle(sBig) { alignment = TextAnchor.MiddleLeft, fontSize = (int)(54 * S) };
        Text(new Rect(ax, ay + 22 * S, 200 * S, 60 * S), g0.mag.ToString(), bigL, g0.mag == 0 ? new Color(1f, 0.3f, 0.3f) : Color.white);
        var midL = new GUIStyle(sMed) { alignment = TextAnchor.MiddleLeft };
        Text(new Rect(ax + (g0.mag >= 100 ? 120 : 92) * S, ay + 30 * S, 200 * S, 50 * S), "/ " + reserveTxt, midL, new Color(1, 1, 1, 0.6f));
        if (wpn.reloading)
        {
            Bar(new Rect(ax, ay + 86 * S, 376 * S, 6 * S), wpn.ReloadProgress, Accent);
            Text(new Rect(ax + 220 * S, ay + 40 * S, 200 * S, 30 * S), "NACHLADEN", sSmall, Accent);
        }
        else if (g0.mag == 0 && !wpn.HasReserve(g0))
            Text(new Rect(ax + 200 * S, ay + 40 * S, 200 * S, 30 * S), "KEINE MUNITION", sSmall, new Color(1f, 0.3f, 0.3f));
        string[] slotNames = { "HAUPT", "PISTOLE", "WERFER" };
        for (int i = 0; i < 3; i++)
        {
            var r = new Rect(ax + i * 126 * S, ay + 100 * S, 120 * S, 58 * S);
            var gs2 = wpn.slots[i];
            bool own = gs2 != null;
            Box(r, i == wpn.cur ? new Color(Accent.r, Accent.g, Accent.b, 0.9f) : own ? new Color(1, 1, 1, 0.15f) : new Color(1, 1, 1, 0.05f));
            var cs = new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter, fontSize = Mathf.Max(7, (int)(15 * S)) };
            Color tc = own ? (i == wpn.cur ? Color.black : Color.white) : new Color(1, 1, 1, 0.3f);
            Text(new Rect(r.x, r.y + 4 * S, r.width, 24 * S), (i + 1) + "  " + slotNames[i], cs, tc);
            Text(new Rect(r.x, r.y + 28 * S, r.width, 24 * S), own ? gs2.d.name : "—", cs, tc);
        }

        // pickup prompt
        if (PromptTarget != null && promptTime >= Time.frameCount - 1)
        {
            var pd = Arsenal.All[PromptTarget.weapon];
            var ps = new GUIStyle(sMed) { alignment = TextAnchor.MiddleCenter };
            Box(new Rect(W / 2 - 300 * S, H * 0.64f, 600 * S, 70 * S), new Color(0, 0, 0, 0.55f));
            Text(new Rect(W / 2 - 300 * S, H * 0.64f + 4 * S, 600 * S, 36 * S), "[E]  " + pd.name + " aufnehmen", ps, Color.white);
            var ss = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter };
            Text(new Rect(W / 2 - 300 * S, H * 0.64f + 38 * S, 600 * S, 28 * S), pd.CatName + "  |  " + SightName(pd.sight) + "  |  Magazin " + pd.mag, ss, new Color(1, 1, 1, 0.65f));
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
            Text(new Rect(bx, by, bwid, 30 * S), "JUGGERNAUT", cTop, new Color(1f, 0.3f, 0.25f));
            Bar(new Rect(bx, by + 34 * S, bwid, 16 * S), boss.hp / boss.maxHp, new Color(0.9f, 0.15f, 0.15f));
        }

        DrawMinimap(p);
        DrawFeed(W);

        // grenades: count and incoming warning
        var gs = new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight };
        Text(new Rect(W - 420 * S, H - 245 * S, 390 * S, 30 * S), "GRANATEN: " + wpn.grenades + "   [G]", gs, new Color(1, 1, 1, 0.75f));
        foreach (var g in Grenade.All)
        {
            if (g == null) continue;
            Vector3 d = g.transform.position - p.transform.position;
            if (d.magnitude > 10f) continue;
            var sp = p.cam.WorldToScreenPoint(g.transform.position);
            float gx, gy;
            if (sp.z > 0)
            {
                gx = Mathf.Clamp(sp.x, 60 * S, W - 60 * S);
                gy = Mathf.Clamp(H - sp.y, 60 * S, H - 60 * S);
            }
            else
            {
                d.y = 0;
                float ang = Vector3.SignedAngle(p.transform.forward, d, Vector3.up) * Mathf.Deg2Rad;
                gx = W / 2 + Mathf.Sin(ang) * H * 0.3f;
                gy = H / 2 - Mathf.Cos(ang) * H * 0.3f;
            }
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 12f);
            GUI.color = new Color(1f, 0.2f, 0.1f, pulse);
            GUI.DrawTexture(new Rect(gx - 22 * S, gy - 22 * S, 44 * S, 44 * S), ringTex);
            GUI.color = Color.white;
            var cs = new GUIStyle(sMed) { alignment = TextAnchor.MiddleCenter };
            Text(new Rect(gx - 22 * S, gy - 22 * S, 44 * S, 44 * S), "!", cs, Color.black);
            Text(new Rect(gx - 100 * S, gy + 24 * S, 200 * S, 30 * S), "GRANATE", new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter }, new Color(1f, 0.3f, 0.2f));
        }

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
                pk.type == PType.Ammo ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.6f, 0.2f);
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
            var d = Arsenal.All[world.weaponIds[i]];
            float s = 18 * S;
            var wc = new Color(1f, 0.55f, 0.2f);
            GUI.color = wc;
            GUI.DrawTexture(new Rect(mp.x - s / 2, mp.y - s / 2, s, s), ringTex);
            GUI.color = Color.white;
            var ws = new GUIStyle(sSmall) { alignment = TextAnchor.MiddleCenter };
            Text(new Rect(mp.x - 150 * S, mp.y + 10 * S, 300 * S, 26 * S), d.name, ws, wc);
        }
        foreach (var pk in Pickup.All)
        {
            if (pk == null || pk.type != PType.Weapon) continue;
            var mp = ToMap(pk.transform.position);
            float s = 9 * S;
            GUI.color = new Color(1f, 0.85f, 0.4f);
            GUI.DrawTexture(new Rect(mp.x - s / 2, mp.y - s / 2, s, s), ringTex);
            GUI.color = Color.white;
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
