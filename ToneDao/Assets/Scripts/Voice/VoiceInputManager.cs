using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class VoiceInputManager : MonoBehaviour
{
    public enum ToneType { None = 0, Tone1 = 1, Tone2 = 2, Tone3 = 3, Tone4 = 4 }

    public event Action<ToneType> OnToneActive;

    public event Action<ToneType> OnToneDetected;

    public event Action OnSilenceDetected;

    public event Action<float[]> OnF0CurveUpdated;

    public event Action<string> OnWordRecognized;

    public int frequencyMin = 40;
    public int frequencyMax = 400;
    public int harmonicsToUse = 5;
    public float smoothingWidth = 500f;
    public float thresholdSRH = 9f;

    public float minVoicedHz = 60f;
    public float maxVoicedHz = 600f;
    public float srhEnter = 9f;
    public float srhExit = 6.5f;

    [Tooltip("0.01 = 10мс — как в оригинале")]
    public float hopSeconds = 0.01f;

    public float bridgeGapSeconds = 0.1f;
    public float endGapSeconds = 0.2f;
    public float minSyllableSeconds = 0.08f;
    public float maxSyllableSeconds = 0.65f;
    public int recentWindow = 200;

   
    public float activeIntervalSeconds = 0.10f;

    public int sampleRate = 8192;
    public int clipLengthSeconds = 10;

    const int spectrumSize = 1024;
    const int outputResolution = 200;

    float[] spectrum = new float[spectrumSize];
    float[] specRaw = new float[spectrumSize];
    float[] specCum = new float[spectrumSize];
    float[] specRes = new float[spectrumSize];

    private AudioSource src;
    private double nextHopDsp;

    private readonly List<float> segTimes = new();
    private readonly List<float> segMidi = new();

    private bool inSyllable;
    private double segStartDsp;
    private double gapStartDsp = -1;
    private bool voicedState;

    private readonly Queue<float> recentMidi = new();
    private float speakerMedian = 60f;

    private float lastActiveTime = -999f;

    private bool freshAfterMaxDur = false;

    private bool _paused = false;

    public bool IsPaused => _paused;

    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused)
        {

            inSyllable = false;
            gapStartDsp = -1;
            segMidi.Clear();
            segTimes.Clear();
            lastActiveTime = -999f;
        }
    }

    public AudioClip MicClip => src?.clip;

    public string MicDeviceName => Microphone.devices.Length > 0 ? Microphone.devices[0] : null;

    private void Awake()
    {
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        src = GetComponent<AudioSource>();
        src.loop = true;
        src.playOnAwake = false;
        src.volume = 0.01f;
    }

    private IEnumerator Start()
    {

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[VoiceInputManager] Микрофон не найден.");
            yield break;
        }

        string micName = Microphone.devices[0];
        src.clip = Microphone.Start(micName, true, clipLengthSeconds, sampleRate);

        float timeout = 2f;
        float t0 = Time.realtimeSinceStartup;
        while (Microphone.GetPosition(micName) <= 0)
        {
            if (Time.realtimeSinceStartup - t0 > timeout)
            {
                Debug.LogError("[VoiceInputManager] Таймаут запуска микрофона.");
                yield break;
            }
            yield return null;
        }

        src.Play();
        nextHopDsp = AudioSettings.dspTime;
        srhExit = Mathf.Min(srhExit, srhEnter);
    }

    private void OnDisable()
    {
        if (Microphone.devices.Length > 0 && Microphone.IsRecording(Microphone.devices[0]))
            Microphone.End(Microphone.devices[0]);
    }

    private void Update()
    {
        if (_paused) return;

        double nowDsp = AudioSettings.dspTime;
        if (nowDsp < nextHopDsp) return;
        nextHopDsp += hopSeconds;

        if (src == null || src.clip == null || !src.isPlaying) return;

        float bestSRH;
        float f0 = Estimate(out bestSRH);

        bool f0Ok = !float.IsNaN(f0) && f0 >= minVoicedHz && f0 <= maxVoicedHz;

        bool srhOk = !voicedState
            ? (!float.IsNaN(bestSRH) && bestSRH >= srhEnter)
            : (!float.IsNaN(bestSRH) && bestSRH >= srhExit);

        voicedState = f0Ok && srhOk;

        if (voicedState)
        {
            if (!inSyllable)
            {
                inSyllable = true;
                segStartDsp = nowDsp;
                gapStartDsp = -1;
                segTimes.Clear();
                segMidi.Clear();
            }

            gapStartDsp = -1;

            float midi = HzToMidi(f0);
            recentMidi.Enqueue(midi);
            while (recentMidi.Count > recentWindow) recentMidi.Dequeue();
            speakerMedian = ApproxMedian(recentMidi);

            float normMidi = midi - speakerMedian;
            segTimes.Add((float)nowDsp);
            segMidi.Add(normMidi);

            OnF0CurveUpdated?.Invoke(segMidi.ToArray());

            if (segMidi.Count >= 3 && Time.time - lastActiveTime >= activeIntervalSeconds)
            {
                ToneType active = ClassifyTone(segMidi);
                OnToneActive?.Invoke(active);
                lastActiveTime = Time.time;
            }

            if (nowDsp - segStartDsp >= maxSyllableSeconds && segMidi.Count >= 3)
            {
                ToneType tone = ClassifyTone(segMidi);
                Debug.Log($"[VoiceInputManager] Тон: {tone} (точек={segMidi.Count}, maxDur сброс)");
                OnToneDetected?.Invoke(tone);

                segMidi.Clear();
                segTimes.Clear();
                segStartDsp = nowDsp;
                lastActiveTime = -999f;
                freshAfterMaxDur = true;
            }
        }
        else
        {
            if (!inSyllable) return;

            if (gapStartDsp < 0) gapStartDsp = nowDsp;

            double gap = nowDsp - gapStartDsp;
            double dur = nowDsp - segStartDsp;

            if (gap < bridgeGapSeconds) return;

            if (gap >= endGapSeconds)
            {

                bool isTailArtifact = freshAfterMaxDur && segMidi.Count < 8;
                freshAfterMaxDur = false;

                if (!isTailArtifact && dur >= minSyllableSeconds && segMidi.Count >= 3)
                {

                    int useCount = Mathf.Min(segMidi.Count, 30);
                    var window = segMidi.GetRange(segMidi.Count - useCount, useCount);
                    ToneType tone = ClassifyTone(window);
                    Debug.Log($"[VoiceInputManager] Тон: {tone} (окно={useCount}/{segMidi.Count}, длина={dur:F2}с)");
                    OnToneDetected?.Invoke(tone);
                }
                else if (isTailArtifact)
                {
                    Debug.Log($"[VoiceInputManager] Хвост после Тона 1 проигнорирован (n={segMidi.Count})");
                }

                OnSilenceDetected?.Invoke();

                inSyllable = false;
                gapStartDsp = -1;
                lastActiveTime = -999f;
                segTimes.Clear();
                segMidi.Clear();
            }
        }
    }

    private float Estimate(out float bestSRH)
    {
        bestSRH = float.NaN;
        if (!src.isPlaying) return float.NaN;

        float nyquistFreq = AudioSettings.outputSampleRate / 2.0f;

        src.GetSpectrumData(spectrum, 0, FFTWindow.Hanning);

        for (int i = 0; i < spectrumSize; i++)
            specRaw[i] = Mathf.Log(spectrum[i] + 1e-9f);

        specCum[0] = 0f;
        for (int i = 1; i < spectrumSize; i++)
            specCum[i] = specCum[i - 1] + specRaw[i];

        int halfRange = Mathf.RoundToInt((smoothingWidth * 0.5f) / nyquistFreq * spectrumSize);
        halfRange = Mathf.Clamp(halfRange, 1, spectrumSize / 2);

        for (int i = 0; i < spectrumSize; i++)
        {
            int indexUpper = Mathf.Min(i + halfRange, spectrumSize - 1);
            int indexLower = Mathf.Max(i - halfRange + 1, 0);
            int width = Mathf.Max(1, indexUpper - indexLower);
            float smoothed = (specCum[indexUpper] - specCum[indexLower]) / width;
            specRes[i] = specRaw[i] - smoothed;
        }

        float bestFreq = 0f;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < outputResolution; i++)
        {
            float freq = (float)i / (outputResolution - 1) * (frequencyMax - frequencyMin) + frequencyMin;
            float score = GetSpectrumAmplitude(freq, nyquistFreq);
            for (int h = 2; h <= harmonicsToUse; h++)
            {
                score += GetSpectrumAmplitude(freq * h, nyquistFreq);
                score -= GetSpectrumAmplitude(freq * (h - 0.5f), nyquistFreq);
            }
            if (score > bestScore) { bestScore = score; bestFreq = freq; }
        }

        bestSRH = bestScore;
        if (bestScore < thresholdSRH) return float.NaN;
        return bestFreq;
    }

    private float GetSpectrumAmplitude(float frequency, float nyquistFreq)
    {
        if (frequency <= 0f) return specRes[0];
        if (frequency >= nyquistFreq) return specRes[^1];
        float position = frequency / nyquistFreq * (spectrumSize - 1);
        int index0 = Mathf.Clamp((int)position, 0, spectrumSize - 1);
        int index1 = Mathf.Clamp(index0 + 1, 0, spectrumSize - 1);
        return Mathf.Lerp(specRes[index0], specRes[index1], position - index0);
    }

    private static ToneType ClassifyTone(List<float> contourNormMidi)
    {

        if (contourNormMidi.Count < 8)
        {
            float s = contourNormMidi[^1] - contourNormMidi[0];
            float minV = Min(contourNormMidi);
            int minI = ArgMin(contourNormMidi);
            float minP = (float)minI / Mathf.Max(1, contourNormMidi.Count - 1);

            bool t3 = minP > 0.15f && minP < 0.85f
                   && (contourNormMidi[0] - minV) > 0.5f
                   && (contourNormMidi[^1] - minV) > 0.3f;

            ToneType t = t3 ? ToneType.Tone3
                       : s <= -0.3f ? ToneType.Tone4
                       : s >= 0.3f ? ToneType.Tone2
                       : ToneType.Tone1;

            Debug.Log($"[Classify SHORT n={contourNormMidi.Count}] slope={s:F2} minP={minP:F2} minV={minV:F2} t3={t3} → {t}");
            return t;
        }

        var sm = Smooth(contourNormMidi, 3);

        float start = SampleAt(sm, 0.0f);
        float mid = SampleAt(sm, 0.5f);
        float end = SampleAt(sm, 1.0f);
        float q1 = SampleAt(sm, 0.25f);
        float q3 = SampleAt(sm, 0.75f);

        float slope = end - start;
        float range = Max(sm) - Min(sm);
        float minVal = Min(sm);
        int minIdx = ArgMin(sm);
        float minPos = (float)minIdx / Mathf.Max(1, sm.Count - 1);

        float riseStrong = 1.0f;
        float fallStrong = 0.8f;
        float dipMin = 0.4f;
        float edgeGuard = 0.18f;

        bool minNotAtEdge = minPos > edgeGuard && minPos < 1f - edgeGuard;
        bool hasFall = (mid - start) < -fallStrong || (q1 - start) < -0.4f;
        bool hasRise = (end - mid) > 0.4f || (end - q3) > 0.2f
                                  || (q3 - minVal) > 0.4f;
        bool deepDipRelativeToEnds = (start - minVal) > dipMin && (end - minVal) > 0.2f;

        bool is3 = minNotAtEdge && hasFall && hasRise && deepDipRelativeToEnds && range > 0.9f;
        bool is2 = slope > riseStrong && !hasFall && q3 > q1 + 0.4f;
        bool is4 = slope < -riseStrong && q1 > q3 + 0.4f;

        ToneType result = is3 ? ToneType.Tone3
                        : is2 ? ToneType.Tone2
                        : is4 ? ToneType.Tone4
                        : ToneType.Tone1;

        Debug.Log(
            $"[Classify n={contourNormMidi.Count}] " +
            $"st={start:F2} q1={q1:F2} mid={mid:F2} q3={q3:F2} end={end:F2} | " +
            $"slope={slope:F2} range={range:F2} minPos={minPos:F2} | " +
            $"notEdge={minNotAtEdge} fall={hasFall} rise={hasRise} dip={deepDipRelativeToEnds} | " +
            $"→ {result}  (is3={is3} is2={is2} is4={is4})"
        );

        return result;
    }

    private static float HzToMidi(float hz) => 69f + 12f * Mathf.Log(hz / 440f, 2f);

    private static float ApproxMedian(IEnumerable<float> values)
    {
        var list = new List<float>(values);
        if (list.Count == 0) return 60f;
        list.Sort();
        int m = list.Count / 2;
        return list.Count % 2 == 1 ? list[m] : 0.5f * (list[m - 1] + list[m]);
    }

    private static List<float> Smooth(List<float> x, int radius)
    {
        int n = x.Count;
        var y = new List<float>(n);
        for (int i = 0; i < n; i++)
        {
            int a = Mathf.Max(0, i - radius), b = Mathf.Min(n - 1, i + radius);
            float s = 0f; int c = 0;
            for (int k = a; k <= b; k++) { s += x[k]; c++; }
            y.Add(s / c);
        }
        return y;
    }

    private static float SampleAt(List<float> x, float t01)
    {
        if (x.Count == 0) return 0f;
        if (x.Count == 1) return x[0];
        float pos = Mathf.Clamp01(t01) * (x.Count - 1);
        int i0 = Mathf.FloorToInt(pos);
        int i1 = Mathf.Min(i0 + 1, x.Count - 1);
        return Mathf.Lerp(x[i0], x[i1], pos - i0);
    }

    private static float Min(List<float> x)
    { float m = float.PositiveInfinity; foreach (var v in x) m = Mathf.Min(m, v); return m; }

    private static float Max(List<float> x)
    { float m = float.NegativeInfinity; foreach (var v in x) m = Mathf.Max(m, v); return m; }

    private static int ArgMin(List<float> x)
    {
        int mi = 0; float mv = float.PositiveInfinity;
        for (int i = 0; i < x.Count; i++) if (x[i] < mv) { mv = x[i]; mi = i; }
        return mi;
    }

    public float[] GetF0Curve() => segMidi.ToArray();
    public void SetRecognitionVocabulary(IEnumerable<string> _) { }
    public void SimulateWordRecognized(string word) => OnWordRecognized?.Invoke(word);
}
