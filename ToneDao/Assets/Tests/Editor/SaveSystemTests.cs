using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Unit-тесты для SaveSystem — обвязка над PlayerPrefs.
///
/// Внимание: каждый тест чистит PlayerPrefs в [SetUp] и [TearDown],
/// чтобы тесты не зависели друг от друга.
/// </summary>
[TestFixture]
public class SaveSystemTests
{
    [SetUp]
    public void Setup()
    {
        SaveSystem.DeleteAll();
    }

    [TearDown]
    public void TearDown()
    {
        SaveSystem.DeleteAll();
    }

    // ─── Счётчик осколков ─────────────────────────────────────────────────

    [Test]
    public void SaveAndLoadFragments_ReturnsCorrectCount()
    {
        SaveSystem.SaveFragments(5);
        Assert.AreEqual(5, SaveSystem.LoadFragments());
    }

    [Test]
    public void LoadFragments_WithoutSave_ReturnsZero()
    {
        Assert.AreEqual(0, SaveSystem.LoadFragments());
    }

    [Test]
    public void SaveFragments_OverwritesPrevious()
    {
        SaveSystem.SaveFragments(3);
        SaveSystem.SaveFragments(7);
        Assert.AreEqual(7, SaveSystem.LoadFragments());
    }

    // ─── HasSave ──────────────────────────────────────────────────────────

    [Test]
    public void HasSave_AfterDeleteAll_ReturnsFalse()
    {
        Assert.IsFalse(SaveSystem.HasSave());
    }

    [Test]
    public void HasSave_AfterSavingFragments_ReturnsTrue()
    {
        SaveSystem.SaveFragments(1);
        Assert.IsTrue(SaveSystem.HasSave());
    }

    // ─── Список собранных осколков ────────────────────────────────────────

    [Test]
    public void AddCollectedFragment_IsCollected_ReturnsTrue()
    {
        SaveSystem.AddCollectedFragment("shu");
        Assert.IsTrue(SaveSystem.IsFragmentCollected("shu"));
    }

    [Test]
    public void IsFragmentCollected_Unsaved_ReturnsFalse()
    {
        Assert.IsFalse(SaveSystem.IsFragmentCollected("shu"));
    }

    [Test]
    public void AddCollectedFragment_MultipleIds()
    {
        SaveSystem.AddCollectedFragment("shu");
        SaveSystem.AddCollectedFragment("ma");
        SaveSystem.AddCollectedFragment("ren");

        Assert.IsTrue(SaveSystem.IsFragmentCollected("shu"));
        Assert.IsTrue(SaveSystem.IsFragmentCollected("ma"));
        Assert.IsTrue(SaveSystem.IsFragmentCollected("ren"));
        Assert.IsFalse(SaveSystem.IsFragmentCollected("ting"));
    }

    [Test]
    public void LoadCollectedFragments_ReturnsAllIds()
    {
        SaveSystem.AddCollectedFragment("shu");
        SaveSystem.AddCollectedFragment("ma");

        HashSet<string> ids = SaveSystem.LoadCollectedFragments();

        Assert.AreEqual(2, ids.Count);
        Assert.IsTrue(ids.Contains("shu"));
        Assert.IsTrue(ids.Contains("ma"));
    }

    [Test]
    public void AddCollectedFragment_SameIdTwice_NotDuplicated()
    {
        SaveSystem.AddCollectedFragment("shu");
        SaveSystem.AddCollectedFragment("shu");

        // HashSet гарантирует уникальность
        HashSet<string> ids = SaveSystem.LoadCollectedFragments();
        Assert.AreEqual(1, ids.Count);
    }

    // ─── Позиция игрока ──────────────────────────────────────────────────

    [Test]
    public void SaveAndLoadPlayerPosition_ReturnsExactValues()
    {
        Vector3 pos = new Vector3(10.5f, -2.3f, 18.1f);
        SaveSystem.SavePlayerPosition(pos);

        Vector3 loaded = SaveSystem.LoadPlayerPosition(Vector3.zero);

        Assert.AreEqual(pos.x, loaded.x, 0.001f);
        Assert.AreEqual(pos.y, loaded.y, 0.001f);
        Assert.AreEqual(pos.z, loaded.z, 0.001f);
    }

    [Test]
    public void LoadPlayerPosition_WithoutSave_ReturnsDefault()
    {
        Vector3 defaultPos = new Vector3(1f, 2f, 3f);
        Vector3 loaded = SaveSystem.LoadPlayerPosition(defaultPos);

        Assert.AreEqual(defaultPos, loaded);
    }

    [Test]
    public void SavePlayerPosition_HasSaveBecomesTrue()
    {
        // Позиция сама по себе не считается "save" — но фрагменты считаются
        SaveSystem.SaveFragments(1);
        SaveSystem.SavePlayerPosition(Vector3.one);
        Assert.IsTrue(SaveSystem.HasSave());
    }

    // ─── Зона ─────────────────────────────────────────────────────────────

    [Test]
    public void SaveAndLoadZone_ReturnsCorrectValue()
    {
        SaveSystem.SaveZone(3);
        Assert.AreEqual(3, SaveSystem.LoadZone());
    }

    [Test]
    public void LoadZone_WithoutSave_ReturnsDefaultOne()
    {
        Assert.AreEqual(1, SaveSystem.LoadZone());
    }

    // ─── Сброс ────────────────────────────────────────────────────────────

    [Test]
    public void DeleteAll_RemovesAllData()
    {
        SaveSystem.SaveFragments(5);
        SaveSystem.SavePlayerPosition(Vector3.one * 10);
        SaveSystem.AddCollectedFragment("shu");
        SaveSystem.SaveZone(2);

        SaveSystem.DeleteAll();

        Assert.AreEqual(0, SaveSystem.LoadFragments());
        Assert.IsFalse(SaveSystem.HasSave());
        Assert.IsFalse(SaveSystem.IsFragmentCollected("shu"));
        Assert.AreEqual(1, SaveSystem.LoadZone());
    }

    // ─── Сериализация словаря ─────────────────────────────────────────────

    [Test]
    public void SaveAndLoadVocabulary_ReturnsAllEntries()
    {
        var entries = new List<VocabularyEntry>
        {
            new VocabularyEntry { hanzi = "树", pinyin = "shù",  translation = "дерево"  },
            new VocabularyEntry { hanzi = "马", pinyin = "mǎ",   translation = "лошадь"  },
            new VocabularyEntry { hanzi = "母", pinyin = "mǔ",   translation = "мать"    }
        };

        SaveSystem.SaveVocabulary(entries);
        var loaded = SaveSystem.LoadVocabulary();

        Assert.IsNotNull(loaded);
        Assert.AreEqual(3, loaded.Count);
        Assert.AreEqual("树", loaded[0].hanzi);
        Assert.AreEqual("дерево", loaded[0].translation);
        Assert.AreEqual("mǎ", loaded[1].pinyin);
    }

    [Test]
    public void LoadVocabulary_WithoutSave_ReturnsNull()
    {
        var loaded = SaveSystem.LoadVocabulary();
        Assert.IsNull(loaded);
    }

    [Test]
    public void SaveVocabulary_EmptyList_LoadsEmpty()
    {
        SaveSystem.SaveVocabulary(new List<VocabularyEntry>());
        var loaded = SaveSystem.LoadVocabulary();

        Assert.IsNotNull(loaded);
        Assert.AreEqual(0, loaded.Count);
    }
}
