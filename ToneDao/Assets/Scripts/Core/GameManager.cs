using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        CollectReferences();
        LoadProgress();
    }

    public VoiceInputManager VoiceManager { get; private set; }
    public PlayerController Player { get; private set; }

    public int FragmentsCollected { get; private set; } = 0;
    public int FragmentsTotal { get; private set; } = 0;

    public bool IsNewGame { get; private set; } = true;

    public event System.Action<int, int> OnFragmentCollected;
    public event System.Action OnAllFragmentsCollected;

    private void CollectReferences()
    {
        VoiceManager = FindFirstObjectByType<VoiceInputManager>();
        Player = FindFirstObjectByType<PlayerController>();

        FragmentsTotal = FindObjectsByType<SunFragment>(FindObjectsSortMode.None).Length;

        Debug.Log($"[GameManager] На сцене найдено {FragmentsTotal} осколков.");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CollectReferences();
        LoadProgress();
    }

    private void LoadProgress()
    {

        IsNewGame = !SaveSystem.HasSave();
        FragmentsCollected = SaveSystem.LoadFragments();
        Debug.Log($"[GameManager] Загружено: {FragmentsCollected} осколков. IsNewGame={IsNewGame}");
    }

    public void RegisterFragmentCollected(VocabularyEntry wordEntry = null)
    {
        FragmentsCollected++;

        SaveSystem.SaveFragments(FragmentsCollected);
        if (Player != null)
            SaveSystem.SavePlayerPosition(Player.transform.position);

        if (wordEntry != null)
            VocabularyManager.Instance?.AddEntry(wordEntry);

        Debug.Log($"[GameManager] Осколков собрано: {FragmentsCollected}/{FragmentsTotal}");

        OnFragmentCollected?.Invoke(FragmentsCollected, FragmentsTotal);

        if (FragmentsCollected >= FragmentsTotal && FragmentsTotal > 0)
            HandleAllFragmentsCollected();
    }

    private void HandleAllFragmentsCollected()
    {
        Debug.Log("[GameManager] Все осколки собраны! Солнце возвращается.");
        OnAllFragmentsCollected?.Invoke();
    }

    public void NewGame()
    {
        SaveSystem.DeleteAll();
        FragmentsCollected = 0;
        VocabularyManager.Instance?.Clear();
        OnFragmentCollected?.Invoke(0, FragmentsTotal);
        Debug.Log("[GameManager] Новая игра — прогресс сброшен.");
    }
}
