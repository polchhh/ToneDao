using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 8f;
    public float highJumpForce = 14f;
    public float groundSlamForce = 20f;

    public LayerMask groundLayer;
    public float groundCheckRadius = 0.15f;
    public Transform groundCheckPoint;

    private Rigidbody rb;
    private Animator animator;
    private Cloth[] clothComponents;
    private VoiceInputManager voiceManager;
    private bool isGrounded;
    private bool wasGrounded;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int StampHash = Animator.StringToHash("Stamp");

    private float moveDirection = 1f;

    private Quaternion _targetRotation;
    public float turnSpeed = 720f;

    private float lastTone1Time = -1f;

    private bool _interactMode = false;

    public float respawnCooldown = 1.5f;
    public float respawnYOffset = 0.5f;

    public Vector3 LastSafePosition { get; private set; }

    private float _lastRespawnTime = -99f;
    private bool _respawnPending = false;

    public void SetInteractMode(bool active)
    {
        _interactMode = active;
        if (!rb.isKinematic)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        lastTone1Time = -1f;
        animator?.SetFloat(SpeedHash, 0f);
    }

    private const float MOVEMENT_TIMEOUT = 0.25f;

    private InputAction leftAction;
    private InputAction rightAction;

    public static Vector3? SpawnOverride = null;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
        clothComponents = GetComponentsInChildren<Cloth>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX;
        _targetRotation = transform.rotation;

        if (SpawnOverride.HasValue)
        {
            rb.position = SpawnOverride.Value;
            SpawnOverride = null;
        }

        LastSafePosition = rb.position;
        voiceManager = GameManager.Instance != null
            ? GameManager.Instance.VoiceManager
            : FindFirstObjectByType<VoiceInputManager>();

        InputSystem.settings.backgroundBehavior =
            UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;

        leftAction = new InputAction(type: InputActionType.Button);
        leftAction.AddBinding("<Keyboard>/a");
        leftAction.AddBinding("<Keyboard>/leftArrow");
        leftAction.performed += _ => SetDirection(-1f);

        rightAction = new InputAction(type: InputActionType.Button);
        rightAction.AddBinding("<Keyboard>/d");
        rightAction.AddBinding("<Keyboard>/rightArrow");
        rightAction.performed += _ => SetDirection(1f);
    }

    private void OnEnable()
    {
        leftAction.Enable();
        rightAction.Enable();
        if (voiceManager != null)
        {
            voiceManager.OnToneActive += HandleToneActive;
            voiceManager.OnToneDetected += HandleToneDetected;
            voiceManager.OnSilenceDetected += HandleSilence;
        }
    }

    private void OnDisable()
    {
        leftAction.Disable();
        rightAction.Disable();
        if (voiceManager != null)
        {
            voiceManager.OnToneActive -= HandleToneActive;
            voiceManager.OnToneDetected -= HandleToneDetected;
            voiceManager.OnSilenceDetected -= HandleSilence;
        }
    }

    private void SetDirection(float dir)
    {
        if (Mathf.Approximately(moveDirection, dir)) return;
        moveDirection = dir;

        float yAngle = dir > 0f ? 0f : 180f;
        _targetRotation = Quaternion.Euler(0f, yAngle, 0f);

        ResetCloth();
    }

    private void FixedUpdate()
    {
        isGrounded = Physics.CheckSphere(
            groundCheckPoint.position, groundCheckRadius, groundLayer);

        if (isGrounded && !_interactMode && !_respawnPending)
            LastSafePosition = rb.position;

        if (_respawnPending)
        {
            _respawnPending = false;
            rb.linearVelocity = Vector3.zero;
            rb.position = LastSafePosition + Vector3.up * respawnYOffset;
            return;
        }

        if (_interactMode)
        {

            if (!rb.isKinematic)
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        if (rb.isKinematic)
            rb.isKinematic = false;

        if (lastTone1Time > 0f && Time.time - lastTone1Time > MOVEMENT_TIMEOUT)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            lastTone1Time = -1f;
        }

        float horizontalSpeed = _interactMode ? 0f : Mathf.Abs(rb.linearVelocity.z);
        animator?.SetFloat(SpeedHash, horizontalSpeed);

        if (turnSpeed > 0f)
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, _targetRotation, turnSpeed * Time.fixedDeltaTime);
        else
            transform.rotation = _targetRotation;

        if (isGrounded && !wasGrounded)
            ResetCloth();
        wasGrounded = isGrounded;
    }

    private void ResetCloth()
    {
        foreach (var cloth in clothComponents)
        {
            cloth.enabled = false;
            cloth.enabled = true;
        }
    }

    private void HandleToneActive(VoiceInputManager.ToneType tone)
    {
        if (_interactMode) return;
        if (tone == VoiceInputManager.ToneType.Tone1)
        {
            Move();
            lastTone1Time = Time.time;
        }
    }

    private void HandleToneDetected(VoiceInputManager.ToneType tone)
    {
        if (_interactMode) return;
        switch (tone)
        {
            case VoiceInputManager.ToneType.Tone2: Jump(); break;
            case VoiceInputManager.ToneType.Tone3: HighJump(); break;
            case VoiceInputManager.ToneType.Tone4: GroundSlam(); break;
        }
    }

    private void HandleSilence()
    {
        if (_interactMode) return;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        lastTone1Time = -1f;
    }

    public void Move()
    {
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, moveSpeed * moveDirection);
    }

    public void Jump()
    {
        if (!isGrounded) return;
        rb.linearVelocity = new Vector3(0f, jumpForce, rb.linearVelocity.z);
        animator?.SetTrigger(JumpHash);
    }

    public void HighJump()
    {
        if (!isGrounded) return;
        rb.linearVelocity = new Vector3(0f, highJumpForce, rb.linearVelocity.z);
        animator?.SetTrigger(JumpHash);
    }

    public event System.Action OnGroundStamp;

    public void GroundSlam()
    {
        if (isGrounded)
        {

            animator?.SetTrigger(StampHash);
            OnGroundStamp?.Invoke();
        }
        else
        {

            rb.linearVelocity = new Vector3(0f, -groundSlamForce, rb.linearVelocity.z);
        }
    }

    public void Respawn()
    {
        if (Time.time - _lastRespawnTime < respawnCooldown) return;
        _lastRespawnTime = Time.time;
        _respawnPending = true;
    }
}
