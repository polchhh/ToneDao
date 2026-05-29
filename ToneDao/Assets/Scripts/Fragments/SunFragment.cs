using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SunFragment : MonoBehaviour
{
    public string fragmentId = "";
    public string hanzi = "树";
    public string hanziAlternate = "樹";
    public string targetPinyin = "shu";
    public string displayPinyin = "shù";
    public string translation = "дерево";
    public AudioClip pronunciationAudio = null;

    public float interactionRadius = 2.5f;

    public float recognitionTimeout = 6f;

    private WitAiRecognizer witAi;
    private PlayerController player;
    private VoiceInputManager voiceManager;

    private string SaveId => string.IsNullOrEmpty(fragmentId) ? gameObject.name : fragmentId;

    private bool isActivated = false;
    private bool playerNearby = false;
    private bool isListening = false;

    private Coroutine _timeoutCoroutine;

    private InputAction interactAction;
    public event System.Action<SunFragment> OnCollected;

    private void Awake()
    {
        interactAction = new InputAction(type: InputActionType.Button);
        interactAction.AddBinding("<Keyboard>/e");
        interactAction.performed += _ => TryInteract();
    }

    private void OnEnable() => interactAction.Enable();
    private void OnDisable() => interactAction.Disable();

    private void Start()
    {
        witAi = FindFirstObjectByType<WitAiRecognizer>();
        player = FindFirstObjectByType<PlayerController>();
        voiceManager = FindFirstObjectByType<VoiceInputManager>();

        if (witAi != null)
        {
            witAi.OnWordRecognized += HandleRecognizedWord;
            witAi.OnError += HandleRecognitionError;
        }
        else
        {
            Debug.LogWarning("[SunFragment] WitAiRecognizer не найден на сцене!");
        }

        if (SaveSystem.IsFragmentCollected(SaveId))
            RestoreAsCollected();
    }

    private void OnDestroy()
    {
        interactAction.Dispose();
        if (witAi != null)
        {
            witAi.OnWordRecognized -= HandleRecognizedWord;
            witAi.OnError -= HandleRecognitionError;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isActivated) return;
        if (other.CompareTag("Player"))
        {
            playerNearby = true;
            UIManager.Instance?.ShowHint(hanzi, displayPinyin, translation, pronunciationAudio);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNearby = false;
        UIManager.Instance?.HideHint();
        UIManager.Instance?.HideVoiceState();
        if (isListening) RestoreControls();
    }

    private void TryInteract()
    {
        if (isActivated || !playerNearby || isListening) return;
        if (witAi == null)
        {
            return;
        }

        StartListening();
    }

    private void StartListening()
    {
        player?.SetInteractMode(true);
        voiceManager?.SetPaused(true);
        isListening = true;

        UIManager.Instance?.ShowVoiceState("Говорите...");

        witAi.StartRecognition();

        if (_timeoutCoroutine != null) StopCoroutine(_timeoutCoroutine);
        _timeoutCoroutine = StartCoroutine(RecordingProgressCoroutine());
    }

    private IEnumerator RecordingProgressCoroutine()
    {

        float recordSec = witAi != null ? witAi.recordDurationSec : 2.5f;
        yield return new WaitForSeconds(recordSec);
        if (!isListening) yield break;

        UIManager.Instance?.ShowVoiceState("Распознаётся...");

        yield return new WaitForSeconds(recognitionTimeout);
        if (!isListening) yield break;

        UIManager.Instance?.ShowVoiceState("Нет ответа — попробуй снова");
        yield return new WaitForSeconds(1.5f);
        RestoreControls();
        UIManager.Instance?.HideVoiceState();
    }

    private void HandleRecognizedWord(string word)
    {
        if (!isListening || isActivated) return;

        if (_timeoutCoroutine != null) { StopCoroutine(_timeoutCoroutine); _timeoutCoroutine = null; }


        if (IsCorrect(word))
        {
            UIManager.Instance?.ShowVoiceState($"Верно!");
            StartCoroutine(ActivateAfterDelay(0.8f));
        }
        else
        {
            UIManager.Instance?.ShowVoiceState($"«{word}» — неверно,\nнажми E и попробуй снова");
            StartCoroutine(RestoreAfterDelay(2.0f));
        }
    }

    private void HandleRecognitionError(string error)
    {
        if (!isListening) return;

        if (_timeoutCoroutine != null) { StopCoroutine(_timeoutCoroutine); _timeoutCoroutine = null; }

        UIManager.Instance?.ShowVoiceState($"Ошибка — нажми E и попробуй снова");
        StartCoroutine(RestoreAfterDelay(2.0f));
    }

    private IEnumerator ActivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        UIManager.Instance?.HideVoiceState();
        Activate();
    }

    private IEnumerator RestoreAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        UIManager.Instance?.HideVoiceState();
        RestoreControls();
    }

    private bool IsCorrect(string word) =>
        WordMatcher.IsCorrect(word, hanzi, hanziAlternate, targetPinyin);

    private void RestoreControls()
    {
        isListening = false;
        if (_timeoutCoroutine != null) { StopCoroutine(_timeoutCoroutine); _timeoutCoroutine = null; }
        player?.SetInteractMode(false);
        voiceManager?.SetPaused(false);
    }

    private void Activate()
    {
        isActivated = true;
        RestoreControls();
        UIManager.Instance?.HideHint();
        OnCollected?.Invoke(this);

        SaveSystem.AddCollectedFragment(SaveId);

        var entry = new VocabularyEntry
        {
            hanzi = hanzi,
            pinyin = displayPinyin,
            translation = translation,
            audio = pronunciationAudio
        };
        GameManager.Instance?.RegisterFragmentCollected(entry);

        var effect = GetComponent<SunFragmentEffect>();
        effect?.Activate(null);
    }

    private void RestoreAsCollected()
    {
        isActivated = true;

        var effect = GetComponent<SunFragmentEffect>();
        if (effect != null)
            effect.Activate(null);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
