using System.Collections.Generic;
using UnityEngine;

public class VocabularyManager : MonoBehaviour
{

    public static VocabularyManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    private readonly List<VocabularyEntry> _entries = new();

    public IReadOnlyList<VocabularyEntry> Entries => _entries;

    public event System.Action<VocabularyEntry> OnWordAdded;

    public void AddEntry(VocabularyEntry entry)
    {
        if (entry == null) return;
        if (_entries.Exists(e => e.hanzi == entry.hanzi)) return;

        _entries.Add(entry);
        Debug.Log($"[Vocabulary] +{entry.hanzi} ({entry.pinyin}) = {entry.translation}");
        OnWordAdded?.Invoke(entry);
        SaveSystem.SaveVocabulary(_entries);
    }

    private void Start()
    {
        var saved = SaveSystem.LoadVocabulary();
        if (saved != null)
        {
            _entries.Clear();
            _entries.AddRange(saved);
            Debug.Log($"[Vocabulary] Загружено {_entries.Count} слов из сохранения.");
        }
    }

    public void Clear()
    {
        _entries.Clear();
        SaveSystem.SaveVocabulary(_entries);
    }
}
