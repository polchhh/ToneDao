using System.Collections;
using UnityEngine;

public class SunReturnController : MonoBehaviour
{
    public Transform sunObject;
    public Transform sunOrigin;
    public Transform sunLanding;
    public AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public float gatherDuration = 2.0f;
    public float flashDuration = 0.6f;
    public float riseDuration = 5.0f;
    public float holdDuration = 3.0f;

    public ParticleSystem gatherBurst;
    public ParticleSystem sunTrail;
    public Light sunLight;
    public float sunLightMaxIntensity = 4f;

    public float flashSaturation = 60f;
    public float flashExposure = 1.5f;
    public float flashBloom = 8f;

    [TextArea] public string subtitleGather = "Все осколки солнца собраны...";
    [TextArea] public string subtitleRising = "Солнце возвращается на небосвод";
    [TextArea] public string subtitleFinal = "Благодаря тебе, Ни'эр, солнце вновь озаряет мир";
    public float subtitleFade = 0.5f;

    public event System.Action OnReturnComplete;

    private string _subtitle = "";
    private float _subtitleAlpha = 0f;
    private bool _triggered = false;

    private GUIStyle _subStyle, _shadowStyle;
    private Texture2D _whiteTex;
    private bool _stylesReady;

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnAllFragmentsCollected += Trigger;
    }

    private void Start()
    {

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAllFragmentsCollected -= Trigger;
            GameManager.Instance.OnAllFragmentsCollected += Trigger;
        }

        if (sunLight != null) sunLight.intensity = 0f;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnAllFragmentsCollected -= Trigger;
    }

    private void OnDestroy()
    {
        if (_whiteTex != null) Destroy(_whiteTex);
    }

    public void Trigger()
    {
        if (_triggered) return;
        _triggered = true;
        StartCoroutine(RunReturnSequence());
    }

    private IEnumerator RunReturnSequence()
    {

        var player = GameManager.Instance?.Player;
        player?.SetInteractMode(true);
        UIManager.Instance?.SetVisible(false);

        yield return StartCoroutine(ShowSubtitle(subtitleGather));

        if (gatherBurst != null) gatherBurst.Play();

        yield return StartCoroutine(PulseWorld(gatherDuration));

        yield return StartCoroutine(FlashWhite());

        if (sunObject != null && sunOrigin != null && sunLanding != null)
        {
            sunObject.position = sunLanding.position;
            sunObject.gameObject.SetActive(true);
        }

        if (sunTrail != null) sunTrail.Play();
        yield return StartCoroutine(ShowSubtitle(subtitleRising));
        yield return StartCoroutine(RiseSun());

        WorldColorController.Instance?.SetColorLevel(1f);
        if (WorldColorController.Instance != null)
            yield return StartCoroutine(
                SmoothColorRestore(WorldColorController.Instance.fragmentDuration));

        yield return StartCoroutine(ShowSubtitle(subtitleFinal));
        yield return new WaitForSeconds(holdDuration);
        yield return StartCoroutine(FadeSubtitle(0f, subtitleFade));

        Debug.Log("[SunReturn] Анимация завершена.");

        if (OnReturnComplete == null)
        {
            player?.SetInteractMode(false);
            UIManager.Instance?.SetVisible(true);
        }
        else
        {
            OnReturnComplete.Invoke();
        }
    }

    private IEnumerator PulseWorld(float duration)
    {
        var wcc = WorldColorController.Instance;
        if (wcc == null) { yield return new WaitForSeconds(duration); yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pulse = Mathf.Sin(elapsed / duration * Mathf.PI * 4f) * 0.5f + 0.5f;
            wcc.SetColorLevel(pulse * 0.25f);
            yield return null;
        }
    }

    private IEnumerator FlashWhite()
    {
        float half = flashDuration * 0.5f;

        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / half);
            WorldColorController.Instance?.SetColorLevel(t);
            if (sunLight) sunLight.intensity = Mathf.Lerp(0f, sunLightMaxIntensity * 2f, t);
            yield return null;
        }

        WorldColorController.Instance?.SetColorLevel(1f);

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / half;
            if (sunLight)
                sunLight.intensity = Mathf.Lerp(sunLightMaxIntensity * 2f, sunLightMaxIntensity, t);
            yield return null;
        }
    }

    private IEnumerator RiseSun()
    {
        if (sunObject == null || sunOrigin == null) yield break;

        Vector3 from = sunLanding != null ? sunLanding.position : sunObject.position;
        Vector3 to = sunOrigin.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / riseDuration;
            float curved = riseCurve.Evaluate(Mathf.Clamp01(t));
            sunObject.position = Vector3.Lerp(from, to, curved);

            if (sunLight)
                sunLight.intensity = Mathf.Lerp(sunLightMaxIntensity, sunLightMaxIntensity, curved);

            yield return null;
        }

        sunObject.position = to;
    }

    private IEnumerator SmoothColorRestore(float duration)
    {

        yield return new WaitForSeconds(duration);
    }

    private IEnumerator ShowSubtitle(string text)
    {
        yield return StartCoroutine(FadeSubtitle(0f, 0f));
        _subtitle = text;
        yield return StartCoroutine(FadeSubtitle(1f, subtitleFade));
        yield return new WaitForSeconds(2.5f);
        yield return StartCoroutine(FadeSubtitle(0f, subtitleFade));
    }

    private IEnumerator FadeSubtitle(float target, float duration)
    {
        float start = _subtitleAlpha, elapsed = 0f;
        if (duration <= 0f) { _subtitleAlpha = target; yield break; }
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _subtitleAlpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        _subtitleAlpha = target;
        if (target <= 0f) _subtitle = "";
    }

    private void OnGUI()
    {
        if (_subtitleAlpha <= 0f || string.IsNullOrEmpty(_subtitle)) return;
        EnsureStyles();

        int fs = Mathf.RoundToInt(Screen.height * 0.032f);
        float w = Screen.width * 0.65f;
        float h = fs * 4f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.72f;
        float pad = fs * 0.8f;
        float sh = Mathf.Max(2f, Screen.height * 0.002f);

        _subStyle.fontSize = fs;
        _shadowStyle.fontSize = fs;

        _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.9f * _subtitleAlpha);
        GUI.Label(new Rect(x + sh, y + sh, w, h), _subtitle, _shadowStyle);

        _subStyle.normal.textColor = new Color(1f, 0.95f, 0.75f, _subtitleAlpha);
        GUI.Label(new Rect(x, y, w, h), _subtitle, _subStyle);
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
}
