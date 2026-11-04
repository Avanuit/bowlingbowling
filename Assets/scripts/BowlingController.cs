using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Lanzamiento con barra de fuerza, cámara lenta y máquina de pines
/// (rastrillo que barre todo al foso + pines nuevos que bajan desde la carcasa).
/// Va en el objeto "Sistema_Bolos". El HUD vive en BowlingHUD.
/// </summary>
public class BowlingController : MonoBehaviour
{
    [Header("Referencias")]
    public Rigidbody ball;
    public Transform pinsRoot;
    public Rigidbody rake;
    public CameraRig cameraRig;

    [Header("Lanzamiento")]
    public float startX = -0.15f;      // posición lateral de la bola (m)
    public float angle = 0.7f;         // ángulo respecto al eje de la pista (grados)
    public float spin = 0f;            // efecto sobre el eje de avance (rad/s)
    public float spinSign = 1f;        // invierte la dirección del efecto si lo necesitas
    public float minSpeed = 5f;        // velocidad con la barra al mínimo (m/s)
    public float maxSpeed = 11.5f;     // velocidad con la barra al máximo (m/s)
    public float powerBarSpeed = 0.9f; // subidas/bajadas por segundo de la barra

    [Header("Física (se aplica al iniciar)")]
    public float ballMass = 6.8f;          // 15 lb
    public float pinMass = 1.6f;           // reglamentario: 1.54 a 1.64 kg
    public float pinCenterOfMassY = 0.147f; // reglamentario: ~5.8 in sobre la base
    public float pinAngularDrag = 0.1f;
    public float pinFriction = 0.30f;
    public float pinBounciness = 0.30f;

    [Header("Cámara lenta")]
    public bool autoSlowMotion = true;
    public float slowMotionScale = 0.3f;

    [Header("Máquina de pines")]
    public float rakeDownY = 0.15f;       // altura del rastrillo al barrer
    public float rakeEndZ = 19.75f;       // hasta dónde empuja el rastrillo
    public float pinSinkDepth = 0.45f;    // cuánto se hunden los pines viejos en el foso
    public float pinSpawnHeight = 1.92f;  // altura desde la que bajan los pines nuevos (dentro de la carcasa)
    public float ballCarryHeight = 1.25f; // altura del retorno de la bola

    const float BallRadius = 0.109f;
    const float BallStartZ = -0.5f;
    const float HeadPinZ = 18.29f;
    const float BaseFixedDelta = 0.005f;

    enum State { Aiming, Rolling, Machine }
    State state;

    Rigidbody[] pins;
    Vector3[] pinPos;
    Quaternion[] pinRot;
    bool[] fallenFlags;
    TrailRenderer trail;
    LineRenderer aimLine;
    LineRenderer refLine;
    Vector3 rakeRest;
    bool rakeBusy;

    float rollTimer;
    float settleTimer;
    float power;
    float lockedPower;
    float powerStartTime;
    float bannerStart = -100f;
    int fallen = -1;
    bool manualSlow;

    // ------------------------------------------------------------
    // API de solo lectura para el HUD
    // ------------------------------------------------------------
    public bool IsAiming { get { return state == State.Aiming; } }
    public bool IsRolling { get { return state == State.Rolling; } }
    public bool IsMachine { get { return state == State.Machine; } }
    public float PowerShown { get { return state == State.Aiming ? power : lockedPower; } }
    public float SpeedShown { get { return Mathf.Lerp(minSpeed, maxSpeed, PowerShown); } }
    public int PinCount { get { return pins == null ? 0 : pins.Length; } }
    public bool IsStrike { get { return fallen == 10; } }

    public Vector3 PinRackPosition(int i)
    {
        return pinPos[i];
    }

    public bool PinDown(int i)
    {
        if (state == State.Aiming) return false;
        if (state == State.Rolling) return IsDown(pins[i].transform);
        return fallenFlags[i];
    }

    public int DisplayFallen
    {
        get
        {
            int n = 0;
            for (int i = 0; i < PinCount; i++) if (PinDown(i)) n++;
            return n;
        }
    }

