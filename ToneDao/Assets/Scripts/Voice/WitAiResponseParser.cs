using System;

public static class WitAiResponseParser
{

    public static string Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        const string key = "\"text\"";
        int keyPos = json.LastIndexOf(key, StringComparison.Ordinal);
        if (keyPos < 0) return null;

        int colon = json.IndexOf(':', keyPos + key.Length);
        if (colon < 0) return null;

        int openQuote = json.IndexOf('"', colon + 1);
        if (openQuote < 0) return null;

        openQuote++;
        int closeQuote = json.IndexOf('"', openQuote);
        if (closeQuote < 0) return null;

        string result = json.Substring(openQuote, closeQuote - openQuote);
        return string.IsNullOrEmpty(result) ? null : result;
    }
}
