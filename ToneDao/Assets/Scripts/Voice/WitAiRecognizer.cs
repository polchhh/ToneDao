using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class WitAiRecognizer : MonoBehaviour
{
    public string witAiToken = "ТОКЕН";

    public float recordDurationSec = 2.5f;

    public event Action<string> OnWordRecognized;
    public event Action<string> OnError;

    public bool IsRecording => _isRecording;
    private bool _isRecording = false;

    public void StartRecognition()
    {
        if (_isRecording) return;
        StartCoroutine(RecordAndRecognize());
    }

    private IEnumerator RecordAndRecognize()
    {
        _isRecording = true;

        var vim = FindFirstObjectByType<VoiceInputManager>();

        string device = vim?.MicDeviceName;
        AudioClip clip = vim?.MicClip;

        if (string.IsNullOrEmpty(device) || clip == null)
        {
            OnError?.Invoke("Микрофон недоступен (VoiceInputManager не запущен)");
            _isRecording = false;
            yield break;
        }

        int startPos = Microphone.GetPosition(device);

        Debug.Log($"[WitAi] Запись {recordDurationSec}с начиная с позиции {startPos}...");
        yield return new WaitForSeconds(recordDurationSec);

        int endPos = Microphone.GetPosition(device);

        byte[] wavBytes = ExtractSegmentAsWav(clip, startPos, endPos, device);

        if (wavBytes == null)
        {
            OnError?.Invoke("Ошибка извлечения аудио из буфера");
            _isRecording = false;
            yield break;
        }

        yield return SendToWitAi(wavBytes);

        _isRecording = false;
    }

    private byte[] ExtractSegmentAsWav(AudioClip clip, int startPos, int endPos, string device)
    {
        if (clip == null) return null;

        int totalSamples = clip.samples;
        int channels = clip.channels;
        int sampleRate = clip.frequency;

        int count = endPos > startPos
            ? endPos - startPos
            : totalSamples - startPos + endPos;

        if (count <= 0)
        {
            Debug.LogWarning("[WitAi] Нет данных для отправки.");
            return null;
        }

        float[] allSamples = new float[totalSamples * channels];
        clip.GetData(allSamples, 0);

        float[] mono = new float[count];
        for (int i = 0; i < count; i++)
        {
            int src = ((startPos + i) % totalSamples) * channels;
            float s = 0f;
            for (int ch = 0; ch < channels; ch++)
                s += allSamples[src + ch];
            mono[i] = s / channels;
        }

        short[] pcm = new short[count];
        byte[] pcmBytes = new byte[count * 2];
        for (int i = 0; i < count; i++)
            pcm[i] = (short)(Mathf.Clamp(mono[i], -1f, 1f) * 32767f);
        Buffer.BlockCopy(pcm, 0, pcmBytes, 0, pcmBytes.Length);

        return BuildWavHeader(pcmBytes, 1, sampleRate);
    }

    private IEnumerator SendToWitAi(byte[] wavBytes)
    {
        string url = "https://api.wit.ai/speech?v=20240101";

        using var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(wavBytes);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", "Bearer " + witAiToken);
        request.SetRequestHeader("Content-Type", "audio/wav");

        Debug.Log($"[WitAi] Отправляю {wavBytes.Length} байт...");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[WitAi] Ошибка сети: {request.error}");
            OnError?.Invoke($"Ошибка сети: {request.error}");
            yield break;
        }

        string json = request.downloadHandler.text;
        Debug.Log($"[WitAi] Ответ: {json}");
        string recognized = WitAiResponseParser.Parse(json);

        if (!string.IsNullOrEmpty(recognized))
        {
            Debug.Log($"[WitAi] Распознано: '{recognized}'");
            OnWordRecognized?.Invoke(recognized.ToLower().Trim());
        }
        else
        {
            OnError?.Invoke("Слово не распознано");
        }
    }

    private byte[] BuildWavHeader(byte[] pcmData, int channels, int sampleRate)
    {
        int byteRate = sampleRate * channels * 2;
        int totalSize = 36 + pcmData.Length;
        byte[] wav = new byte[44 + pcmData.Length];

        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(totalSize).CopyTo(wav, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);
        BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
        BitConverter.GetBytes(byteRate).CopyTo(wav, 28);
        BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32);
        BitConverter.GetBytes((short)16).CopyTo(wav, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(pcmData.Length).CopyTo(wav, 40);
        pcmData.CopyTo(wav, 44);

        return wav;
    }
}
