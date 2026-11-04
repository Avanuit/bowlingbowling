using UnityEngine;

/// <summary>
/// HUD visual (sin tablas de texto):
///  - Arriba al centro: los 10 pines como círculos que se apagan al caer.
///  - Abajo a la izquierda (al apuntar): minimapa de la pista con la bola, la mira y el efecto.
///  - A la derecha: barra de fuerza.
///  - Centro: banner con el resultado (CHUZA o cantidad de pines).
///  - Abajo: teclas disponibles en cada momento.
/// Va en "Sistema_Bolos" junto al BowlingController.
/// </summary>
public class BowlingHUD : MonoBehaviour
{
    public BowlingController controller;

    const float HeadPinZ = 18.29f;

    GUIStyle panelStyle;
    GUIStyle labelStyle;
    GUIStyle boldStyle;
    GUIStyle keyStyle;
    GUIStyle hintStyle;
    GUIStyle bannerStyle;
    Texture2D roundTex;
    Texture2D circleTex;

    void Awake()
    {
        if (controller == null) controller = GetComponent<BowlingController>();
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        if (controller == null || controller.PinCount == 0) return;

        EnsureAssets();
        float u = Mathf.Max(0.55f, Screen.height / 1080f);
        ApplySizes(u);

        DrawPinsPanel(u);
        if (controller.IsAiming) DrawAimPanel(u);
        if (!controller.IsMachine) DrawPowerBar(u);
        DrawBanner(u);
        DrawHints(u);
    }

    // ------------------------------------------------------------
    // Panel de pines: el triángulo de 10 pines, cada uno se apaga al caer
    // ------------------------------------------------------------
    void DrawPinsPanel(float u)
    {
        float pw = 270f * u;
        float ph = 215f * u;
        float px = (Screen.width - pw) * 0.5f;
        float py = 20f * u;
        Panel(new Rect(px, py, pw, ph), 0.6f);

        float cx = px + pw * 0.5f;
        float rowSp = 40f * u;
        float colSp = 46f * u;
        float headY = py + 154f * u;

        for (int i = 0; i < controller.PinCount; i++)
        {
            Vector3 rp = controller.PinRackPosition(i);
            int row = Mathf.RoundToInt((rp.z - HeadPinZ) / 0.264f);
            Vector2 c = new Vector2(cx + rp.x / 0.305f * colSp, headY - row * rowSp);
            bool down = controller.PinDown(i);
            Dot(c, down ? 22f * u : 32f * u,
                down ? new Color(0.30f, 0.33f, 0.40f, 0.85f) : Color.white);
        }

        int n = controller.DisplayFallen;
        Text(new Rect(px, py + 176f * u, pw, 30f * u), n + (n == 1 ? " derribado" : " derribados"),
            labelStyle, new Color(1f, 1f, 1f, 0.9f));
    }

    // ------------------------------------------------------------
    // Panel de puntería: minimapa de la pista visto desde arriba
    // ------------------------------------------------------------
    void DrawAimPanel(float u)
    {
        float pw = 190f * u;
        float ph = 450f * u;
        float px = 24f * u;
        float py = Screen.height - ph - 24f * u;
        Panel(new Rect(px, py, pw, ph), 0.6f);

        float lw = 76f * u;
        float lh = 270f * u;
        float gw = 12f * u;
        Rect lane = new Rect(px + (pw - lw) * 0.5f, py + 22f * u, lw, lh);

        // canales, pista y línea de falta
        Color gutter = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        Fill(new Rect(lane.x - gw, lane.y, gw, lane.height), gutter);
        Fill(new Rect(lane.xMax, lane.y, gw, lane.height), gutter);
        Fill(lane, new Color(0.78f, 0.60f, 0.38f, 0.95f));
        Fill(new Rect(lane.x, lane.yMax - 3f * u, lane.width, 3f * u), new Color(0.75f, 0.1f, 0.1f, 1f));

        // marcas de puntería
        for (int i = -3; i <= 3; i++)
            Dot(MapLane(lane, i * 0.15f, 4.5f), 5f * u, new Color(0.15f, 0.09f, 0.05f, 0.7f));

        // pines
        for (int i = 0; i < controller.PinCount; i++)
        {
            Vector3 rp = controller.PinRackPosition(i);
            Dot(MapLane(lane, rp.x, rp.z), 7f * u, Color.white);
        }

        // referencia recta, mira y bola
        Vector2 ballP = MapLane(lane, controller.startX, 0.4f);
        Line(ballP, MapLane(lane, controller.startX, HeadPinZ), 1.5f * u, new Color(1f, 1f, 1f, 0.45f));

        float xEnd = controller.startX + Mathf.Tan(controller.angle * Mathf.Deg2Rad) * HeadPinZ;
        xEnd = Mathf.Clamp(xEnd, -0.76f, 0.76f);
        Vector2 endP = MapLane(lane, xEnd, HeadPinZ);
        Line(ballP, endP, 3f * u, new Color(1f, 0.85f, 0.1f, 1f));
        Dot(endP, 11f * u, new Color(1f, 0.85f, 0.1f, 1f));
        Dot(ballP, 16f * u, new Color(0.2f, 0.35f, 0.95f, 1f));

        // posición y ángulo
        float ty = lane.yMax + 14f * u;
        Text(new Rect(px, ty, pw, 26f * u),
            "Posición  " + (controller.startX * 100f).ToString("0") + " cm", labelStyle, Color.white);
        Text(new Rect(px, ty + 28f * u, pw, 26f * u),
            "Ángulo  " + controller.angle.ToString("0.0") + "°", labelStyle, Color.white);

        // efecto: barra centrada con perilla
        float ey = ty + 68f * u;
        Text(new Rect(px, ey, pw, 26f * u), "Efecto", labelStyle, new Color(1f, 1f, 1f, 0.75f));
        Rect track = new Rect(px + 28f * u, ey + 38f * u, pw - 56f * u, 10f * u);
        Fill(track, new Color(0.18f, 0.19f, 0.24f, 1f));
        float v = Mathf.Clamp(controller.spin / 12f, -1f, 1f);
        float midX = track.center.x;
        float knobX = midX + v * track.width * 0.5f;
        Fill(new Rect(Mathf.Min(midX, knobX), track.y, Mathf.Abs(knobX - midX), track.height),
            new Color(1f, 0.6f, 0.15f, 1f));
        Fill(new Rect(midX - 1f * u, track.y - 5f * u, 2f * u, track.height + 10f * u), new Color(1f, 1f, 1f, 0.6f));
        Dot(new Vector2(knobX, track.center.y), 18f * u, Color.white);
    }