    public string BannerText
    {
        get
        {
            if (fallen == 10) return "¡CHUZA!";
            if (fallen == 0) return "SIN PINES";
            return fallen + (fallen == 1 ? " PIN" : " PINES");
        }
    }

    public float BannerAlpha
    {
        get
        {
            if (state != State.Machine) return 0f;
            float t = Time.unscaledTime - bannerStart;
            if (t < 0f || t > 3.2f) return 0f;
            return Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((3.2f - t) / 0.6f);
        }
    }

    // ------------------------------------------------------------
    void Start()
    {
        Physics.defaultSolverIterations = 20;
        Physics.defaultSolverVelocityIterations = 8;
        Time.fixedDeltaTime = BaseFixedDelta;

        PhysicMaterial pinMat = new PhysicMaterial("PinRuntime");
        pinMat.dynamicFriction = pinFriction;
        pinMat.staticFriction = pinFriction + 0.1f;
        pinMat.bounciness = pinBounciness;
        pinMat.frictionCombine = PhysicMaterialCombine.Average;
        pinMat.bounceCombine = PhysicMaterialCombine.Average;

        pins = pinsRoot.GetComponentsInChildren<Rigidbody>();
        pinPos = new Vector3[pins.Length];
        pinRot = new Quaternion[pins.Length];
        fallenFlags = new bool[pins.Length];
        for (int i = 0; i < pins.Length; i++)
        {
            Rigidbody p = pins[i];
            pinPos[i] = p.transform.position;
            pinRot[i] = p.transform.rotation;
            ConfigurePin(p);
            Collider[] cols = p.GetComponentsInChildren<Collider>();
            for (int c = 0; c < cols.Length; c++) cols[c].sharedMaterial = pinMat;
        }

        ball.mass = ballMass;
        ball.maxAngularVelocity = 300f;
        ball.solverIterations = 20;
        ball.solverVelocityIterations = 8;
        trail = ball.GetComponent<TrailRenderer>();

        rakeRest = rake.position;

        aimLine = MakeLine("Linea_Direccion", new Color(1f, 0.85f, 0.1f, 1f), 0.025f);
        refLine = MakeLine("Linea_Referencia", new Color(1f, 1f, 1f, 0.6f), 0.01f);

        BeginAiming();
    }

    void ConfigurePin(Rigidbody p)
    {
        p.mass = pinMass;
        p.centerOfMass = new Vector3(0f, pinCenterOfMassY, 0f);
        p.angularDrag = pinAngularDrag;
        p.sleepThreshold = 0.005f;
        p.maxDepenetrationVelocity = 10f;
        p.solverIterations = 20;
        p.solverVelocityIterations = 8;
    }

