using UnityEngine;

public class VoiceVisualizer : MonoBehaviour
{
    public int panelX = 10;
    public int panelY = 10;
    public int panelWidth = 320;

    [Range(1f, 5f)] public float lineWidth = 2f;
    [Range(30, 200)] public int historySize = 80;
    public bool showGrid = true;
    public bool showToneLabel = true;
    public Texture2D borderTexture = null;
    public int borderPadding = 12;
    public int marginLeft = 0;
    public int marginRight = 0;

    public Color panelBgColor = new Color(0f, 0f, 0f, 0.55f);
    public Color graphBgColor = new Color(0.04f, 0.04f, 0.1f, 0.95f);
    public Color voiceOnColor = new Color(0.2f, 1f, 0.45f, 1f);
    public Color voiceOffColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color labelTextColor = new Color(0.85f, 0.85f, 0.85f, 1f);

    private VoiceInputManager voiceManager;

    private float[] history;
    private bool[] voiced;
    private int head;
    private int filled;

    private string toneLabel = "—";
    private Color toneColor = Color.white;
    private float labelAlpha = 0f;
    private bool isVoiced = false;

    private float dispMin = -6f;
    private float dispMax = 6f;

    private static readonly Color[] TONE_COLORS = {
        Color.white,
        new Color(0.35f, 1f, 0.35f),
        new Color(0.35f, 0.75f, 1f),
        new Color(1f, 0.85f, 0.2f),
        new Color(1f, 0.35f, 0.35f),
    };
    private static readonly string[] TONE_NAMES =
        { "—", "Тон 1  ā  →", "Тон 2  á  ↗", "Тон 3  ǎ  ↘↗", "Тон 4  à  ↘" };

    private Texture2D whiteTex;
    private GUIStyle labelStyle;
    private GUIStyle toneStyle;
    private bool stylesReady;

    private void Start()
    {
        history = new float[historySize];
        voiced = new bool[historySize];

        voiceManager = FindFirstObjectByType<VoiceInputManager>();
        if (voiceManager == null)
        {
            Debug.LogWarning("[VoiceVisualizer] VoiceInputManager не найден.");
            return;
        }
        voiceManager.OnToneDetected += OnToneDetected;
        voiceManager.OnF0CurveUpdated += OnF0Updated;
    }

    private void OnDestroy()
    {
        if (voiceManager != null)
        {
            voiceManager.OnToneDetected -= OnToneDetected;
            voiceManager.OnF0CurveUpdated -= OnF0Updated;
        }
        if (whiteTex != null) Destroy(whiteTex);
    }

    private void Update()
    {
        if (labelAlpha > 0f) labelAlpha -= Time.deltaTime * 0.5f;
    }

    private void OnToneDetected(VoiceInputManager.ToneType tone)
    {
        int idx = (int)tone;
        toneLabel = TONE_NAMES[idx];
        toneColor = TONE_COLORS[idx];
        labelAlpha = 1f;
        isVoiced = (tone != VoiceInputManager.ToneType.None);
    }

    private void OnF0Updated(float[] curve)
    {
        if (curve == null || curve.Length == 0) return;

        float newVal = curve[curve.Length - 1];
        isVoiced = true;

        dispMin = Mathf.Lerp(dispMin, newVal - 4f, 0.04f);
        dispMax = Mathf.Lerp(dispMax, newVal + 4f, 0.04f);
        if (dispMax - dispMin < 6f) { dispMin -= 1f; dispMax += 1f; }

        history[head] = newVal;
        voiced[head] = true;
        head = (head + 1) % historySize;
        filled = Mathf.Min(filled + 1, historySize);
    }

    private void PushSilence()
    {
        voiced[head] = false;
        history[head] = 0f;
        head = (head + 1) % historySize;
        filled = Mathf.Min(filled + 1, historySize);
        isVoiced = false;
    }

    private bool _visible = true;

    public void SetVisible(bool value) => _visible = value;

