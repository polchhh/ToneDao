using UnityEngine;

public class NyuwaController : MonoBehaviour
{
    public string animationStateName = "arm";

    public Transform handBone;

    public Vector3 handOffset = new Vector3(0f, 0.1f, 0f);

    public PlayerController player;

    public float maxDuration = 10f;

    private Animator _animator;
    private Rigidbody _playerRb;
    private bool _attached = false;
    private bool _finished = false;
    private float _elapsed = 0f;

    private void Start()
    {

        if (GameManager.Instance != null && !GameManager.Instance.IsNewGame)
        {
            _finished = true;
            enabled = false;
            return;
        }

        _animator = GetComponent<Animator>();

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player != null)
            _playerRb = player.GetComponent<Rigidbody>();

        Attach();
        player?.SetInteractMode(true);
    }

    private void Update()
    {
        if (_finished) return;
        if (_animator == null) return;

        _elapsed += Time.deltaTime;

        var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        bool doneByName = stateInfo.IsName(animationStateName) && stateInfo.normalizedTime >= 0.99f;
        bool doneByTime = _elapsed >= maxDuration;

        if (doneByName || doneByTime)
        {
            if (doneByTime && !doneByName)
                Debug.LogWarning($"[Nyuwa] Стейт «{animationStateName}» не найден — завершено по таймауту. " +
                                 $"Текущий стейт: {stateInfo.shortNameHash}");
            _finished = true;
            Detach();
            player?.SetInteractMode(false);
        }
    }

    private void LateUpdate()
    {

        if (!_attached || _finished || handBone == null || player == null) return;

        player.transform.position = handBone.position + handOffset;
    }

    private void Attach()
    {
        if (player == null) return;
        if (_playerRb != null)
        {
            _playerRb.linearVelocity = Vector3.zero;
            _playerRb.isKinematic = true;
        }

        if (handBone != null)
            handOffset = player.transform.position - handBone.position;

        _attached = true;
    }

    private void Detach()
    {
        _attached = false;
        if (_playerRb != null)
        {
            _playerRb.isKinematic = false;
            _playerRb.linearVelocity = Vector3.zero;
        }
    }
}
