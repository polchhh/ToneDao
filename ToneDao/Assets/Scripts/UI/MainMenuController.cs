using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public string gameSceneName = "Game";

    public CanvasGroup splashPanel;
    public CanvasGroup mainMenuPanel;
    public CanvasGroup playPanel;
    public CanvasGroup settingsPanel;

    public CanvasGroup phaseLogo;
    public CanvasGroup phaseMic;
    public CanvasGroup phaseHeadphones;
    public CanvasGroup phaseInternet;

    public Button newGameButton;
    public Button continueButton;
    public TextMeshProUGUI playMessageText;
    public Button playBackButton;

    public Slider masterSlider;
    public TextMeshProUGUI masterPct;
    public Slider musicSlider;
    public TextMeshProUGUI musicPct;

    public TextMeshProUGUI micDeviceText;
    public Button prevMicButton;
    public Button nextMicButton;
    public Button testMicButton;
    public TextMeshProUGUI testMicLabel;
    public Image micLevelFill;
    public Button settingsBackButton;

    public float fadeDuration = 0.7f;
    public float logoHold = 2.2f;
    public float micHold = 2.8f;
    public float headphonesHold = 2.8f;
    public float internetHold = 2.8f;

    public float micRecordSeconds = 2f;

    public static bool SkipSplash = false;

    private string[] _micDevices;
    private int _micIndex = 0;
    private AudioClip _micClip;
    private AudioSource _micSrc;
    private float _recEndTime = 0f;
    private float _micLevel = 0f;

    private enum MicState { Idle, Recording, Playing, Done }
    private MicState _micState = MicState.Idle;

    private void Awake()
    {

        SetAlpha(splashPanel, 0f, false);
        SetAlpha(mainMenuPanel, 0f, false);
        SetAlpha(playPanel, 0f, false);
        SetAlpha(settingsPanel, 0f, false);

        if (phaseLogo) SetAlpha(phaseLogo, 0f, false);
        if (phaseMic) SetAlpha(phaseMic, 0f, false);
        if (phaseHeadphones) SetAlpha(phaseHeadphones, 0f, false);
        if (phaseInternet) SetAlpha(phaseInternet, 0f, false);

        if (playMessageText) playMessageText.text = "";
        if (micLevelFill) micLevelFill.fillAmount = 0f;

        _micDevices = Microphone.devices;
        _micIndex = PlayerPrefs.GetInt("MicIndex", 0);
        if (_micDevices.Length > 0 && _micIndex >= _micDevices.Length)
            _micIndex = 0;

        _micSrc = gameObject.AddComponent<AudioSource>();
        _micSrc.playOnAwake = false;
        _micSrc.spatialBlend = 0f;
    }

    private void Start()
    {

        GetButton("PlayButton")?.onClick.AddListener(OpenPlayPanel);
        GetButton("SettingsButton")?.onClick.AddListener(() => OpenSettings());
        GetButton("QuitButton")?.onClick.AddListener(Application.Quit);

        if (newGameButton)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(OnNewGame);
        }
        if (continueButton)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinue);
        }
        if (playBackButton)
        {
            playBackButton.onClick.RemoveAllListeners();
            playBackButton.onClick.AddListener(() => ShowPanel(mainMenuPanel));
        }

        if (prevMicButton)
            prevMicButton.onClick.AddListener(() => ShiftMic(-1));
        if (nextMicButton)
            nextMicButton.onClick.AddListener(() => ShiftMic(+1));
        if (testMicButton)
            testMicButton.onClick.AddListener(OnTestMic);
        if (settingsBackButton)
            settingsBackButton.onClick.AddListener(() => ShowPanel(mainMenuPanel));

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

        if (SkipSplash)
        {
            SkipSplash = false;
            SetAlpha(mainMenuPanel, 1f, true);
            _activePanel = mainMenuPanel;
        }
        else
        {
            StartCoroutine(RunSplash());
        }
    }

    private void Update()
    {

        if (_micState == MicState.Recording
            && Time.realtimeSinceStartup >= _recEndTime)
        {
            Microphone.End(MicDevice());
            _micLevel = 0f;
            if (_micClip != null)
            {
                _micSrc.clip = _micClip;
                _micSrc.Play();
                _micState = MicState.Playing;
            }
            else
            {
                _micState = MicState.Done;
            }
        }

        if (_micState == MicState.Playing && !_micSrc.isPlaying)
        {
            _micState = MicState.Done;
            UpdateTestButton();
        }

        if (_micState == MicState.Recording && _micClip != null)
        {
            int pos = Microphone.GetPosition(MicDevice());
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

    private void OnDestroy()
    {
        if (_micState == MicState.Recording) Microphone.End(MicDevice());
    }

    private IEnumerator RunSplash()
    {

        SetAlpha(splashPanel, 1f, true);

        yield return PhaseShow(phaseLogo, logoHold);

        yield return PhaseShow(phaseMic, micHold);

        yield return PhaseShow(phaseHeadphones, headphonesHold);

        yield return PhaseShow(phaseInternet, internetHold);

        yield return Fade(splashPanel, 1f, 0f);
        SetAlpha(splashPanel, 0f, false);

        yield return Fade(mainMenuPanel, 0f, 1f);
    }

    private IEnumerator PhaseShow(CanvasGroup phase, float hold)
    {
        if (phase == null) yield break;
        yield return Fade(phase, 0f, 1f);
        yield return new WaitForSeconds(hold);
        yield return Fade(phase, 1f, 0f);
    }

    private CanvasGroup _activePanel;

    private void ShowPanel(CanvasGroup target)
    {
        StartCoroutine(SwitchPanels(target));
    }

    private IEnumerator SwitchPanels(CanvasGroup target)
    {

        if (_activePanel != null && _activePanel != mainMenuPanel)
            yield return Fade(_activePanel, 1f, 0f, 0.18f);

        if (playMessageText) playMessageText.text = "";

        _activePanel = target;

        if (target != mainMenuPanel)
            yield return Fade(target, 0f, 1f, 0.22f);
    }

    private void OpenSettings()
    {
        RefreshMicUI();
        ShowPanel(settingsPanel);
    }

    private void OpenPlayPanel()
    {
        ShowPanel(playPanel);
        RefreshPlayButtonsByConnection();
    }

    private void RefreshPlayButtonsByConnection()
    {
        bool online = IsOnline();

        if (newGameButton) newGameButton.interactable = online;
        if (continueButton) continueButton.interactable = online && SaveSystem.HasSave();

        if (playMessageText)
        {
            if (!online)
                playMessageText.text = "Нет подключения к интернету.\nИгра требует онлайн для распознавания речи.";
            else
                playMessageText.text = "";
        }
    }

    private static bool IsOnline() =>
        Application.internetReachability != NetworkReachability.NotReachable;

    private void OnNewGame()
    {
        if (!IsOnline())
        {
            RefreshPlayButtonsByConnection();
            return;
        }

        SaveSystem.DeleteAll();
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnContinue()
    {
        if (!IsOnline())
        {
            RefreshPlayButtonsByConnection();
            return;
        }

        if (SaveSystem.HasSave())
        {
            PlayerController.SpawnOverride = SaveSystem.LoadPlayerPosition(Vector3.zero);
            SceneManager.LoadScene(gameSceneName);
        }
        else if (playMessageText)
            playMessageText.text = "Сохранение не найдено";
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

        string dev = MicDevice();
        _micClip = Microphone.Start(dev, false, Mathf.CeilToInt(micRecordSeconds) + 1, 44100);
        _micState = MicState.Recording;
        _recEndTime = Time.realtimeSinceStartup + micRecordSeconds;
        _micLevel = 0f;
        UpdateTestButton();
    }

    private void ResetMicTest()
    {
        if (_micState == MicState.Recording) Microphone.End(MicDevice());
        _micSrc.Stop();
        _micState = MicState.Idle;
        _micLevel = 0f;
        UpdateTestButton();
        if (micLevelFill) micLevelFill.fillAmount = 0f;
    }

    private void RefreshMicUI()
    {
        if (micDeviceText == null) return;
        if (_micDevices == null || _micDevices.Length == 0)
            micDeviceText.text = "Микрофон не обнаружен";
        else
            micDeviceText.text = _micDevices[_micIndex];
    }

    private void UpdateTestButton()
    {
        if (testMicLabel == null) return;
        testMicLabel.text = _micState switch
        {
            MicState.Recording => "Говорите...",
            MicState.Playing => "Воспроизведение...",
            MicState.Done => "Тест микрофона",
            _ => "Тест микрофона"
        };
    }

    private string MicDevice() =>
        (_micDevices != null && _micDevices.Length > 0) ? _micDevices[_micIndex] : null;

    private IEnumerator Fade(CanvasGroup cg, float from, float to,
                              float duration = -1f)
    {
        if (cg == null) yield break;
        float dur = duration < 0 ? fadeDuration : duration;
        float t = 0f;

        cg.alpha = from;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / dur));
            yield return null;
        }

        cg.alpha = to;
        cg.interactable = to > 0.5f;
        cg.blocksRaycasts = to > 0.5f;
    }

    private static void SetAlpha(CanvasGroup cg, float a, bool interact)
    {
        if (cg == null) return;
        cg.alpha = a;
        cg.interactable = interact;
        cg.blocksRaycasts = interact;
    }

    private Button GetButton(string objName)
    {
        var t = mainMenuPanel?.transform.Find(objName);
        return t != null ? t.GetComponent<Button>() : null;
    }
}
