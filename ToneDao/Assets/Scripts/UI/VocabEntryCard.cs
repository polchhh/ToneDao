using TMPro;
using UnityEngine;

public class VocabEntryCard : MonoBehaviour
{
    public TextMeshProUGUI hanziText;
    public TextMeshProUGUI pinyinText;
    public TextMeshProUGUI translationText;

    public void Setup(string hanzi, string pinyin, string translation)
    {
        if (hanziText) hanziText.text = hanzi;
        if (pinyinText) pinyinText.text = pinyin;
        if (translationText) translationText.text = translation;
    }
}
