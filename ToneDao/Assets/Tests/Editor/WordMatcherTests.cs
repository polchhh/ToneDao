using NUnit.Framework;

/// <summary>
/// Unit-тесты для WordMatcher — логика сопоставления распознанного слова
/// с целевым иероглифом / пиньинем.
///
/// Запуск: Window → General → Test Runner → EditMode → Run All
/// </summary>
[TestFixture]
public class WordMatcherTests
{
    // ─── Точное совпадение иероглифа ──────────────────────────────────────

    [Test]
    public void ExactHanziMatch_ReturnsTrue()
    {
        bool result = WordMatcher.IsCorrect("树", "树", "樹", "shu");
        Assert.IsTrue(result);
    }

    [Test]
    public void TraditionalHanziMatch_ReturnsTrue()
    {
        bool result = WordMatcher.IsCorrect("樹", "树", "樹", "shu");
        Assert.IsTrue(result);
    }

    [Test]
    public void DifferentHanzi_ReturnsFalse()
    {
        bool result = WordMatcher.IsCorrect("猫", "树", "樹", "shu");
        Assert.IsFalse(result);
    }

    // ─── Совпадение по пиньиню ────────────────────────────────────────────

    [Test]
    public void PinyinExactMatch_ReturnsTrue()
    {
        bool result = WordMatcher.IsCorrect("shu", "树", "樹", "shu");
        Assert.IsTrue(result);
    }

    [Test]
    public void PinyinCaseInsensitive_ReturnsTrue()
    {
        bool result = WordMatcher.IsCorrect("SHU", "树", "樹", "shu");
        Assert.IsTrue(result);
    }

    [Test]
    public void DifferentPinyin_ReturnsFalse()
    {
        bool result = WordMatcher.IsCorrect("mao", "树", "樹", "shu");
        Assert.IsFalse(result);
    }

    // ─── Содержание иероглифа в распознанной фразе ────────────────────────

    [Test]
    public void HanziInLongerPhrase_ReturnsTrue()
    {
        // Wit.ai может вернуть «这是树» вместо просто «树»
        bool result = WordMatcher.IsCorrect("这是树", "树", "樹", "shu");
        Assert.IsTrue(result);
    }

    [Test]
    public void PinyinInLongerPhrase_ReturnsTrue()
    {
        bool result = WordMatcher.IsCorrect("zhe shi shu", "树", "樹", "shu");
        Assert.IsTrue(result);
    }

    // ─── Обработка пробелов и регистра ────────────────────────────────────

    [Test]
    public void TrimsWhitespace()
    {
        bool result = WordMatcher.IsCorrect("  树  ", "树", "樹", "shu");
        Assert.IsTrue(result);
    }

    [Test]
    public void HandlesTabsAndNewlines()
    {
        bool result = WordMatcher.IsCorrect("\t树\n", "树", "樹", "shu");
        Assert.IsTrue(result);
    }

    // ─── Граничные случаи ─────────────────────────────────────────────────

    [Test]
    public void EmptyString_ReturnsFalse()
    {
        bool result = WordMatcher.IsCorrect("", "树", "樹", "shu");
        Assert.IsFalse(result);
    }

    [Test]
    public void NullString_ReturnsFalse()
    {
        bool result = WordMatcher.IsCorrect(null, "树", "樹", "shu");
        Assert.IsFalse(result);
    }

    [Test]
    public void OnlyWhitespace_ReturnsFalse()
    {
        bool result = WordMatcher.IsCorrect("   ", "树", "樹", "shu");
        Assert.IsFalse(result);
    }

    [Test]
    public void EmptyTargetHanzi_FallsBackToPinyin()
    {
        bool result = WordMatcher.IsCorrect("shu", "", "", "shu");
        Assert.IsTrue(result);
    }

    [Test]
    public void AllTargetsEmpty_ReturnsFalse()
    {
        bool result = WordMatcher.IsCorrect("anything", "", "", "");
        Assert.IsFalse(result);
    }

    // ─── Тоновые знаки в распознанном слове ───────────────────────────────

    [Test]
    public void RecognizedWithToneMarks_DoesNotMatchUntonedPinyin()
    {
        // shù (с тоном) — другая строка чем shu (без тона)
        // Это документирует ограничение: Wit.ai обычно возвращает БЕЗ тонов,
        // но если вернёт с тонами — нужно вручную добавить в hanziAlternate
        bool result = WordMatcher.IsCorrect("shù", "树", "樹", "shu");
        Assert.IsFalse(result, "Тоновый пиньинь не совпадает с не-тоновым (известное поведение)");
    }
}
