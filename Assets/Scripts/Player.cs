using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player I;
    public static float Sensitivity = 1.6f;

    public CharacterController cc;
    public Camera cam;
    public Weapons weapons;
    public float hp = 100, maxHp = 100, armor = 0, maxArmor = 100, stamina = 1;
    public float yaw, pitch, recoil, hurtFlash;
    public bool dead;
    public float lastHitTime = -10f;
    public Vector3 lastHitFrom;
    public float targetFov = BaseFov;
    public bool Sprinting { get; private set; }
    public bool Moving { get; private set; }
    public bool Grounded => cc.isGrounded;
    public Vector3 Velocity => vel;
    public Vector3 EyePos => cam.transform.position;

    public const float BaseFov = 75f;
    float vy, shake, bobT, deathT;
    Vector3 vel;

    public static Player Create(Vector3 pos, float yaw)
    {
        var g = new GameObject("Player");
        g.transform.position = pos;
        var p = g.AddComponent<Player>();
        p.yaw = yaw;
        p.Init();
        return p;
    }

    void Init()
    {
        I = this;
        gameObject.layer = GA.LayerPlayer;
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0, 0.9f, 0);
        cc.stepOffset = 0.45f; cc.slopeLimit = 50f; cc.skinWidth = 0.06f;
        var camGo = new GameObject("PlayerCam");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0, 1.6f, 0);
        cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f; cam.fieldOfView = BaseFov;
        camGo.AddComponent<AudioListener>();
        weapons = gameObject.AddComponent<Weapons>();
        weapons.Init(this);
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (dead)
        {
            deathT += dt;
            float k = Mathf.Clamp01(deathT / 0.8f);
            cam.transform.localPosition = Vector3.Lerp(new Vector3(0, 1.6f, 0), new Vector3(0, 0.35f, 0), k);
            cam.transform.localRotation = Quaternion.Euler(Mathf.Lerp(pitch, -10f, k), 0, Mathf.Lerp(0, 35f, k));
            return;
        }

        if (GameManager.I.MouseActive)
        {
            float zoomK = cam.fieldOfView / BaseFov;
            yaw += Input.GetAxisRaw("Mouse X") * Sensitivity * zoomK;
            pitch -= Input.GetAxisRaw("Mouse Y") * Sensitivity * zoomK;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
        }
        transform.rotation = Quaternion.Euler(0, yaw, 0);

        var input = new Vector3(
            (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), 0,
            (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool wantSprint = Input.GetKey(KeyCode.LeftShift) && input.z > 0.1f && stamina > 0.02f && !weapons.Zoomed;
        Sprinting = wantSprint && (cc.isGrounded || Sprinting);
        stamina = Sprinting ? Mathf.Max(0, stamina - 0.2f * dt) : Mathf.Min(1, stamina + 0.16f * dt);

        float speed = Sprinting ? 9.5f : (weapons.Zoomed ? 3.6f : 6.2f);
        Vector3 target = transform.TransformDirection(input) * speed;
        float accel = cc.isGrounded ? 55f : 14f;
        vel = Vector3.MoveTowards(vel, target, accel * dt);

        if (cc.isGrounded)
        {
            if (vy < 0) vy = -2f;
            if (Input.GetKeyDown(KeyCode.Space)) vy = 7.2f;
        }
        vy -= 20f * dt;
        cc.Move((vel + Vector3.up * vy) * dt);
        if ((cc.collisionFlags & CollisionFlags.Above) != 0 && vy > 0) vy = 0;
        Moving = cc.isGrounded && new Vector2(vel.x, vel.z).sqrMagnitude > 2f;

        // camera
        recoil = Mathf.Lerp(recoil, 0f, dt * 9f);
        shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
        hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, dt * 1.4f);
        Vector3 off = Vector3.zero;
        if (Moving)
        {
            bobT += dt * (Sprinting ? 14f : 10f);
            off.y = Mathf.Sin(bobT) * (Sprinting ? 0.07f : 0.045f);
            off.x = Mathf.Cos(bobT * 0.5f) * 0.035f;
        }
        float sx = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * shake;
        float sy = (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * shake;
        cam.transform.localPosition = new Vector3(0, 1.6f, 0) + off;
        cam.transform.localRotation = Quaternion.Euler(pitch - recoil + sy * 6f, sx * 6f, 0);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, dt * 12f);

        if (transform.position.y < -20f) Teleport(World.I.playerSpawn);
    }

    public float BobOffset => Moving ? Mathf.Sin(bobT) : 0f;

    public void Teleport(Vector3 p)
    {
        cc.enabled = false;
        transform.position = p;
        cc.enabled = true;
        vy = 0; vel = Vector3.zero;
    }

    public void Damage(float d, Vector3 from)
    {
        if (dead || GameManager.I.state != GameManager.State.Playing) return;
        d *= GameManager.I.DmgTakenMul;
        if (armor > 0)
        {
            float absorb = Mathf.Min(armor, d * 0.6f);
            armor -= absorb; d -= absorb;
        }
        hp -= d;
        hurtFlash = Mathf.Min(1f, hurtFlash + 0.35f + d * 0.01f);
        shake = Mathf.Min(1.2f, shake + 0.25f + d * 0.015f);
        lastHitTime = Time.time;
        lastHitFrom = from;
        SFX.Play2D("hurt", 0.8f, Random.Range(0.9f, 1.1f));
        if (hp <= 0)
        {
            hp = 0;
            dead = true;
            weapons.OnDeath();
            GameManager.I.OnPlayerDied();
        }
    }

    public void AddShake(float s) => shake = Mathf.Min(1.5f, shake + s);
}
