using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player I;
    public static float Sensitivity = 1.6f;

    public CharacterController cc;
    public Camera cam;
    public Weapons weapons;
    public PostFX post;
    public float hp = 100, maxHp = 100, armor = 50, maxArmor = 100, stamina = 1;
    public float yaw, pitch, recoil, hurtFlash, suppression;
    public bool dead;
    public float lastHitTime = -10f;
    public Vector3 lastHitFrom;
    public float targetFov = BaseFov;
    public bool Sprinting { get; private set; }
    public bool Moving { get; private set; }
    public bool Crouching { get; private set; }
    public bool Grounded => cc.isGrounded;
    public Vector3 Velocity => vel;
    public Vector3 EyePos => cam.transform.position;

    public const float BaseFov = 72f;
    const float StandEye = 1.62f, CrouchEye = 1.05f;
    float vy, shake, bobT, deathT, eyeH = StandEye, lastBobSign = 1f, regenDelay;
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
        cc.height = 1.8f; cc.radius = 0.38f; cc.center = new Vector3(0, 0.9f, 0);
        cc.stepOffset = 0.45f; cc.slopeLimit = 50f; cc.skinWidth = 0.06f;
        var camGo = new GameObject("PlayerCam");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0, StandEye, 0);
        cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.03f; cam.farClipPlane = 1400f; cam.fieldOfView = BaseFov;
        camGo.AddComponent<AudioListener>();
        post = camGo.AddComponent<PostFX>();
        weapons = gameObject.AddComponent<Weapons>();
        weapons.Init(this, GameManager.Primary, GameManager.Secondary);
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
            cam.transform.localPosition = Vector3.Lerp(new Vector3(0, eyeH, 0), new Vector3(0, 0.3f, 0), k * k);
            cam.transform.localRotation = Quaternion.Euler(Mathf.Lerp(pitch, -5f, k), 0, Mathf.Lerp(0, 70f, k));
            post.desat = Mathf.Lerp(post.desat, 0.9f, dt * 2f);
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

        bool wantCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        if (!wantCrouch && Crouching && Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.35f, Vector3.up, out _, 1.0f, GA.MaskWorld, QueryTriggerInteraction.Ignore))
            wantCrouch = true;
        Crouching = wantCrouch;
        cc.height = Crouching ? 1.2f : 1.8f;
        cc.center = new Vector3(0, cc.height / 2f, 0);
        eyeH = Mathf.Lerp(eyeH, Crouching ? CrouchEye : StandEye, dt * 10f);

        bool wantSprint = Input.GetKey(KeyCode.LeftShift) && input.z > 0.1f && stamina > 0.02f && !weapons.Zoomed && !Crouching;
        Sprinting = wantSprint && (cc.isGrounded || Sprinting);
        stamina = Sprinting ? Mathf.Max(0, stamina - 0.14f * dt) : Mathf.Min(1, stamina + 0.12f * dt);

        float speed = Sprinting ? 8.2f : Crouching ? 2.6f : (weapons.Zoomed ? 3f : 5f);
        Vector3 target = transform.TransformDirection(input) * speed;
        float accel = cc.isGrounded ? 40f : 6f;
        vel = Vector3.MoveTowards(vel, target, accel * dt);

        if (cc.isGrounded)
        {
            if (vy < 0) vy = -2f;
            if (Input.GetKeyDown(KeyCode.Space) && !Crouching) { vy = 5.6f; stamina = Mathf.Max(0, stamina - 0.08f); }
        }
        vy -= 18f * dt;
        bool wasGrounded = cc.isGrounded;
        float fallSpeed = vy;
        cc.Move((vel + Vector3.up * vy) * dt);
        if (!wasGrounded && cc.isGrounded && fallSpeed < -6f)
        {
            SFX.Play2D("step", 1f, 0.7f);
            AddShake(0.25f);
            if (fallSpeed < -14f) Damage((-fallSpeed - 14f) * 6f, transform.position);
        }
        if ((cc.collisionFlags & CollisionFlags.Above) != 0 && vy > 0) vy = 0;
        Moving = cc.isGrounded && new Vector2(vel.x, vel.z).sqrMagnitude > 1f;

        // health regeneration after a short break
        regenDelay -= dt;
        if (regenDelay <= 0f && hp < maxHp) hp = Mathf.Min(maxHp, hp + 14f * dt);

        // camera
        recoil = Mathf.Lerp(recoil, 0f, dt * 9f);
        shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
        hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, dt * 1.4f);
        suppression = Mathf.MoveTowards(suppression, 0f, dt * 0.6f);
        Vector3 off = Vector3.zero;
        if (Moving)
        {
            float rate = Sprinting ? 13f : Crouching ? 6.5f : 9f;
            bobT += dt * rate;
            float amp = Sprinting ? 0.06f : Crouching ? 0.02f : 0.035f;
            off.y = Mathf.Sin(bobT) * amp;
            off.x = Mathf.Cos(bobT * 0.5f) * amp * 0.8f;
            float sgn = Mathf.Sign(Mathf.Sin(bobT));
            if (sgn < 0 && lastBobSign > 0) SFX.Play2D("step", Sprinting ? 0.5f : Crouching ? 0.15f : 0.3f, Random.Range(0.85f, 1.15f));
            lastBobSign = sgn;
        }
        float sx = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * shake;
        float sy = (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * shake;
        float roll = -input.x * 1.2f;
        cam.transform.localPosition = new Vector3(0, eyeH, 0) + off;
        cam.transform.localRotation = Quaternion.Euler(pitch - recoil + sy * 6f, sx * 6f, roll + sx * 4f);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov + (Sprinting ? 5f : 0f), dt * 12f);

        float lowHp = 1f - Mathf.Clamp01(hp / 45f);
        post.desat = Mathf.Lerp(post.desat, Mathf.Max(lowHp * 0.8f, suppression * 0.5f), dt * 4f);
        post.exposure = Mathf.Lerp(post.exposure, 1.15f - suppression * 0.15f, dt * 4f);

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

    public void Suppress(float amount)
    {
        suppression = Mathf.Min(1f, suppression + amount);
        AddShake(amount * 0.3f);
    }

    public void Damage(float d, Vector3 from)
    {
        if (dead || GameManager.I.state != GameManager.State.Playing) return;
        d *= GameManager.I.DmgTakenMul;
        if (armor > 0)
        {
            float absorb = Mathf.Min(armor, d * 0.7f);
            armor -= absorb; d -= absorb;
        }
        hp -= d;
        regenDelay = 4f;
        hurtFlash = Mathf.Min(1f, hurtFlash + 0.3f + d * 0.012f);
        shake = Mathf.Min(1.2f, shake + 0.2f + d * 0.015f);
        suppression = Mathf.Min(1f, suppression + 0.3f);
        lastHitTime = Time.time;
        lastHitFrom = from;
        SFX.Play2D("flesh", 0.8f, Random.Range(0.85f, 1.05f));
        SFX.Play2D("hurt", 0.5f, Random.Range(0.9f, 1.1f));
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
