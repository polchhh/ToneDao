using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SunFallController : MonoBehaviour
{

    public bool autoPlay = true;
    public float startDelay = 1.0f;

    public Transform sunObject;
    public Transform sunLandingPoint;
    public float sunFallDuration = 2.5f;
    public AnimationCurve sunFallCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public ParticleSystem impactEffect;
    public float hitStopDuration = 0.15f;
    public float shakeStrength = 0.4f;
    public float shakeDuration = 0.6f;

    public float pauseBeforeFade = 0.5f;
    public float pauseAfterFade = 1.5f;

    public PlayerController player;

    [System.Serializable]
    public class SubtitleLine
    {
        [TextArea(2, 4)]
        public string text;
        public float holdDuration = 3f;
        public float fadeDuration = 0.7f;
    }

    public List<SubtitleLine> subtitles = new()
    {
        new SubtitleLine { text = "В начале времен существовала великая богиня Нюйва –\nмать всего живого, хранительница мироздания", holdDuration = 3.5f },
        new SubtitleLine { text = "Из желтой речной глины она лепила людей,\nвдыхая в каждого жизнь своими руками", holdDuration = 3.5f },
        new SubtitleLine { text = "Но в миг, когда был создан первый человек,\nнебо содрогнулось...", holdDuration = 3.0f },
        new SubtitleLine { text = "Солнце сорвалось с небосвода\nи разлетелось на осколки", holdDuration = 0f },
        new SubtitleLine { text = "Мир погрузился в холод и серость.\nСвет угас.", holdDuration = 3.0f },
        new SubtitleLine { text = "Ты – Ни'эр,\nЕдинственный, кто видел это", holdDuration = 3.5f },
        new SubtitleLine { text = "Верни солнцу его осколки", holdDuration = 3.5f },
    };

    public Color subtitleColor = new Color(1f, 0.96f, 0.85f, 1f);
    public Color subtitleShadow = new Color(0f, 0f, 0f, 0.9f);
    public Color subtitleBgColor = new Color(0f, 0f, 0f, 0.45f);
    [Range(0.02f, 0.06f)]
    public float fontSizeRatio = 0.032f;
    [Range(0.05f, 0.25f)]
    public float bottomRatio = 0.12f;
    [Range(0.4f, 0.9f)]
    public float widthRatio = 0.65f;

    private Vector3 _sunStartPos;
    private Transform _mainCamTransform;
    private bool _played = false;
    private VoiceVisualizer _voiceViz;

    private string _currentText = "";
    private float _currentAlpha = 0f;

    private GUIStyle _subStyle;
    private GUIStyle _shadowStyle;
    private Texture2D _whiteTex;
    private bool _stylesReady;

    private void Start()
    {
        if (sunObject != null)
            _sunStartPos = sunObject.position;

        _mainCamTransform = Camera.main?.transform;

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        _voiceViz = FindFirstObjectByType<VoiceVisualizer>();

        bool isNewGame = GameManager.Instance == null || GameManager.Instance.IsNewGame;

        if (autoPlay && isNewGame)
            StartCoroutine(RunCutscene());
        else if (!isNewGame)
            SkipToGameplay();
    }

    public void PlayCutscene()
    {
        if (!_played) StartCoroutine(RunCutscene());
    }

    private void SkipToGameplay()
    {
        _played = true;

        if (sunObject != null) sunObject.gameObject.SetActive(false);

        var effects = FindObjectsByType<SunFragmentEffect>(FindObjectsSortMode.None);
        foreach (var fx in effects) { fx.MakeGray(); fx.BoostGlow(); }

        if (WorldColorController.Instance != null)
        {
            int total = GameManager.Instance != null ? GameManager.Instance.FragmentsTotal : 0;
            int collected = GameManager.Instance != null ? GameManager.Instance.FragmentsCollected : 0;
            float t = total > 0 ? (float)collected / total : 0f;
            WorldColorController.Instance.SetColorLevel(t);
        }

        player?.SetInteractMode(false);
        UIManager.Instance?.SetVisible(true);
        _voiceViz?.SetVisible(true);

    }

    private IEnumerator RunCutscene()
    {
        _played = true;

        UIManager.Instance?.SetVisible(false);
        _voiceViz?.SetVisible(false);
        player?.SetInteractMode(true);

        yield return new WaitForSeconds(startDelay);

        for (int i = 0; i <= 2 && i < subtitles.Count; i++)
            yield return StartCoroutine(ShowSubtitle(subtitles[i]));

        if (subtitles.Count > 3)
            StartCoroutine(ShowSubtitle(subtitles[3]));

        yield return StartCoroutine(AnimateSunFall());

        impactEffect?.Play();
        if (sunObject != null) sunObject.gameObject.SetActive(false);

        if (hitStopDuration > 0f)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitStopDuration);
            Time.timeScale = 1f;
        }

        if (shakeDuration > 0f && _mainCamTransform != null)
            StartCoroutine(ShakeCamera());

        yield return new WaitForSeconds(pauseBeforeFade);

        var effects = FindObjectsByType<SunFragmentEffect>(FindObjectsSortMode.None);
        foreach (var fx in effects) { fx.MakeGray(); fx.BoostGlow(); }
        WorldColorController.Instance?.TriggerSunFall();

        for (int i = 4; i < subtitles.Count; i++)
            yield return StartCoroutine(ShowSubtitle(subtitles[i]));

        yield return new WaitForSeconds(pauseAfterFade);

        yield return StartCoroutine(FadeSubtitle("", 0f, 0.5f));

        player?.SetInteractMode(false);
        UIManager.Instance?.SetVisible(true);
        _voiceViz?.SetVisible(true);

        TutorialManager.Instance?.StartTutorial();
    }

    private IEnumerator ShowSubtitle(SubtitleLine line)
    {

        yield return StartCoroutine(FadeSubtitle(line.text, 1f, line.fadeDuration));

        if (line.holdDuration > 0f)
            yield return new WaitForSeconds(line.holdDuration);

        yield return StartCoroutine(FadeSubtitle(line.text, 0f, line.fadeDuration));
    }

    private IEnumerator FadeSubtitle(string text, float targetAlpha, float duration)
    {
        _currentText = text;
        float startAlpha = _currentAlpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        _currentAlpha = targetAlpha;
        if (targetAlpha <= 0f) _currentText = "";
    }

    private IEnumerator AnimateSunFall()
    {
        if (sunObject == null || sunLandingPoint == null)
        {
            yield return new WaitForSeconds(sunFallDuration);
            yield break;
        }

        Vector3 from = _sunStartPos;
        Vector3 to = sunLandingPoint.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / sunFallDuration;
            sunObject.position = Vector3.Lerp(from, to,
                sunFallCurve.Evaluate(Mathf.Clamp01(t)));
            yield return null;
        }

        sunObject.position = to;
    }

    private IEnumerator ShakeCamera()
    {
        Vector3 originalPos = _mainCamTransform.localPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float s = Mathf.Lerp(shakeStrength, 0f, elapsed / shakeDuration);
            _mainCamTransform.localPosition = originalPos + Random.insideUnitSphere * s;
            elapsed += Time.deltaTime;
            yield return null;
        }

        _mainCamTransform.localPosition = originalPos;
    }

    private void OnGUI()
    {
        if (_currentAlpha <= 0f || string.IsNullOrEmpty(_currentText)) return;

        EnsureStyles();

        int fontSize = Mathf.RoundToInt(Screen.height * fontSizeRatio);
        float w = Screen.width * widthRatio;
        float h = fontSize * 4f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - Screen.height * bottomRatio - h * 0.5f;
        float pad = fontSize * 0.8f;
        float shadow = Mathf.Max(2f, Screen.height * 0.002f);

        _subStyle.fontSize = fontSize;
        _shadowStyle.fontSize = fontSize;

        _shadowStyle.normal.textColor =
            new Color(0f, 0f, 0f, subtitleShadow.a * _currentAlpha);
        GUI.Label(new Rect(x + shadow, y + shadow, w, h), _currentText, _shadowStyle);

        _subStyle.normal.textColor = new Color(
            subtitleColor.r, subtitleColor.g, subtitleColor.b, _currentAlpha);
        GUI.Label(new Rect(x, y, w, h), _currentText, _subStyle);
    }

    private void EnsureStyles()
    {
        if (_stylesReady) return;

        _whiteTex = new Texture2D(1, 1);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();

        _subStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            fontStyle = FontStyle.Italic,
        };
        _shadowStyle = new GUIStyle(_subStyle);

        _stylesReady = true;
    }

    private void OnDestroy()
    {
        if (_whiteTex != null) Destroy(_whiteTex);
    }

    private void OnDrawGizmosSelected()
    {
        if (sunObject != null && sunLandingPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(sunObject.position, sunLandingPoint.position);
            Gizmos.DrawWireSphere(sunLandingPoint.position, 0.3f);
        }
    }
}
