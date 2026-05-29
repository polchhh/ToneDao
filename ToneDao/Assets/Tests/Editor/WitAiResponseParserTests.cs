using NUnit.Framework;

/// <summary>
/// Unit-тесты для WitAiResponseParser — парсер JSON-ответа Wit.ai Speech API.
/// Wit.ai возвращает streaming-формат: несколько JSON-объектов подряд,
/// финальный текст в последнем (с is_final = true).
/// </summary>
[TestFixture]
public class WitAiResponseParserTests
{
    // ─── Корректные ответы ────────────────────────────────────────────────

    [Test]
    public void SimpleSingleObject_ReturnsText()
    {
        string json = "{\"text\": \"树\", \"is_final\": true}";
        string result = WitAiResponseParser.Parse(json);
        Assert.AreEqual("树", result);
    }

    [Test]
    public void EnglishText_Parsed()
    {
        string json = "{\"text\": \"tree\", \"intents\": []}";
        string result = WitAiResponseParser.Parse(json);
        Assert.AreEqual("tree", result);
    }

    [Test]
    public void StreamingResponse_ReturnsLastText()
    {
        // Wit.ai возвращает 3 JSON-объекта подряд — берём последний
        string json = "{\"text\": \"shu\", \"is_final\": false}" +
                      "{\"text\": \"shù\", \"is_final\": false}" +
                      "{\"text\": \"树\", \"is_final\": true}";
        string result = WitAiResponseParser.Parse(json);
        Assert.AreEqual("树", result);
    }

    [Test]
    public void MultipleTextFieldsInNestedObjects_ReturnsLastOne()
    {
        // Сложный кейс: вложенный объект тоже содержит "text" — но мы хотим финальный
        string json = "{\"intents\": [{\"text\": \"intent_name\"}], \"text\": \"final_text\", \"is_final\": true}";
        string result = WitAiResponseParser.Parse(json);
        Assert.AreEqual("final_text", result);
    }

    [Test]
    public void ChinesePhraseWithSpaces_Preserved()
    {
        string json = "{\"text\": \"这是 一只 猫\", \"is_final\": true}";
        string result = WitAiResponseParser.Parse(json);
        Assert.AreEqual("这是 一只 猫", result);
    }

    // ─── Граничные случаи ─────────────────────────────────────────────────

    [Test]
    public void EmptyText_ReturnsNull()
    {
        string json = "{\"text\": \"\", \"is_final\": true}";
        string result = WitAiResponseParser.Parse(json);
        Assert.IsNull(result);
    }

    [Test]
    public void NoTextField_ReturnsNull()
    {
        string json = "{\"intents\": [], \"is_final\": true}";
        string result = WitAiResponseParser.Parse(json);
        Assert.IsNull(result);
    }

    [Test]
    public void EmptyJson_ReturnsNull()
    {
        string result = WitAiResponseParser.Parse("");
        Assert.IsNull(result);
    }

    [Test]
    public void NullJson_ReturnsNull()
    {
        string result = WitAiResponseParser.Parse(null);
        Assert.IsNull(result);
    }

    [Test]
    public void MalformedJsonWithoutColon_ReturnsNull()
    {
        string json = "{\"text\" \"value\"}";   // нет двоеточия
        string result = WitAiResponseParser.Parse(json);
        Assert.IsNull(result);
    }

    [Test]
    public void MalformedJsonWithoutClosingQuote_ReturnsNull()
    {
        string json = "{\"text\": \"unclosed";
        string result = WitAiResponseParser.Parse(json);
        Assert.IsNull(result);
    }
}