    LineRenderer MakeLine(string lineName, Color c, float width)
    {
        GameObject go = new GameObject(lineName);
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = c;
        lr.endColor = c;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    void Update()
    {
        if (state == State.Aiming)
        {
            HandleAimInput(Time.unscaledDeltaTime);
            power = Mathf.PingPong((Time.unscaledTime - powerStartTime) * powerBarSpeed, 1f);
            UpdateAimVisuals();
            if (Input.GetKeyDown(KeyCode.Space)) Launch();
        }
        else if (state == State.Rolling)
        {
            rollTimer += Time.deltaTime;

            bool finish = false;
            if (Input.GetKeyDown(KeyCode.R))
            {
                finish = true;
            }
            else
            {
                if (rollTimer > 1.5f && ReadyToFinish()) settleTimer += Time.deltaTime;
                else settleTimer = 0f;

                if (settleTimer > 0.8f) finish = true;
                if (rollTimer > 14f) finish = true;
            }
            if (finish) FinishRoll();
        }

        if (Input.GetKeyDown(KeyCode.T)) manualSlow = !manualSlow;
        UpdateTimeScale();
    }

    void HandleAimInput(float dt)
    {
        float h = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        startX = Mathf.Clamp(startX + h * 0.3f * dt, -0.45f, 0.45f);

        float a = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
        angle = Mathf.Clamp(angle + a * 2f * dt, -6f, 6f);

        float sp = (Input.GetKey(KeyCode.X) ? 1f : 0f) - (Input.GetKey(KeyCode.Z) ? 1f : 0f);
        spin = Mathf.Clamp(spin + sp * 6f * dt, -12f, 12f);

        ball.transform.position = new Vector3(startX, BallRadius + 0.002f, BallStartZ);
    }

    void UpdateAimVisuals()
    {
        Vector3 p = ball.transform.position;
        Vector3 p0 = new Vector3(p.x, 0.03f, p.z);
        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
        aimLine.SetPosition(0, p0);
        aimLine.SetPosition(1, p0 + yaw * Vector3.forward * 8f);
        refLine.SetPosition(0, p0);
        refLine.SetPosition(1, p0 + Vector3.forward * 8f);
    }

    void BeginAiming()
    {
        state = State.Aiming;
        rollTimer = 0f;
        settleTimer = 0f;

        Freeze(ball);
        ball.transform.rotation = Quaternion.identity;
        ball.transform.position = new Vector3(startX, BallRadius + 0.002f, BallStartZ);

        trail.emitting = false;
        trail.Clear();

        aimLine.enabled = true;
        refLine.enabled = true;
        powerStartTime = Time.unscaledTime;
        UpdateAimVisuals();
    }

    void Launch()
    {
        lockedPower = power;
        float speed = Mathf.Lerp(minSpeed, maxSpeed, lockedPower);

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
        Vector3 dir = yaw * Vector3.forward;
        Vector3 right = yaw * Vector3.right;

        ball.isKinematic = false;
        ball.velocity = dir * speed;
        ball.angularVelocity = right * (speed / BallRadius) + dir * (spin * spinSign);

        trail.Clear();
        trail.emitting = true;
        aimLine.enabled = false;
        refLine.enabled = false;

        // La cámara llega a los pines un poco antes que la bola
        if (cameraRig != null) cameraRig.ShowPins(Mathf.Clamp(17.5f / speed * 0.9f, 1.2f, 3.2f));

        state = State.Rolling;
        rollTimer = 0f;
        settleTimer = 0f;
        fallen = -1;
    }

    bool ReadyToFinish()
    {
        bool ballDone = ball.position.z > 19.0f || ball.velocity.sqrMagnitude < 0.01f;
        if (!ballDone) return false;

        for (int i = 0; i < pins.Length; i++)
        {
            Rigidbody p = pins[i];
            if (p.IsSleeping()) continue;
            if (p.velocity.sqrMagnitude > 0.0025f || p.angularVelocity.sqrMagnitude > 0.05f) return false;
        }
        return true;
    }

    static bool IsDown(Transform t)
    {
        bool tilted = Vector3.Dot(t.up, Vector3.up) < 0.8f;
        bool lost = t.position.y < -0.03f || t.position.z > 19.3f || Mathf.Abs(t.position.x) > 0.55f;
        return tilted || lost;
    }

    void FinishRoll()
    {
        state = State.Machine;
        fallen = 0;
        for (int i = 0; i < pins.Length; i++)
        {
            fallenFlags[i] = IsDown(pins[i].transform);
            if (fallenFlags[i]) fallen++;
        }
        bannerStart = Time.unscaledTime;
        StartCoroutine(PinsetterRoutine());
    }

    // ------------------------------------------------------------
    // Máquina de pines:
    // 1) el rastrillo baja y empuja TODOS los pines al foso,
    // 2) el foso se traga los pines viejos y la bola sube al retorno,
    // 3) la bola vuelve al punto de lanzamiento,
    // 4) pines nuevos bajan desde la carcasa y se posan en el triángulo,
    // 5) la cámara regresa a la vista de lanzamiento.
    // ------------------------------------------------------------
    IEnumerator PinsetterRoutine()
    {
        int n = pins.Length;

        yield return new WaitForSeconds(1.0f);

        // 1) Rastrillo
        Vector3 r0 = rakeRest;
        yield return StartCoroutine(Tween(0.9f, true, s =>
            rake.MovePosition(new Vector3(0f, Mathf.Lerp(r0.y, rakeDownY, s), r0.z))));
        yield return StartCoroutine(Tween(3.0f, true, s =>
            rake.MovePosition(new Vector3(0f, rakeDownY, Mathf.Lerp(r0.z, rakeEndZ, s)))));
        yield return StartCoroutine(Tween(0.8f, true, s =>
            rake.MovePosition(new Vector3(0f, Mathf.Lerp(rakeDownY, r0.y, s), rakeEndZ))));

        StartCoroutine(RakeReturn());
        yield return new WaitForSeconds(0.6f);

        // 2) El foso se traga los pines viejos; la bola sube
        Vector3[] a = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Freeze(pins[i]);
            a[i] = pins[i].position;
        }
        Freeze(ball);
        trail.emitting = false;
        trail.Clear();
        Vector3 ball0 = ball.position;

        yield return StartCoroutine(Tween(1.0f, true, s =>
        {
            for (int i = 0; i < n; i++)
                pins[i].MovePosition(a[i] + Vector3.down * (pinSinkDepth * s));
            ball.MovePosition(new Vector3(ball0.x, Mathf.Lerp(ball0.y, ballCarryHeight, s), ball0.z));
        }));

        for (int i = 0; i < n; i++) pins[i].gameObject.SetActive(false);

        // 3) Retorno de la bola
        Vector3 ballTop = new Vector3(ball0.x, ballCarryHeight, ball0.z);
        Vector3 ballTarget = new Vector3(startX, ballCarryHeight, BallStartZ);
        Vector3 ballLow = new Vector3(startX, BallRadius + 0.002f, BallStartZ);
        yield return StartCoroutine(Tween(2.2f, true, s =>
            ball.MovePosition(Vector3.Lerp(ballTop, ballTarget, s))));

        while (rakeBusy) yield return null;

        // 4) Pines nuevos bajan desde la carcasa
        for (int i = 0; i < n; i++)
        {
            pins[i].transform.SetPositionAndRotation(pinPos[i] + Vector3.up * pinSpawnHeight, pinRot[i]);
            pins[i].gameObject.SetActive(true);
            ConfigurePin(pins[i]);
        }
        yield return StartCoroutine(Tween(1.8f, true, s =>
        {
            for (int i = 0; i < n; i++)
                pins[i].MovePosition(Vector3.Lerp(pinPos[i] + Vector3.up * pinSpawnHeight, pinPos[i] + Vector3.up * 0.003f, s));
            ball.MovePosition(Vector3.Lerp(ballTarget, ballLow, s));
        }));

        for (int i = 0; i < n; i++)
        {
            pins[i].isKinematic = false;
            pins[i].velocity = Vector3.zero;
            pins[i].angularVelocity = Vector3.zero;
            pins[i].WakeUp();
        }

        // 5) Volver a la cámara de lanzamiento
        yield return new WaitForSeconds(0.4f);
        if (cameraRig != null)
        {
            cameraRig.ShowThrow(2.0f);
            while (!cameraRig.AtThrow) yield return null;
        }
        yield return new WaitForSeconds(0.2f);

        BeginAiming();
    }

    IEnumerator RakeReturn()
    {
        rakeBusy = true;
        Vector3 from = new Vector3(0f, rakeRest.y, rakeEndZ);
        Vector3 to = rakeRest;
        yield return StartCoroutine(Tween(1.8f, true, s => rake.MovePosition(Vector3.Lerp(from, to, s))));
        rakeBusy = false;
    }

    IEnumerator Tween(float duration, bool smooth, Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            step(smooth ? k * k * (3f - 2f * k) : k);
        }
        step(1f);
    }

    static void Freeze(Rigidbody rb)
    {
        if (!rb.isKinematic)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;
    }

    void UpdateTimeScale()
    {
        float target = 1f;
        if (state == State.Rolling)
        {
            if (manualSlow)
            {
                target = 0.25f;
            }
            else if (autoSlowMotion)
            {
                float z = ball.position.z;
                if (z > HeadPinZ - 1.6f && z < HeadPinZ + 1.2f) target = slowMotionScale;
            }
        }

        if (!Mathf.Approximately(Time.timeScale, target))
        {
            Time.timeScale = target;
            Time.fixedDeltaTime = BaseFixedDelta * target;
        }
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