    // ------------------------------------------------------------
    // Barra de fuerza (derecha)
    // ------------------------------------------------------------
    void DrawPowerBar(float u)
    {
        float pw = 84f * u;
        float ph = 620f * u;
        float px = Screen.width - pw - 36f * u;
        float py = (Screen.height - ph) * 0.5f;
        Panel(new Rect(px, py, pw, ph), 0.6f);

        float v = controller.PowerShown;
        Rect track = new Rect(px + pw * 0.5f - 16f * u, py + 56f * u, 32f * u, ph - 150f * u);
        Fill(track, new Color(0.15f, 0.16f, 0.2f, 1f));

        float fh = track.height * v;
        Color fc = Color.Lerp(new Color(0.2f, 0.85f, 0.3f), new Color(0.95f, 0.2f, 0.15f), v);
        Fill(new Rect(track.x, track.yMax - fh, track.width, fh), fc);

        for (int k = 1; k <= 3; k++)
            Fill(new Rect(track.x - 6f * u, track.yMax - track.height * k / 4f - 1f * u, track.width + 12f * u, 2f * u),
                new Color(1f, 1f, 1f, 0.45f));

        Fill(new Rect(track.x - 10f * u, track.yMax - fh - 2f * u, track.width + 20f * u, 4f * u), Color.white);

        Text(new Rect(px, py + 14f * u, pw, 28f * u), "FUERZA", boldStyle, new Color(1f, 1f, 1f, 0.85f));
        Text(new Rect(px, py + ph - 90f * u, pw, 40f * u), Mathf.RoundToInt(v * 100f) + "%", boldStyle, Color.white);
        Text(new Rect(px, py + ph - 50f * u, pw, 30f * u), controller.SpeedShown.ToString("0.0") + " m/s",
            labelStyle, new Color(1f, 1f, 1f, 0.7f));
    }

    // ------------------------------------------------------------
    // Banner de resultado
    // ------------------------------------------------------------
    void DrawBanner(float u)
    {
        float a = controller.BannerAlpha;
        if (a <= 0.01f) return;

        Rect r = new Rect(0f, Screen.height * 0.30f, Screen.width, 160f * u);
        string t = controller.BannerText;
        Color main = controller.IsStrike ? new Color(1f, 0.82f, 0.2f, a) : new Color(1f, 1f, 1f, a);
        Text(new Rect(r.x + 4f * u, r.y + 4f * u, r.width, r.height), t, bannerStyle, new Color(0f, 0f, 0f, a * 0.6f));
        Text(r, t, bannerStyle, main);
    }

    // ------------------------------------------------------------
    // Teclas disponibles (cambian según el momento)
    // ------------------------------------------------------------
    void DrawHints(float u)
    {
        string[][] hints;
        if (controller.IsAiming)
        {
            hints = new[]
            {
                new[] { "A  D", "Mover", "Mover" },
                new[] { "Q  E", "Ángulo", "Ángulo" },
                new[] { "Z  X", "Efecto", "Efecto" },
                new[] { "ESPACIO", "Lanzar", "Lanzar" },
                new[] { "B  N  L", "Materiales", "Materiales" }
            };
        }
        else if (controller.IsRolling)
        {
            hints = new[]
            {
                new[] { "T", "Cámara lenta", "Cámara lenta" },
                new[] { "R", "Acomodar ya", "Acomodar ya" }
            };
        }
        else
        {
            string dots = new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);
            hints = new[] { new[] { "", "Acomodando pines" + dots, "Acomodando pines..." } };
        }

