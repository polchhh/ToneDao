using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndingCutsceneController : MonoBehaviour
{

    public SunReturnController sunReturn;

    public Transform pedestal;
    public float playerMoveDuration = 2.5f;
    public AnimationCurve playerMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public Transform nyuwaTransform;
    public Transform nyuwaWalkTarget;
    public Animator nyuwaAnimator;
    public string nyuwaSpeedParam = "Speed";
    public string nyuwaOfferParam = "Offer";
    public float nyuwaWalkDuration = 3.5f;
    public float offerAnimDuration = 2.0f;

    [TextArea(2, 4)]
    public string[] subtitleLines = new[]
    {
        "Ни'эр, ты собрал все осколки солнца",
        "Свет снова коснулся мира",
        "Прими этот дар — жемчужину знаний"
    };
    public float lineDuration = 3.5f;
    public float subtitleFade = 0.7f;

    public Color subtitleColor = new Color(1f, 0.96f, 0.85f, 1f);
    public Color subtitleShadow = new Color(0f, 0f, 0f, 0.9f);
    public Color subtitleBgColor = new Color(0f, 0f, 0f, 0.45f);
    [Range(0.02f, 0.06f)] public float fontSizeRatio = 0.032f;
    [Range(0.05f, 0.25f)] public float bottomRatio = 0.12f;
    [Range(0.4f, 0.9f)] public float widthRatio = 0.65f;

    public float fadeToBlackDuration = 2.5f;
    public float blackHoldDuration = 1.2f;

    public float endScreenFadeDuration = 1.5f;

    public float delayAfterSunReturn = 1.5f;

    public string mainMenuScene = "MainMenu";

    public Image fadeOverlay;

    public CanvasGroup endScreenGroup;
    public TMP_Text gameTitleText;
    public TMP_Text completionText;
    public Button exitButton;

    private bool _triggered = false;

    private string _currentSubtitle = "";
    private float _currentAlpha = 0f;
    private GUIStyle _subStyle, _shadowStyle;
    private Texture2D _whiteTex;
    private bool _stylesReady;

    private void Start()
    {
        if (sunReturn != null)
            sunReturn.OnReturnComplete += TriggerEnding;
        else
            Debug.LogWarning("[EndingCutscene] SunReturnController не назначен!");

        if (nyuwaAnimator == null && nyuwaTransform != null)
            nyuwaAnimator = nyuwaTransform.GetComponentInChildren<Animator>();

        exitButton?.onClick.AddListener(() =>
        {
            MainMenuController.SkipSplash = true;
            SceneManager.LoadScene(mainMenuScene);
        });

        if (fadeOverlay != null)
        {
            Color c = fadeOverlay.color;
            c.a = 0f;
            fadeOverlay.color = c;
            fadeOverlay.raycastTarget = false;
        }
        SetGroup(endScreenGroup, false);
    }

    private void OnDestroy()
    {
        if (sunReturn != null)
            sunReturn.OnReturnComplete -= TriggerEnding;
        if (_whiteTex != null) Destroy(_whiteTex);
    }

    public void TriggerEnding()
    {
        if (_triggered) return;
        _triggered = true;
        StartCoroutine(RunEndingSequence());
    }

    private IEnumerator RunEndingSequence()
    {
        yield return new WaitForSeconds(delayAfterSunReturn);

        var player = GameManager.Instance?.Player;
        player?.SetInteractMode(true);
        UIManager.Instance?.SetVisible(false);

        var voiceViz = FindAnyObjectByType<VoiceVisualizer>();
        if (voiceViz != null) voiceViz.gameObject.SetActive(false);

        if (player != null && pedestal != null)
            yield return StartCoroutine(MovePlayerToPedestal(player));

        if (nyuwaTransform != null && nyuwaWalkTarget != null)
        {
            if (nyuwaAnimator != null && !string.IsNullOrEmpty(nyuwaSpeedParam))
                nyuwaAnimator.SetFloat(nyuwaSpeedParam, 1f);

            yield return StartCoroutine(MoveNyuwa());

            if (nyuwaAnimator != null)
            {
                if (!string.IsNullOrEmpty(nyuwaSpeedParam))
                    nyuwaAnimator.SetFloat(nyuwaSpeedParam, 0f);
                if (!string.IsNullOrEmpty(nyuwaOfferParam))
                    nyuwaAnimator.SetTrigger(nyuwaOfferParam);
            }

            yield return new WaitForSeconds(offerAnimDuration);
        }

        foreach (var line in subtitleLines)
            yield return StartCoroutine(ShowSubtitle(line));

        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(FadeOverlayTo(1f, fadeToBlackDuration));
        yield return new WaitForSeconds(blackHoldDuration);

        SetGroup(endScreenGroup, true);
        yield return StartCoroutine(FadeGroup(endScreenGroup, 1f, endScreenFadeDuration));
    }

    private IEnumerator MovePlayerToPedestal(PlayerController player)
    {
        var rb = player.GetComponent<Rigidbody>();
        if (rb == null) yield break;

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;

        Vector3 from = player.transform.position;
        Vector3 to = pedestal.position;
        to.x = from.x;

        if (nyuwaTransform != null)
        {
            float zDiff = nyuwaTransform.position.z - to.z;
            player.transform.rotation = Quaternion.Euler(0f, zDiff >= 0f ? 0f : 180f, 0f);
        }

        float elapsed = 0f;
        while (elapsed < playerMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = playerMoveCurve.Evaluate(Mathf.Clamp01(elapsed / playerMoveDuration));
            player.transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        player.transform.position = to;
    }

    private IEnumerator MoveNyuwa()
    {
        Vector3 from = nyuwaTransform.position;
        Vector3 to = nyuwaWalkTarget.position;
        to.x = from.x;

        float elapsed = 0f;
        while (elapsed < nyuwaWalkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / nyuwaWalkDuration);
            nyuwaTransform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }
        nyuwaTransform.position = to;
    }

    private IEnumerator ShowSubtitle(string text)
    {
        yield return StartCoroutine(FadeSubtitle(text, 1f, subtitleFade));
        yield return new WaitForSeconds(lineDuration);
        yield return StartCoroutine(FadeSubtitle(text, 0f, subtitleFade));
    }

    private IEnumerator FadeSubtitle(string text, float targetAlpha, float duration)
    {
        _currentSubtitle = text;
        float startAlpha = _currentAlpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        _currentAlpha = targetAlpha;
        if (targetAlpha <= 0f) _currentSubtitle = "";
    }

    private void OnGUI()
    {
        if (_currentAlpha <= 0f || string.IsNullOrEmpty(_currentSubtitle)) return;

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
        GUI.Label(new Rect(x + shadow, y + shadow, w, h), _currentSubtitle, _shadowStyle);

        _subStyle.normal.textColor = new Color(
            subtitleColor.r, subtitleColor.g, subtitleColor.b, _currentAlpha);
        GUI.Label(new Rect(x, y, w, h), _currentSubtitle, _subStyle);
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

    private IEnumerator FadeOverlayTo(float target, float duration)
    {
        if (fadeOverlay == null) { yield return new WaitForSeconds(duration); yield break; }

        Color c = fadeOverlay.color;
        float start = c.a, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, elapsed / duration));
            fadeOverlay.color = c;
            yield return null;
        }
        c.a = target;
        fadeOverlay.color = c;
    }

    private IEnumerator FadeGroup(CanvasGroup cg, float target, float duration)
    {
        if (cg == null) yield break;
        float start = cg.alpha, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        cg.alpha = target;
    }

    private static void SetGroup(CanvasGroup cg, bool visible)
    {
        if (cg == null) return;
        cg.alpha = visible ? 0f : 0f;
        cg.blocksRaycasts = visible;
        cg.interactable = visible;
    }
}
