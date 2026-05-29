using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{

    public static MusicManager Instance { get; private set; }

    [System.Serializable]
    public class SceneMusic
    {
        public string sceneName;
        public AudioClip clip;
    }

    public SceneMusic[] sceneTracks;

    public float crossfadeDuration = 1.5f;
    [Range(0f, 1f)]
    public float defaultVolume = 0.5f;

    private AudioSource _srcA;
    private AudioSource _srcB;
    private float _volume = 1f;
    private Coroutine _fade;

    private const string KEY_VOL = "MusicVolume";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _srcA = AddSource();
        _srcB = AddSource();

        _volume = PlayerPrefs.GetFloat(KEY_VOL, defaultVolume);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {

        AudioClip clip = FindClipForScene(SceneManager.GetActiveScene().name);
        if (clip != null)
        {
            _srcA.clip = clip;
            _srcA.volume = _volume;
            _srcA.Play();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AudioClip clip = FindClipForScene(scene.name);

        if (clip == null)
        {
            StopAll();
            return;
        }

        if (_srcA.isPlaying && _srcA.clip == clip) return;

        PlayTrack(clip);
    }

    public void PlayTrack(AudioClip clip)
    {
        if (clip == null) { StopAll(); return; }

        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Crossfade(clip));
    }

    public void SetVolume(float value)
    {
        _volume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KEY_VOL, _volume);

        _srcA.volume = _volume;
    }

    public float GetVolume() => _volume;

    private IEnumerator Crossfade(AudioClip newClip)
    {

        _srcB.clip = newClip;
        _srcB.volume = 0f;
        _srcB.Play();

        float elapsed = 0f;
        float startVol = _srcA.volume;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / crossfadeDuration);
            _srcA.volume = Mathf.Lerp(startVol, 0f, t);
            _srcB.volume = Mathf.Lerp(0f, _volume, t);
            yield return null;
        }

        _srcA.Stop();
        _srcA.volume = 0f;

        (_srcA, _srcB) = (_srcB, _srcA);
        _srcA.volume = _volume;
    }

    private void StopAll()
    {
        if (_fade != null) StopCoroutine(_fade);
        _srcA.Stop();
        _srcB.Stop();
    }

    private AudioClip FindClipForScene(string sceneName)
    {
        if (sceneTracks == null) return null;
        foreach (var entry in sceneTracks)
            if (entry.sceneName == sceneName)
                return entry.clip;
        return null;
    }

    private AudioSource AddSource()
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        return src;
    }
}