        int n = hints.Length;
        float keyH = 44f * u;
        float gap = 34f * u;
        float keyGap = 10f * u;
        float[] keyW = new float[n];
        float[] labW = new float[n];
        float total = 0f;

        for (int i = 0; i < n; i++)
        {
            string key = hints[i][0];
            keyW[i] = key.Length == 0 ? 0f : Mathf.Max(44f * u, keyStyle.CalcSize(new GUIContent(key)).x + 28f * u);
            labW[i] = hintStyle.CalcSize(new GUIContent(hints[i][2])).x + 4f * u;
            total += keyW[i] + (key.Length > 0 ? keyGap : 0f) + labW[i];
            if (i < n - 1) total += gap;
        }

        float x = (Screen.width - total) * 0.5f;
        float y = Screen.height - 78f * u;
        Panel(new Rect(x - 24f * u, y - 10f * u, total + 48f * u, keyH + 20f * u), 0.55f);

        for (int i = 0; i < n; i++)
        {
            string key = hints[i][0];
            if (key.Length > 0)
            {
                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.92f, 0.93f, 0.96f, 0.96f);
                Rect kr = new Rect(x, y, keyW[i], keyH);
                GUI.Box(kr, GUIContent.none, panelStyle);
                GUI.backgroundColor = prevBg;
                GUI.Label(kr, key, keyStyle);
                x += keyW[i] + keyGap;
            }
            GUI.Label(new Rect(x, y, labW[i], keyH), hints[i][1], hintStyle);
            x += labW[i] + gap;
        }
    }

    // ------------------------------------------------------------
    // Utilidades de dibujo
    // ------------------------------------------------------------
    static Vector2 MapLane(Rect lane, float x, float z)
    {
        return new Vector2(lane.x + (x + 0.525f) / 1.05f * lane.width, lane.yMax - (z / 19f) * lane.height);
    }

    void Panel(Rect r, float alpha)
    {
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.04f, 0.05f, 0.08f, alpha);
        GUI.Box(r, GUIContent.none, panelStyle);
        GUI.backgroundColor = prev;
    }

    static void Fill(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    void Dot(Vector2 center, float diameter, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - diameter * 0.5f, center.y - diameter * 0.5f, diameter, diameter), circleTex);
        GUI.color = prev;
    }

    static void Line(Vector2 a, Vector2 b, float thickness, Color c)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.5f) return;
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        Matrix4x4 m = GUI.matrix;
        Color prev = GUI.color;
        GUI.color = c;
        GUIUtility.RotateAroundPivot(ang, a);
        GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
        GUI.matrix = m;
        GUI.color = prev;
    }

    static void Text(Rect r, string s, GUIStyle style, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.Label(r, s, style);
        GUI.color = prev;
    }

    void EnsureAssets()
    {
        if (roundTex == null) roundTex = MakeRounded(32, 12);
        if (circleTex == null) circleTex = MakeCircle(64);

        if (panelStyle == null)
        {
            panelStyle = new GUIStyle();
            panelStyle.normal.background = roundTex;
            panelStyle.border = new RectOffset(12, 12, 12, 12);

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.wordWrap = false;
            labelStyle.clipping = TextClipping.Overflow;
            labelStyle.normal.textColor = Color.white;

            boldStyle = new GUIStyle(labelStyle);
            boldStyle.fontStyle = FontStyle.Bold;

            keyStyle = new GUIStyle(boldStyle);
            keyStyle.normal.textColor = new Color(0.10f, 0.11f, 0.15f);

            hintStyle = new GUIStyle(labelStyle);
            hintStyle.alignment = TextAnchor.MiddleLeft;

            bannerStyle = new GUIStyle(boldStyle);
        }
    }

    void ApplySizes(float u)
    {
        labelStyle.fontSize = Mathf.RoundToInt(24f * u);
        boldStyle.fontSize = Mathf.RoundToInt(24f * u);
        keyStyle.fontSize = Mathf.RoundToInt(22f * u);
        hintStyle.fontSize = Mathf.RoundToInt(24f * u);
        bannerStyle.fontSize = Mathf.RoundToInt(120f * u);
    }

    static Texture2D MakeRounded(int size, int radius)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.hideFlags = HideFlags.HideAndDontSave;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f;
                float fy = y + 0.5f;
                float dx = Mathf.Max(Mathf.Max(radius - fx, fx - (size - radius)), 0f);
                float dy = Mathf.Max(Mathf.Max(radius - fy, fy - (size - radius)), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f));
            }
        }
        t.SetPixels(px);
        t.Apply();
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }

    static Texture2D MakeCircle(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.hideFlags = HideFlags.HideAndDontSave;
        Color[] px = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
            }
        }
        t.SetPixels(px);
        t.Apply();
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        return t;
    }
}
