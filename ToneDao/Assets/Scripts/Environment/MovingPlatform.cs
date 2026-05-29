using System.Collections;
using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Transform extendedTarget;

    public float extendDuration = 0.8f;
    public float retractDuration = 0.6f;
    public AnimationCurve extendCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve retractCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool autoRetract = false;
    public float autoRetractDelay = 3f;

    public AudioClip extendSound;
    public AudioClip retractSound;

    public enum State { Retracted, Extending, Extended, Retracting }
    public State CurrentState { get; private set; } = State.Retracted;

    private Vector3 _retractedPos;
    private Coroutine _moveCoroutine;
    private AudioSource _audio;

    private void Awake()
    {
        _retractedPos = transform.position;

        if (extendSound != null || retractSound != null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
        }
    }

    public void Extend()
    {
        if (CurrentState == State.Extended || CurrentState == State.Extending) return;
        if (extendedTarget == null)
        {
            Debug.LogWarning($"[MovingPlatform] extendedTarget не назначен на {name}!");
            return;
        }

        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveTo(extendedTarget.position, extendDuration, extendCurve,
            State.Extending, State.Extended, extendSound,
            autoRetract ? () => StartCoroutine(AutoRetractAfterDelay()) : (System.Action)null));
    }

    public void Retract()
    {
        if (CurrentState == State.Retracted || CurrentState == State.Retracting) return;

        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveTo(_retractedPos, retractDuration, retractCurve,
            State.Retracting, State.Retracted, retractSound, null));
    }

    public void Toggle()
    {
        if (CurrentState == State.Retracted || CurrentState == State.Retracting) Extend();
        else Retract();
    }

    private IEnumerator MoveTo(Vector3 target, float duration, AnimationCurve curve,
        State duringState, State doneState, AudioClip sound, System.Action onComplete)
    {
        CurrentState = duringState;

        if (_audio != null && sound != null)
            _audio.PlayOneShot(sound);

        Vector3 from = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(from, target, t);
            yield return null;
        }

        transform.position = target;
        CurrentState = doneState;

        onComplete?.Invoke();
    }

    private IEnumerator AutoRetractAfterDelay()
    {
        yield return new WaitForSeconds(autoRetractDelay);
        Retract();
    }

    private void OnDrawGizmosSelected()
    {
        if (extendedTarget == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, extendedTarget.position);
        Gizmos.DrawWireSphere(extendedTarget.position, 0.2f);

        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawCube(extendedTarget.position, renderer.bounds.size);
        }
    }
}