    private void OnGUI()
    {
        if (!_visible) return;
        EnsureStyles();

        const int graphH = 70;
        const int headerH = 44;
        int totalH = headerH + graphH + 8;

        float px = panelX + marginLeft;
        float py = panelY;
        float pw = panelWidth - marginLeft - marginRight;

        int bp = borderPadding;
        float bx = panelX - bp;
        float by = panelY - bp;
        float bw = panelWidth + bp * 2;
        float bh = totalH + bp * 2;

        DrawRect(bx, by, bw, bh, panelBgColor);

        if (borderTexture != null)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(bx, by, bw, bh), borderTexture,
                            ScaleMode.StretchToFill, alphaBlend: true);
            GUI.color = Color.white;
        }

        DrawRect(px, py + 3, 10, 10, isVoiced ? voiceOnColor : voiceOffColor);
        GUI.color = Color.white;
        labelStyle.normal.textColor = labelTextColor;
        GUI.Label(new Rect(px + 14, py, 200, 18), isVoiced ? "Голос активен" : "Тишина", labelStyle);

        if (showToneLabel)
        {
            toneStyle.normal.textColor = new Color(toneColor.r, toneColor.g, toneColor.b,
                Mathf.Clamp01(labelAlpha));
            GUI.Label(new Rect(px, py + 20, pw, 22), toneLabel, toneStyle);
        }

        float gx = px, gy = py + headerH, gw = pw, gh = graphH;
        DrawRect(gx, gy, gw, gh, graphBgColor);

        DrawLine(new Vector2(gx, gy), new Vector2(gx + gw, gy), new Color(1,1,1,0.15f), 1);
        DrawLine(new Vector2(gx, gy + gh), new Vector2(gx + gw, gy + gh), new Color(1,1,1,0.15f), 1);
        DrawLine(new Vector2(gx, gy), new Vector2(gx, gy + gh), new Color(1,1,1,0.15f), 1);
        DrawLine(new Vector2(gx + gw, gy), new Vector2(gx + gw, gy + gh), new Color(1,1,1,0.15f), 1);

        if (showGrid)
        {
            for (int i = 1; i <= 3; i++)
                DrawLine(new Vector2(gx, gy + gh * i / 4f),
                         new Vector2(gx + gw, gy + gh * i / 4f),
                         new Color(1,1,1,0.07f), 1);
        }

        DrawHistory(gx, gy, gw, gh);

        GUI.color = Color.white;
    }

    private void DrawHistory(float gx, float gy, float gw, float gh)
    {
        if (filled < 2) return;

        float pad = 4f;
        float iw = gw - pad * 2f;
        float ih = gh - pad * 2f;

        float range = Mathf.Max(dispMax - dispMin, 4f);

        int count = Mathf.Min(filled, historySize);

        Vector2? prev = null;
        bool prevVoiced = false;

        for (int i = 0; i < count; i++)
        {

            int idx = (head - count + i + historySize) % historySize;

            float t = (float)i / (count - 1);
            float x = gx + pad + t * iw;

            bool v = voiced[idx];

            if (v)
            {
                float val = history[idx];
                float y = gy + pad + (1f - (val - dispMin) / range) * ih;
                y = Mathf.Clamp(y, gy + pad, gy + pad + ih);

                Vector2 cur = new Vector2(x, y);

                if (prev.HasValue && prevVoiced)
                {

                    float bright = Mathf.Lerp(0.35f, 1f, t);
                    Color c = new Color(toneColor.r * bright, toneColor.g * bright, toneColor.b * bright, 0.95f);
                    DrawLine(prev.Value, cur, c, lineWidth);
                }

                prev = cur;
                prevVoiced = true;
            }
            else
            {

                prev = null;
                prevVoiced = false;
            }
        }

        if (prev.HasValue)
            DrawRect(prev.Value.x - 3, prev.Value.y - 3, 6, 6, Color.white);
    }

    private void DrawLine(Vector2 p1, Vector2 p2, Color color, float width)
    {
        float length = Vector2.Distance(p1, p2);
        if (length < 0.5f) return;
        float angle = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;
        Matrix4x4 saved = GUI.matrix;
        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, p1);
        GUI.DrawTexture(new Rect(p1.x, p1.y - width * 0.5f, length, width), whiteTex);
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    private void DrawRect(float x, float y, float w, float h, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, w, h), whiteTex);
        GUI.color = Color.white;
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;
        whiteTex = new Texture2D(1, 1);
        whiteTex.SetPixel(0, 0, Color.white);
        whiteTex.Apply();
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        toneStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        stylesReady = true;
    }
}
