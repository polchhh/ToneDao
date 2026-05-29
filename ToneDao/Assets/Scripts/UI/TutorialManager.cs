using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{

    public static TutorialManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        if (tutorialGroup != null)
        {
            tutorialGroup.alpha = 0f;
            tutorialGroup.blocksRaycasts = false;
            tutorialGroup.interactable = false;
        }
    }

    private enum TutorialStepId
    {
        Move, Jump, HighJump, GroundSlam, Interact, Pronounce, Done
    }

    [System.Serializable]
    public class TutorialStep
    {
        [TextArea(1, 3)] public string title;
        [TextArea(2, 4)] public string instruction;
        public string toneSymbol;
        public AudioClip voiceSample;
    }

    public TutorialStep[] steps = new TutorialStep[]
    {
        new TutorialStep
        {
            title = "Движение",
            instruction = "Произнеси первый тон — ровный звук «а»\nудерживай его ровно, как будто поёшь одну ноту.",
            toneSymbol = "ā",
        },
        new TutorialStep
        {
            title = "Прыжок",
            instruction = "Произнеси второй тон — голос идёт вверх,\nкак будто переспрашиваешь: «А?»",
            toneSymbol = "á",
        },
        new TutorialStep
        {
            title = "Высокий прыжок",
            instruction = "Третий тон — голос сначала падает, потом поднимается",
            toneSymbol = "ǎ",
        },
        new TutorialStep
        {
            title = "Удар о землю",
            instruction = "Четвёртый тон — голос резко падает вниз.»",
            toneSymbol = "à",
        },
        new TutorialStep
        {
            title = "Взаимодействие с объектами",
            instruction = "Подойди к светящемуся объекту.\nКогда увидишь подсказку — нажми E",
            toneSymbol = "E",
        },
        new TutorialStep
        {
            title = "Произнеси название",
            instruction = "Произнеси название объекта по-китайски\n",
            toneSymbol = "Иероглиф",
        },
    };

    public CanvasGroup tutorialGroup;

    public Image[] progressDots;

    public TMP_Text titleText;
    public TMP_Text toneSymbolText;
    public Image toneDiagramImage;
    public Sprite[] toneSprites;
    public TMP_Text instructionText;

    public GameObject listenButtonContainer;
    public Button listenButton;
    public TMP_Text listenButtonLabel;

    public TMP_Text feedbackText;

    public Color accentColor = new Color(1f, 0.88f, 0.3f);
    public Color dimColor = new Color(0.75f, 0.75f, 0.75f);
    public Color successColor = new Color(0.35f, 1f, 0.55f);
    public Color doneColor = new Color(0.35f, 1f, 0.55f);

    private TutorialStepId _currentStep = TutorialStepId.Done;
    private bool _stepDone = false;
    private bool _active = false;
    private float _sampleEndTime = -1f;

    private VoiceInputManager _voice;
    private AudioSource _audioSource;

    private void Start()
    {

        if (SaveSystem.IsIntroCompleted())
        {
            SetGroupVisible(tutorialGroup, false);
            if (feedbackText != null) feedbackText.gameObject.SetActive(false);
            enabled = false;
            return;
        }

        _voice = GameManager.Instance?.VoiceManager
                 ?? FindFirstObjectByType<VoiceInputManager>();

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 0.9f;

        listenButton?.onClick.AddListener(PlayCurrentSample);

        SetGroupVisible(tutorialGroup, false);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    private void OnDestroy() => UnsubscribeAll();

    public void StartTutorial()
    {
        if (_active) return;

        if (SaveSystem.IsIntroCompleted())
        {
            Debug.Log("[Tutorial] StartTutorial проигнорирован — IntroCompleted=true");
            return;
        }

        SaveSystem.MarkIntroCompleted();

        _active = true;
        StartCoroutine(RunTutorial());
    }

    private IEnumerator RunTutorial()
    {
        yield return StartCoroutine(FadeGroup(tutorialGroup, 1f, 0.5f));

        for (int i = 0; i < steps.Length; i++)
        {
            _currentStep = (TutorialStepId)i;
            _stepDone = false;
            _sampleEndTime = -1f;

            RefreshStepDisplay(i);
            SubscribeForStep(_currentStep);

            while (!_stepDone)
            {

                UpdateListenLabel(i);
                yield return null;
            }

            UnsubscribeAll();
            yield return StartCoroutine(ShowFeedback("Отлично!", 1.2f));
        }

        _currentStep = TutorialStepId.Done;
        yield return StartCoroutine(ShowFeedback("Ты готов. Удачи, Ни'эр!", 2.5f));
        yield return StartCoroutine(FadeGroup(tutorialGroup, 0f, 0.6f));

        _active = false;
        Debug.Log("[Tutorial] Завершён.");
    }

    private void RefreshStepDisplay(int idx)
    {
        var step = steps[idx];

        if (titleText != null) titleText.text = step.title;
        if (toneSymbolText != null) toneSymbolText.text = step.toneSymbol;
        if (instructionText != null) instructionText.text = step.instruction;
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);

        if (progressDots != null)
        {
            for (int i = 0; i < progressDots.Length; i++)
            {
                if (progressDots[i] == null) continue;
                progressDots[i].color = i < idx ? doneColor :
                                        i == idx ? accentColor :
                                                   dimColor;
            }
        }

        if (toneDiagramImage != null)
        {
            bool hasDiagram = toneSprites != null && idx < toneSprites.Length
                              && toneSprites[idx] != null;
            toneDiagramImage.gameObject.SetActive(hasDiagram);
            if (hasDiagram)
                toneDiagramImage.sprite = toneSprites[idx];
        }

        if (listenButton != null)
        {
            bool hasAudio = step.voiceSample != null;

            if (listenButtonContainer != null)
                listenButtonContainer.SetActive(hasAudio);
            else
                listenButton.gameObject.SetActive(hasAudio);

            if (hasAudio && listenButtonLabel != null)
                listenButtonLabel.text = "Послушать образец";
        }
    }

    private void UpdateListenLabel(int idx)
    {
        if (listenButton == null || listenButtonLabel == null) return;
        if (steps[idx].voiceSample == null) return;

        bool isPlaying = Time.realtimeSinceStartup < _sampleEndTime;
        listenButtonLabel.text = isPlaying ? "Воспроизводится..." : "Послушать образец";
    }

    private void PlayCurrentSample()
    {
        int idx = (int)_currentStep;
        if (idx < 0 || idx >= steps.Length) return;
        var clip = steps[idx].voiceSample;
        if (clip == null) return;

        _audioSource.Stop();
        _audioSource.PlayOneShot(clip);
        _sampleEndTime = Time.realtimeSinceStartup + clip.length;
    }

    private void SubscribeForStep(TutorialStepId step)
    {
        if (_voice == null) return;
        switch (step)
        {
            case TutorialStepId.Move:
                _voice.OnToneActive += OnToneActive_Move; break;
            case TutorialStepId.Jump:
                _voice.OnToneDetected += OnToneDetected_Jump; break;
            case TutorialStepId.HighJump:
                _voice.OnToneDetected += OnToneDetected_HighJump; break;
            case TutorialStepId.GroundSlam:
                _voice.OnToneDetected += OnToneDetected_Slam; break;
            case TutorialStepId.Interact:
                StartCoroutine(WaitForInteract()); break;
            case TutorialStepId.Pronounce:
                if (GameManager.Instance != null)
                    GameManager.Instance.OnFragmentCollected += OnFragmentCollected; break;
        }
    }

    private void UnsubscribeAll()
    {
        if (_voice != null)
        {
            _voice.OnToneActive -= OnToneActive_Move;
            _voice.OnToneDetected -= OnToneDetected_Jump;
            _voice.OnToneDetected -= OnToneDetected_HighJump;
            _voice.OnToneDetected -= OnToneDetected_Slam;
        }
        if (GameManager.Instance != null)
            GameManager.Instance.OnFragmentCollected -= OnFragmentCollected;
    }

    private void OnToneActive_Move(VoiceInputManager.ToneType t)
        { if (t == VoiceInputManager.ToneType.Tone1) _stepDone = true; }

    private void OnToneDetected_Jump(VoiceInputManager.ToneType t)
        { if (t == VoiceInputManager.ToneType.Tone2) _stepDone = true; }

    private void OnToneDetected_HighJump(VoiceInputManager.ToneType t)
        { if (t == VoiceInputManager.ToneType.Tone3) _stepDone = true; }

    private void OnToneDetected_Slam(VoiceInputManager.ToneType t)
        { if (t == VoiceInputManager.ToneType.Tone4) _stepDone = true; }

    private IEnumerator WaitForInteract()
    {
        while (!_stepDone)
        {
            if (_voice != null && _voice.IsPaused)
                _stepDone = true;
            yield return null;
        }
    }

    private void OnFragmentCollected(int collected, int total) => _stepDone = true;

    private IEnumerator FadeGroup(CanvasGroup cg, float target, float duration)
    {
        if (cg == null) yield break;
        float start = cg.alpha, elapsed = 0f;

        bool visible = target > 0f;
        cg.blocksRaycasts = visible;
        cg.interactable = visible;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        cg.alpha = target;
    }

    private IEnumerator ShowFeedback(string msg, float duration)
    {
        if (feedbackText == null) { yield return new WaitForSeconds(duration); yield break; }

        feedbackText.gameObject.SetActive(true);
        feedbackText.text = msg;
        feedbackText.color = successColor;

        yield return new WaitForSeconds(duration * 0.7f);

        float elapsed = 0f, fade = duration * 0.3f;
        Color c = feedbackText.color;
        while (elapsed < fade)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / fade);
            feedbackText.color = c;
            yield return null;
        }

        feedbackText.gameObject.SetActive(false);
    }

    private static void SetGroupVisible(CanvasGroup cg, bool visible)
    {
        if (cg == null) return;
        cg.alpha = visible ? 1f : 0f;
        cg.blocksRaycasts = visible;
        cg.interactable = visible;
    }
}
