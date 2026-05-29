using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{

    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    public CanvasGroup hudRoot;

    public TextMeshProUGUI counterText;

    public CanvasGroup hintGroup;
    public TextMeshProUGUI hintText;
    public Button listenButton;

    public CanvasGroup voiceStateGroup;
    public TextMeshProUGUI voiceStateText;

    public CanvasGroup pausePanel;
    public Button resumeButton;
    public Button settingsButton;
    public Button vocabButton;
    public Button quitButton;
    public string mainMenuScene = "MainMenu";

    public CanvasGroup settingsPanel;
    public Slider masterSlider;
    public TextMeshProUGUI masterPct;
    public Slider musicSlider;
    public TextMeshProUGUI musicPct;
    public Button settingsBackButton;

    public TextMeshProUGUI micDeviceText;
    public Button prevMicButton;
    public Button nextMicButton;
    public Button testMicButton;
    public TextMeshProUGUI testMicLabel;
    public Image micLevelFill;
    public float micRecordSeconds = 2f;

    public CanvasGroup vocabPanel;
    public Button closeButton;
    public TextMeshProUGUI emptyLabel;
    public Transform vocabContent;
    public GameObject entryPrefab;

    private bool _hudVisible = true;
    private bool _paused = false;
    private readonly List<GameObject> _entryRows = new();

    private AudioSource _hintAudio;
    private AudioClip _currentHintClip;

    private string[] _micDevices;
    private int _micIndex = 0;
    private AudioClip _micClip;
    private AudioSource _micSrc;
    private float _recEndTime = 0f;
    private float _micLevel = 0f;

    private enum MicState { Idle, Recording, Playing, Done }
    private MicState _micState = MicState.Idle;

    private void Start()
    {

        _hintAudio = gameObject.AddComponent<AudioSource>();
        _hintAudio.playOnAwake = false;
        _hintAudio.spatialBlend = 0f;

        if (listenButton != null)
            listenButton.gameObject.SetActive(false);
        else
            Debug.LogWarning("[UIManager] Listen Button не назначен в Inspector!");

        HideVoiceState();

        resumeButton?.onClick.AddListener(Resume);
        settingsButton?.onClick.AddListener(OpenSettings);
        vocabButton?.onClick.AddListener(OpenVocab);
        quitButton?.onClick.AddListener(QuitToMenu);

        settingsBackButton?.onClick.AddListener(CloseSubPanels);
        if (masterSlider)
        {
            masterSlider.value = AudioListener.volume;
            masterSlider.onValueChanged.AddListener(v =>
            {
                AudioListener.volume = v;
                if (masterPct) masterPct.text = $"{Mathf.RoundToInt(v * 100)} %";
            });
            if (masterPct) masterPct.text = $"{Mathf.RoundToInt(masterSlider.value * 100)} %";
        }
        if (musicSlider)
        {
            musicSlider.value = MusicManager.Instance != null
                ? MusicManager.Instance.GetVolume()
                : PlayerPrefs.GetFloat("MusicVolume", 1f);
            musicSlider.onValueChanged.AddListener(v =>
            {
                MusicManager.Instance?.SetVolume(v);
                if (musicPct) musicPct.text = $"{Mathf.RoundToInt(v * 100)} %";
            });
            if (musicPct) musicPct.text = $"{Mathf.RoundToInt(musicSlider.value * 100)} %";
        }

        _micDevices = Microphone.devices;
        _micIndex = PlayerPrefs.GetInt("MicIndex", 0);
        if (_micDevices.Length > 0 && _micIndex >= _micDevices.Length) _micIndex = 0;
        _micSrc = gameObject.AddComponent<AudioSource>();
        _micSrc.playOnAwake = false;
        _micSrc.spatialBlend = 0f;

        prevMicButton?.onClick.AddListener(() => ShiftMic(-1));
        nextMicButton?.onClick.AddListener(() => ShiftMic(+1));
        testMicButton?.onClick.AddListener(OnTestMic);
        if (micLevelFill) micLevelFill.fillAmount = 0f;
        RefreshMicUI();
        UpdateTestButton();

        closeButton?.onClick.AddListener(CloseSubPanels);

        if (GameManager.Instance != null)
            GameManager.Instance.OnFragmentCollected += OnFragmentCollected;
        if (VocabularyManager.Instance != null)
            VocabularyManager.Instance.OnWordAdded += _ => RebuildVocabList();

        SetHintVisible(false);
        SetPauseVisible(false);
        SetSubPanel(null);
        UpdateCounter();
        RebuildVocabList();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnFragmentCollected -= OnFragmentCollected;

        if (_paused) Time.timeScale = 1f;
        if (_micState == MicState.Recording) Microphone.End(CurrentMicDevice());
    }

    private void Update()
    {
        if (!_hudVisible) return;

        if (Keyboard.current != null && Keyboard.current[Key.Escape].wasPressedThisFrame)
        {
            if (_paused) Resume();
            else Pause();
        }

        UpdateMic();
    }

    private void UpdateMic()
    {

        if (_micState == MicState.Recording && Time.realtimeSinceStartup >= _recEndTime)
        {
            Microphone.End(CurrentMicDevice());
            _micLevel = 0f;
            if (_micClip != null)
            {
                _micSrc.clip = _micClip;
                _micSrc.Play();
                _micState = MicState.Playing;
            }
            else _micState = MicState.Done;
        }

        if (_micState == MicState.Playing && !_micSrc.isPlaying)
        {
            _micState = MicState.Done;
            UpdateTestButton();
        }

        if (_micState == MicState.Recording && _micClip != null)
        {
            int pos = Microphone.GetPosition(CurrentMicDevice());
            if (pos > 128)
            {
                float[] buf = new float[128];
                _micClip.GetData(buf, pos - 128);
                float sum = 0f;
                foreach (float s in buf) sum += s * s;
                _micLevel = Mathf.Lerp(_micLevel,
                    Mathf.Sqrt(sum / buf.Length), Time.deltaTime * 15f);
            }
        }
        else
        {
            _micLevel = Mathf.Lerp(_micLevel, 0f, Time.deltaTime * 8f);
        }

        if (micLevelFill != null)
            micLevelFill.fillAmount = Mathf.Clamp01(_micLevel * 7f);
    }

    public void SetVisible(bool value)
    {
        _hudVisible = value;
        if (!value && _paused) Resume();

        if (hudRoot == null) return;
        hudRoot.alpha = value ? 1f : 0f;
        hudRoot.interactable = value;
        hudRoot.blocksRaycasts = value;
    }

    public void ShowHint(string hanzi, string pinyin, string translation,
                         AudioClip audio = null)
    {
        if (hintText != null)
            hintText.text = $"Нажми  E  —  произнеси:  {hanzi}  ({pinyin})  «{translation}»";

        _currentHintClip = audio;
        if (listenButton != null)
            listenButton.gameObject.SetActive(audio != null);

        SetHintVisible(true);
    }

    public void ShowSimpleHint(string text)
    {
        if (hintText != null)
            hintText.text = text;

        _currentHintClip = null;
        if (listenButton != null)
            listenButton.gameObject.SetActive(false);

        SetHintVisible(true);
    }

    public void HideHint()
    {
        _hintAudio?.Stop();
        _currentHintClip = null;
        if (listenButton != null)
            listenButton.gameObject.SetActive(false);
        SetHintVisible(false);
    }

    public void ShowVoiceState(string message)
    {
        if (voiceStateText != null) { voiceStateText.text = message; }
        if (voiceStateGroup != null) { voiceStateGroup.alpha = 1f; voiceStateGroup.blocksRaycasts = false; }
    }

    public void HideVoiceState()
    {
        if (voiceStateGroup != null) voiceStateGroup.alpha = 0f;
    }

    public void PlayHintAudio()
    {
        Debug.Log($"[UIManager] PlayHintAudio вызван, клип: {(_currentHintClip != null ? _currentHintClip.name : "NULL")}");

        if (_currentHintClip == null)
        {
            Debug.LogWarning("[UIManager] PlayHintAudio: клип не назначен");
            return;
        }

        if (_hintAudio == null)
            _hintAudio = gameObject.AddComponent<AudioSource>();

        _hintAudio.Stop();
        _hintAudio.spatialBlend = 0f;
        _hintAudio.clip = _currentHintClip;
        _hintAudio.volume = 1f;
        _hintAudio.Play();
    }

    private void Pause()
    {
        _paused = true;
        Time.timeScale = 0f;
        SetSubPanel(null);
        SetPauseVisible(true);
    }

    private void Resume()
    {
        _paused = false;
        Time.timeScale = 1f;
        SetPauseVisible(false);
    }

    private void OpenSettings()
    {
        SetSubPanel(settingsPanel);
    }

    private void OpenVocab()
    {
        RebuildVocabList();
        SetSubPanel(vocabPanel);
    }

    private void CloseSubPanels() => SetSubPanel(null);

    private void QuitToMenu()
    {
        _paused = false;
        Time.timeScale = 1f;
        MainMenuController.SkipSplash = true;
        SceneManager.LoadScene(mainMenuScene);
    }

    private void OnFragmentCollected(int collected, int total) => UpdateCounter();

    private void UpdateCounter()
    {
        if (counterText == null) return;
        int c = GameManager.Instance != null ? GameManager.Instance.FragmentsCollected : 0;
        int t = GameManager.Instance != null ? GameManager.Instance.FragmentsTotal : 0;
        counterText.text = $"{c} / {t}";
    }

    private void SetHintVisible(bool show)
    {
        if (hintGroup == null) return;
        hintGroup.alpha = show ? 1f : 0f;
        hintGroup.blocksRaycasts = show;
    }

    private void SetPauseVisible(bool show)
    {
        if (pausePanel == null) return;
        pausePanel.alpha = show ? 1f : 0f;
        pausePanel.interactable = show;
        pausePanel.blocksRaycasts = show;
    }

    private void SetSubPanel(CanvasGroup target)
    {
        SetCG(settingsPanel, target == settingsPanel);
        SetCG(vocabPanel, target == vocabPanel);
    }

    private static void SetCG(CanvasGroup cg, bool show)
    {
        if (cg == null) return;
        cg.alpha = show ? 1f : 0f;
        cg.interactable = show;
        cg.blocksRaycasts = show;
    }

    private void ShiftMic(int dir)
    {
        if (_micDevices == null || _micDevices.Length == 0) return;
        _micIndex = (_micIndex + dir + _micDevices.Length) % _micDevices.Length;
        PlayerPrefs.SetInt("MicIndex", _micIndex);
        ResetMicTest();
        RefreshMicUI();
    }

    private void OnTestMic()
    {
        if (_micState == MicState.Recording || _micState == MicState.Playing) return;
        string dev = CurrentMicDevice();
        _micClip = Microphone.Start(dev, false, Mathf.CeilToInt(micRecordSeconds) + 1, 44100);
        _micState = MicState.Recording;
        _recEndTime = Time.realtimeSinceStartup + micRecordSeconds;
        _micLevel = 0f;
        UpdateTestButton();
    }

    private void ResetMicTest()
    {
        if (_micState == MicState.Recording) Microphone.End(CurrentMicDevice());
        _micSrc?.Stop();
        _micState = MicState.Idle;
        _micLevel = 0f;
        if (micLevelFill) micLevelFill.fillAmount = 0f;
        UpdateTestButton();
    }

    private void RefreshMicUI()
    {
        if (micDeviceText == null) return;
        micDeviceText.text = (_micDevices != null && _micDevices.Length > 0)
            ? _micDevices[_micIndex]
            : "Микрофон не обнаружен";
    }

    private void UpdateTestButton()
    {
        if (testMicLabel == null) return;
        testMicLabel.text = _micState switch
        {
            MicState.Recording => "Говорите...",
            MicState.Playing => "Воспроизведение...",
            MicState.Done => "Тест ещё раз",
            _ => "Тест микрофона"
        };
    }

    private string CurrentMicDevice() =>
        (_micDevices != null && _micDevices.Length > 0) ? _micDevices[_micIndex] : null;

    private void RebuildVocabList()
    {
        if (vocabContent == null) return;

        foreach (var go in _entryRows) Destroy(go);
        _entryRows.Clear();

        var entries = VocabularyManager.Instance?.Entries;
        bool empty = entries == null || entries.Count == 0;

        if (emptyLabel != null) emptyLabel.gameObject.SetActive(empty);
        if (empty || entryPrefab == null) return;

        foreach (var e in entries)
        {
            var card = Instantiate(entryPrefab, vocabContent);
            card.GetComponent<VocabEntryCard>()?.Setup(e.hanzi, e.pinyin, e.translation);
            _entryRows.Add(card);
        }
    }
}
