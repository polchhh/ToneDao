using System;

public static class WordMatcher
{

    public static bool IsCorrect(string recognized, string hanzi,
                                  string hanziAlternate, string pinyin)
    {
        if (string.IsNullOrEmpty(recognized)) return false;

        string w = recognized.Trim().ToLower();

        if (string.IsNullOrEmpty(w)) return false;

        if (!string.IsNullOrEmpty(hanzi) && w == hanzi) return true;
        if (!string.IsNullOrEmpty(hanziAlternate) && w == hanziAlternate) return true;

        if (!string.IsNullOrEmpty(hanzi) && w.Contains(hanzi)) return true;
        if (!string.IsNullOrEmpty(hanziAlternate) && w.Contains(hanziAlternate)) return true;

        if (!string.IsNullOrEmpty(pinyin))
        {
            string p = pinyin.ToLower();
            if (w == p || w.Contains(p) || p.Contains(w)) return true;
        }

        return false;
    }
}
