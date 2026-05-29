using System.Collections;
using UnityEngine;

public class ButtonPlatform : MonoBehaviour
{
    public MovingPlatform[] targets;
    public bool isToggle = false;

    public Transform buttonVisual;
    public float pressDepth = 0.08f;
    public float pressDuration = 0.12f;

    public Color colorIdle = new Color(0.9f, 0.4f, 0.1f);
    public Color colorActive = new Color(0.2f, 0.85f, 0.3f);

    public AudioClip pressSound;
    public AudioClip releaseSound;

    public bool showHint = true;

    private bool _activated = false;
    private bool _playerOn = false;
    private PlayerController _player;

    private Vector3 _buttonRestPos;
    private Renderer _buttonRenderer;
    private AudioSource _audio;
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        if (buttonVisual != null)
            _buttonRestPos = buttonVisual.localPosition;

        _buttonRenderer = buttonVisual != null
            ? buttonVisual.GetComponent<Renderer>()
            : GetComponentInChildren<Renderer>();

        if (_buttonRenderer != null)
        {
            _mpb = new MaterialPropertyBlock();
            SetButtonColor(colorIdle);
        }

        if (pressSound != null || releaseSound != null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
        }
    }

    private void OnDestroy()
    {
        UnsubscribePlayer();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        _player = other.GetComponent<PlayerController>();
        _playerOn = true;

        if (_player != null)
            _player.OnGroundStamp += HandleStamp;

        if (showHint)
            UIManager.Instance?.ShowSimpleHint("Произнеси  Тон 4  (à)  —  топни по кнопке");

        Debug.Log("[ButtonPlatform] Игрок на кнопке — топни Тоном 4");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        UnsubscribePlayer();
        _playerOn = false;

        UIManager.Instance?.HideHint();
    }

    private void HandleStamp()
    {
        if (!_playerOn) return;

        if (isToggle)
        {
            _activated = !_activated;
            ActivateTargets(_activated);
        }
        else
        {
            if (_activated) return;
            _activated = true;
            ActivateTargets(true);
        }

        StartCoroutine(PressAnimation());
        PlaySound(_activated ? pressSound : releaseSound);
        SetButtonColor(_activated ? colorActive : colorIdle);

        Debug.Log($"[ButtonPlatform] Нажата! activated={_activated}");
    }

    private void ActivateTargets(bool extend)
    {
        if (targets == null) return;
        foreach (var t in targets)
        {
            if (t == null) continue;
            if (extend) t.Extend();
            else t.Retract();
        }
    }

    private IEnumerator PressAnimation()
    {
        if (buttonVisual == null) yield break;

        Vector3 pressedPos = _buttonRestPos + Vector3.down * pressDepth;

        float elapsed = 0f;
        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / pressDuration);
            buttonVisual.localPosition = Vector3.Lerp(_buttonRestPos, pressedPos, t);
            yield return null;
        }

        if (!_activated || isToggle && !_activated)
        {
            elapsed = 0f;
            while (elapsed < pressDuration * 1.5f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / (pressDuration * 1.5f));
                buttonVisual.localPosition = Vector3.Lerp(pressedPos, _buttonRestPos, t);
                yield return null;
            }
            buttonVisual.localPosition = _buttonRestPos;
        }
        else
        {

            buttonVisual.localPosition = pressedPos;
        }
    }

    private void SetButtonColor(Color color)
    {
        if (_buttonRenderer == null || _mpb == null) return;
        _mpb.SetColor("_BaseColor", color);
        _mpb.SetColor("_Color", color);
        _buttonRenderer.SetPropertyBlock(_mpb);
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audio == null || clip == null) return;
        _audio.PlayOneShot(clip);
    }

    private void UnsubscribePlayer()
    {
        if (_player != null)
        {
            _player.OnGroundStamp -= HandleStamp;
            _player = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (targets == null) return;
        Gizmos.color = Color.green;
        foreach (var t in targets)
            if (t != null)
            {
                Gizmos.DrawLine(transform.position, t.transform.position);
                Gizmos.DrawWireSphere(t.transform.position, 0.3f);
            }
    }
}
