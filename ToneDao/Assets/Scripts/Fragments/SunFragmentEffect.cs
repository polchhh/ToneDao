using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SunFragmentEffect : MonoBehaviour
{
    public Renderer[] treeRenderers;
    public float colorRestoreDuration = 1.5f;

    public Transform fragmentRoot;
    public Light fragmentLight;
    public float floatHeight = 2f;
    public float floatDuration = 0.7f;
    public float glowIntensity = 4f;
    public float spinDuration = 0.8f;
    public float spinSpeed = 720f;
    public float flyDuration = 2.5f;

    public float idleBobHeight = 0.15f;
    public float idleBobSpeed = 1.2f;
    public float idleRotateSpeed = 25f;

    public Vector2 inventoryScreenPos = new Vector2(0.95f, 0.95f);
    public float inventoryCameraDistance = 8f;

    public ParticleSystem activationBurst;
    public ParticleSystem fragmentGlowPS;
    public ParticleSystem flyTrailPS;

    [Range(0f, 1f)] public float grayBrightness = 0.64f;

    public float glowBoostIntensity = 6f;
    public Color glowBoostColor = new Color(2.5f, 1.8f, 0.3f);
    public float glowPulseAmplitude = 2f;
    public float glowPulseSpeed = 1.8f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    private Color[] _originalColors;
    private int[] _colorPropIds;
    private Texture[] _originalTextures;
    private int[] _texPropIds;

    private Dictionary<Texture, Texture2D> _grayTextureCache = new();

    private List<Texture2D> _createdTextures = new();

    private bool _activated = false;
    private bool _glowBoosted = false;
    private Coroutine _idleCoroutine;

    private void Awake()
    {

        CacheOriginalColors();

        if (fragmentLight != null)
            fragmentLight.intensity = 0f;
    }

    private void Start()
    {
        if (fragmentRoot != null)
            _idleCoroutine = StartCoroutine(IdleAnimation());
    }

    private void OnDestroy()
    {

        foreach (var tex in _createdTextures)
            if (tex != null) Destroy(tex);
    }

    public void Activate(Action onComplete = null)
    {
        _activated = true;
        if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);
        StartCoroutine(ActivationSequence(onComplete));
    }

    private Texture2D CreateGrayTexture(Texture source)
    {
        if (_grayTextureCache.TryGetValue(source, out var cached))
            return cached;

        var rt = RenderTexture.GetTemporary(
            source.width, source.height, 0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);

        Graphics.Blit(source, rt);

        var prevRT = RenderTexture.active;
        RenderTexture.active = rt;

        var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);

        RenderTexture.active = prevRT;
        RenderTexture.ReleaseTemporary(rt);

        byte grayByte = (byte)Mathf.RoundToInt(grayBrightness * 255f);
        var pixels = result.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i].r = grayByte;
            pixels[i].g = grayByte;
            pixels[i].b = grayByte;

        }
        result.SetPixels32(pixels);
        result.Apply();

        _grayTextureCache[source] = result;
        _createdTextures.Add(result);
        return result;
    }

    private void CacheOriginalColors()
    {
        if (treeRenderers == null || treeRenderers.Length == 0) return;

        _originalColors = new Color[treeRenderers.Length];
        _colorPropIds = new int[treeRenderers.Length];
        _originalTextures = new Texture[treeRenderers.Length];
        _texPropIds = new int[treeRenderers.Length];

        for (int i = 0; i < treeRenderers.Length; i++)
        {
            if (treeRenderers[i] == null) continue;
            var mat = treeRenderers[i].sharedMaterial;

            if (mat.HasProperty(BaseColorId))
            {
                _colorPropIds[i] = BaseColorId;
                _originalColors[i] = mat.GetColor(BaseColorId);
            }
            else if (mat.HasProperty(ColorId))
            {
                _colorPropIds[i] = ColorId;
                _originalColors[i] = mat.GetColor(ColorId);
            }
            else
            {
                _colorPropIds[i] = BaseColorId;
                _originalColors[i] = Color.white;
            }

            if (mat.HasProperty(BaseMapId) && mat.GetTexture(BaseMapId) != null)
            {
                _texPropIds[i] = BaseMapId;
                _originalTextures[i] = mat.GetTexture(BaseMapId);
            }
            else if (mat.HasProperty(MainTexId) && mat.GetTexture(MainTexId) != null)
            {
                _texPropIds[i] = MainTexId;
                _originalTextures[i] = mat.GetTexture(MainTexId);
            }
            else
            {
                _texPropIds[i] = BaseMapId;
                _originalTextures[i] = null;
            }
        }
    }

    public void BoostGlow()
    {
        if (_activated || _glowBoosted) return;
        _glowBoosted = true;

        if (fragmentRoot != null)
        {
            var renderers = fragmentRoot.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                var pb = new MaterialPropertyBlock();
                r.GetPropertyBlock(pb);

                pb.SetColor("_EmissionColor", glowBoostColor);
                r.SetPropertyBlock(pb);
            }
        }

        if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);
        if (!_activated)
            _idleCoroutine = StartCoroutine(IdleAnimationGlowing());
    }

    public void MakeGray()
    {
        if (_originalColors == null) CacheOriginalColors();
        if (treeRenderers == null) return;

        for (int i = 0; i < treeRenderers.Length; i++)
        {
            if (treeRenderers[i] == null) continue;

            var pb = new MaterialPropertyBlock();
            if (_originalTextures[i] != null)
                pb.SetTexture(_texPropIds[i], CreateGrayTexture(_originalTextures[i]));

            pb.SetColor(_colorPropIds[i], new Color(1f, 1f, 1f, _originalColors[i].a));
            treeRenderers[i].SetPropertyBlock(pb);
        }
    }

    private IEnumerator RestoreTreeColor()
    {
        if (treeRenderers == null || _originalColors == null) yield break;

        float elapsed = 0f;
        while (elapsed < colorRestoreDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / colorRestoreDuration);

            for (int i = 0; i < treeRenderers.Length; i++)
            {
                if (treeRenderers[i] == null) continue;

                var pb = new MaterialPropertyBlock();

                if (_originalTextures[i] != null)
                {

                    if (t < 0.5f)
                    {
                        pb.SetTexture(_texPropIds[i], CreateGrayTexture(_originalTextures[i]));
                        Color tint = Color.Lerp(Color.white, _originalColors[i], t * 2f);
                        pb.SetColor(_colorPropIds[i], tint);
                    }
                    else
                    {
                        pb.SetTexture(_texPropIds[i], _originalTextures[i]);
                        Color gray = new Color(grayBrightness, grayBrightness, grayBrightness, _originalColors[i].a);
                        Color tint = Color.Lerp(gray, _originalColors[i], (t - 0.5f) * 2f);
                        pb.SetColor(_colorPropIds[i], tint);
                    }
                }
                else
                {
                    Color gray = new Color(grayBrightness, grayBrightness, grayBrightness, _originalColors[i].a);
                    pb.SetColor(_colorPropIds[i], Color.Lerp(gray, _originalColors[i], t));
                }

                treeRenderers[i].SetPropertyBlock(pb);
            }
            yield return null;
        }

        foreach (var r in treeRenderers)
            r?.SetPropertyBlock(null);
    }

    private IEnumerator IdleAnimation()
    {
        if (fragmentRoot == null) yield break;
        Vector3 basePos = fragmentRoot.localPosition;
        while (!_activated)
        {
            float bob = Mathf.Sin(Time.time * idleBobSpeed) * idleBobHeight;
            fragmentRoot.localPosition = basePos + Vector3.up * bob;
            fragmentRoot.Rotate(Vector3.up, idleRotateSpeed * Time.deltaTime, Space.World);
            yield return null;
        }
        fragmentRoot.localPosition = basePos;
    }

    private IEnumerator IdleAnimationGlowing()
    {
        if (fragmentRoot == null) yield break;
        Vector3 basePos = fragmentRoot.localPosition;

        while (!_activated)
        {
            float t = Time.time;
            float bob = Mathf.Sin(t * idleBobSpeed) * idleBobHeight;
            fragmentRoot.localPosition = basePos + Vector3.up * bob;
            fragmentRoot.Rotate(Vector3.up, idleRotateSpeed * Time.deltaTime, Space.World);

            if (fragmentLight != null)
            {
                float pulse = glowBoostIntensity
                    + Mathf.Sin(t * glowPulseSpeed * Mathf.PI) * glowPulseAmplitude;
                fragmentLight.intensity = Mathf.Max(0f, pulse);
                fragmentLight.color = glowBoostColor;
            }

            yield return null;
        }

        fragmentRoot.localPosition = basePos;
    }

    private IEnumerator ActivationSequence(Action onComplete)
    {
        activationBurst?.Play();
        fragmentGlowPS?.Play();

        StartCoroutine(RestoreTreeColor());
        yield return StartCoroutine(FloatUp());
        yield return StartCoroutine(SpinInPlace());

        fragmentGlowPS?.Stop();
        flyTrailPS?.Play();
        yield return StartCoroutine(FlyToInventory());

        onComplete?.Invoke();
    }

    private IEnumerator FloatUp()
    {
        if (fragmentRoot == null) yield break;
        Vector3 startPos = fragmentRoot.position;
        Vector3 endPos = startPos + Vector3.up * floatHeight;
        float elapsed = 0f;
        while (elapsed < floatDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / floatDuration);
            fragmentRoot.position = Vector3.Lerp(startPos, endPos, t);
            if (fragmentLight != null)
                fragmentLight.intensity = Mathf.Lerp(0f, glowIntensity, t);
            fragmentRoot.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);
            yield return null;
        }
    }

    private IEnumerator SpinInPlace()
    {
        if (fragmentRoot == null) yield break;
        float elapsed = 0f;
        while (elapsed < spinDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spinDuration;
            float speedMult = Mathf.SmoothStep(0.3f, 1f, Mathf.Sin(t * Mathf.PI));
            fragmentRoot.Rotate(Vector3.up, spinSpeed * speedMult * Time.deltaTime, Space.World);
            float tilt = Mathf.Sin(t * Mathf.PI * 3f) * 15f;
            fragmentRoot.localRotation = Quaternion.Euler(0f, fragmentRoot.localEulerAngles.y, tilt);
            if (fragmentLight != null)
                fragmentLight.intensity = glowIntensity * (0.7f + 0.3f * Mathf.Sin(t * Mathf.PI * 6f));
            yield return null;
        }
    }

    private IEnumerator FlyToInventory()
    {
        if (fragmentRoot == null) yield break;
        Camera cam = Camera.main;
        Vector3 targetWorld = fragmentRoot.position;
        if (cam != null)
        {
            Vector3 screenPt = new Vector3(
                inventoryScreenPos.x * Screen.width,
                inventoryScreenPos.y * Screen.height,
                inventoryCameraDistance);
            targetWorld = cam.ScreenToWorldPoint(screenPt);
        }
        Vector3 startPos = fragmentRoot.position;
        Vector3 startScale = fragmentRoot.localScale;
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flyDuration);
            Vector3 arc = Vector3.Lerp(startPos, targetWorld, t)
                        + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.5f);
            fragmentRoot.position = arc;
            fragmentRoot.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            if (fragmentLight != null)
                fragmentLight.intensity = Mathf.Lerp(glowIntensity, 0f, t);
            fragmentRoot.Rotate(Vector3.up, (200f + t * 300f) * Time.deltaTime, Space.World);
            yield return null;
        }
        fragmentRoot.gameObject.SetActive(false);
    }
}
