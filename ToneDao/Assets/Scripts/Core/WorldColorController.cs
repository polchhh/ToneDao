using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Volume))]
public class WorldColorController : MonoBehaviour
{

    public static WorldColorController Instance { get; private set; }

    public float saturationGray = -80f;
    public float saturationColor = 20f;

    public float exposureGray = -1.0f;
    public float exposureColor = 0.0f;

    public float bloomGray = 0.2f;
    public float bloomColor = 1.5f;

    public Color colorFilterGray = new Color(0.75f, 0.82f, 1.0f);
    public Color colorFilterColor = new Color(1.0f, 0.93f, 0.78f);

  
    public float sunFallDuration = 3.0f;
    public float fragmentDuration = 2.0f;

    private Volume _volume;
    private ColorAdjustments _colorAdj;
    private Bloom _bloom;

    private float _currentT = 1f;
    private float _targetT = 1f;
    private Coroutine _transition;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        _volume = GetComponent<Volume>();

        if (!_volume.profile.TryGet(out _colorAdj))
            _colorAdj = _volume.profile.Add<ColorAdjustments>(overrides: true);

        if (!_volume.profile.TryGet(out _bloom))
            _bloom = _volume.profile.Add<Bloom>(overrides: true);

        ApplyT(1f);
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnFragmentCollected += HandleFragmentCollected;
    }

    private void Start()
    {

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnFragmentCollected -= HandleFragmentCollected;
            GameManager.Instance.OnFragmentCollected += HandleFragmentCollected;

            SyncWithGameManager();
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnFragmentCollected -= HandleFragmentCollected;
    }

    private void OnDestroy()
    {

        if (_colorAdj != null)
        {
            _colorAdj.saturation.Override(0f);
            _colorAdj.postExposure.Override(0f);
            _colorAdj.colorFilter.Override(Color.white);
        }
        if (_bloom != null)
            _bloom.intensity.Override(1f);
    }

    public void TriggerSunFall()
    {

        _targetT = 0f;
        if (_transition != null) StopCoroutine(_transition);
        _transition = StartCoroutine(SmoothTransition(_currentT, 0f, sunFallDuration));
    }

    public void SetColorLevel(float t)
    {
        if (_transition != null) StopCoroutine(_transition);
        _currentT = Mathf.Clamp01(t);
        ApplyT(_currentT);
    }

    private void HandleFragmentCollected(int collected, int total)
    {
        if (total <= 0) return;

        _targetT = (collected >= total) ? 1f : Mathf.Clamp01((float)collected / total);

        if (_transition != null) StopCoroutine(_transition);
        _transition = StartCoroutine(SmoothTransition(_currentT, _targetT, fragmentDuration));
    }

    private void SyncWithGameManager()
    {
        int total = GameManager.Instance.FragmentsTotal;
        int collected = GameManager.Instance.FragmentsCollected;

        if (total <= 0 || GameManager.Instance.IsNewGame) return;

        float t = (collected >= total) ? 1f : Mathf.Clamp01((float)collected / total);
        SetColorLevel(t);
    }

    private IEnumerator SmoothTransition(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _currentT = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            ApplyT(_currentT);
            yield return null;
        }
        _currentT = to;
        ApplyT(_currentT);
    }

    private void ApplyT(float t)
    {
        if (_colorAdj != null)
        {
            _colorAdj.saturation.Override(Mathf.Lerp(saturationGray, saturationColor, t));
            _colorAdj.postExposure.Override(Mathf.Lerp(exposureGray, exposureColor, t));
            _colorAdj.colorFilter.Override(Color.Lerp(colorFilterGray, colorFilterColor, t));
        }
        if (_bloom != null)
            _bloom.intensity.Override(Mathf.Lerp(bloomGray, bloomColor, t));
    }
}
