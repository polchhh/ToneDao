using System.Collections.Generic;
using UnityEngine;

public static class SaveSystem
{
    private const string KEY_FRAGMENTS = "FragmentsCollected";
    private const string KEY_ZONE = "CurrentZone";
    private const string KEY_VOCAB = "VocabularyJSON";
    private const string KEY_POS_X = "PlayerPosX";
    private const string KEY_POS_Y = "PlayerPosY";
    private const string KEY_POS_Z = "PlayerPosZ";
    private const string KEY_COLLECTED_IDS = "CollectedFragmentIDs";
    private const string KEY_INTRO_COMPLETED = "IntroCompleted";

    public static void SaveFragments(int count)
    {
        PlayerPrefs.SetInt(KEY_FRAGMENTS, count);
        PlayerPrefs.Save();
    }

    public static int LoadFragments() =>
        PlayerPrefs.GetInt(KEY_FRAGMENTS, 0);

    public static void SaveZone(int zone)
    {
        PlayerPrefs.SetInt(KEY_ZONE, zone);
        PlayerPrefs.Save();
    }

    public static int LoadZone() =>
        PlayerPrefs.GetInt(KEY_ZONE, 1);

    [System.Serializable]
    private class VocabWrapper
    {
        public List<VocabData> words = new();
    }

    [System.Serializable]
    private class VocabData
    {
        public string hanzi;
        public string pinyin;
        public string translation;
    }

    public static void SaveVocabulary(IList<VocabularyEntry> entries)
    {
        var wrapper = new VocabWrapper();
        foreach (var e in entries)
            wrapper.words.Add(new VocabData
            {
                hanzi = e.hanzi,
                pinyin = e.pinyin,
                translation = e.translation
            });

        PlayerPrefs.SetString(KEY_VOCAB, JsonUtility.ToJson(wrapper));
        PlayerPrefs.Save();
    }

    public static List<VocabularyEntry> LoadVocabulary()
    {
        if (!PlayerPrefs.HasKey(KEY_VOCAB)) return null;

        var wrapper = JsonUtility.FromJson<VocabWrapper>(PlayerPrefs.GetString(KEY_VOCAB));
        if (wrapper == null) return null;

        var list = new List<VocabularyEntry>();
        foreach (var d in wrapper.words)
            list.Add(new VocabularyEntry
            {
                hanzi = d.hanzi,
                pinyin = d.pinyin,
                translation = d.translation

            });

        return list;
    }

    public static void AddCollectedFragment(string id)
    {
        var set = LoadCollectedFragments();
        set.Add(id);
        PlayerPrefs.SetString(KEY_COLLECTED_IDS, string.Join(",", set));
        PlayerPrefs.Save();
    }

    public static bool IsFragmentCollected(string id) =>
        LoadCollectedFragments().Contains(id);

    public static System.Collections.Generic.HashSet<string> LoadCollectedFragments()
    {
        var raw = PlayerPrefs.GetString(KEY_COLLECTED_IDS, "");
        var set = new System.Collections.Generic.HashSet<string>();
        if (!string.IsNullOrEmpty(raw))
            foreach (var s in raw.Split(','))
                if (!string.IsNullOrEmpty(s)) set.Add(s);
        return set;
    }

    public static void SavePlayerPosition(Vector3 pos)
    {
        PlayerPrefs.SetFloat(KEY_POS_X, pos.x);
        PlayerPrefs.SetFloat(KEY_POS_Y, pos.y);
        PlayerPrefs.SetFloat(KEY_POS_Z, pos.z);
        PlayerPrefs.Save();
    }

    public static Vector3 LoadPlayerPosition(Vector3 defaultPos)
    {
        if (!PlayerPrefs.HasKey(KEY_POS_X)) return defaultPos;
        return new Vector3(
            PlayerPrefs.GetFloat(KEY_POS_X),
            PlayerPrefs.GetFloat(KEY_POS_Y),
            PlayerPrefs.GetFloat(KEY_POS_Z));
    }

    public static bool HasSave() =>
        PlayerPrefs.HasKey(KEY_FRAGMENTS) || PlayerPrefs.HasKey(KEY_ZONE);

    public static void MarkIntroCompleted()
    {
        PlayerPrefs.SetInt(KEY_INTRO_COMPLETED, 1);
        PlayerPrefs.Save();
    }

    public static bool IsIntroCompleted() =>
        PlayerPrefs.GetInt(KEY_INTRO_COMPLETED, 0) == 1;

    public static void DeleteAll()
    {
        PlayerPrefs.DeleteKey(KEY_FRAGMENTS);
        PlayerPrefs.DeleteKey(KEY_ZONE);
        PlayerPrefs.DeleteKey(KEY_VOCAB);
        PlayerPrefs.DeleteKey(KEY_POS_X);
        PlayerPrefs.DeleteKey(KEY_POS_Y);
        PlayerPrefs.DeleteKey(KEY_POS_Z);
        PlayerPrefs.DeleteKey(KEY_COLLECTED_IDS);
        PlayerPrefs.DeleteKey(KEY_INTRO_COMPLETED);
        PlayerPrefs.Save();
        Debug.Log("[SaveSystem] Сохранение удалено.");
    }
}
