#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class SunFragmentSetup
{
    private const string MatFolder = "Assets/Materials/SunFragment";

    private static Material CreateParticleMaterial(string assetName, Color baseColor)
    {

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "SunFragment");

        string path = $"{MatFolder}/{assetName}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            ApplyParticleSettings(existing, baseColor);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        var mat = new Material(shader) { name = assetName };
        ApplyParticleSettings(mat, baseColor);

        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Setup] Сохранён материал: {path}");
        return mat;
    }

    private static void ApplyParticleSettings(Material mat, Color baseColor)
    {
        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_EmissionColor", baseColor * 2f);

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 3f);
        mat.SetFloat("_SrcBlend", 1f);
        mat.SetFloat("_DstBlend", 1f);
        mat.SetFloat("_ZWrite", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_BLENDMODE_ADD");
        mat.EnableKeyword("_EMISSION");

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    [MenuItem("Tools/Fix Sun Fragment Particle Materials")]
    private static void FixMaterials()
    {
        var go = Selection.activeGameObject;
        if (go == null) { EditorUtility.DisplayDialog("Ошибка", "Выдели GameObject.", "OK"); return; }
        var effect = go.GetComponent<SunFragmentEffect>();
        if (effect == null) { EditorUtility.DisplayDialog("Ошибка", "Нет SunFragmentEffect.", "OK"); return; }

        if (effect.activationBurst != null)
        {
            var mat = CreateParticleMaterial("ParticleGlow_Burst", new Color(1f, 0.8f, 0.2f, 1f));
            if (mat != null) effect.activationBurst.GetComponent<ParticleSystemRenderer>().material = mat;
        }

        if (effect.fragmentGlowPS != null)
        {
            var mat = CreateParticleMaterial("ParticleGlow_Glow", new Color(1f, 0.85f, 0.2f, 1f));
            if (mat != null) effect.fragmentGlowPS.GetComponent<ParticleSystemRenderer>().material = mat;
        }

        if (effect.flyTrailPS != null)
        {
            var mat = CreateParticleMaterial("ParticleGlow_Trail", new Color(1f, 0.7f, 0.1f, 1f));
            if (mat != null) effect.flyTrailPS.GetComponent<ParticleSystemRenderer>().material = mat;
        }

        EditorUtility.SetDirty(effect);
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] Материалы частиц обновлены и сохранены.");
    }

    [MenuItem("Tools/Setup Sun Fragment VFX")]
    private static void Setup()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Выдели GameObject с SunFragmentEffect в иерархии.", "OK");
            return;
        }

        var effect = go.GetComponent<SunFragmentEffect>();
        if (effect == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "На выбранном объекте нет компонента SunFragmentEffect.", "OK");
            return;
        }

        if (effect.activationBurst == null)
        {
            var burstGO = new GameObject("ActivationBurst");
            burstGO.transform.SetParent(go.transform, false);
            var ps = burstGO.AddComponent<ParticleSystem>();
            SetupActivationBurst(ps);
            effect.activationBurst = ps;
            Debug.Log("[Setup] ActivationBurst создан.");
        }

        if (effect.fragmentGlowPS == null)
        {
            var glowGO = new GameObject("FragmentGlow");
            var parent = effect.fragmentRoot != null ? effect.fragmentRoot : go.transform;
            glowGO.transform.SetParent(parent, false);
            var ps = glowGO.AddComponent<ParticleSystem>();
            SetupFragmentGlow(ps);
            effect.fragmentGlowPS = ps;
            Debug.Log("[Setup] FragmentGlow создан.");
        }

        if (effect.flyTrailPS == null)
        {
            var trailGO = new GameObject("FlyTrail");
            var parent = effect.fragmentRoot != null ? effect.fragmentRoot : go.transform;
            trailGO.transform.SetParent(parent, false);
            var ps = trailGO.AddComponent<ParticleSystem>();
            SetupFlyTrail(ps);
            effect.flyTrailPS = ps;
            Debug.Log("[Setup] FlyTrail создан.");
        }

        EditorUtility.SetDirty(effect);
        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] Готово! Все ParticleSystem созданы и назначены.");
    }

    private static void SetupActivationBurst(ParticleSystem ps)
    {
        var mat = CreateParticleMaterial("ParticleGlow_Burst", new Color(1f, 0.8f, 0.2f, 1f));
        if (mat != null) ps.GetComponent<ParticleSystemRenderer>().material = mat;

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.4f, 1f),
            new Color(1f, 0.6f, 0.1f, 1f));
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.3f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f,0.95f,0.3f), 0f), new GradientColorKey(new Color(1f,0.45f,0f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }

    private static void SetupFragmentGlow(ParticleSystem ps)
    {
        var mat = CreateParticleMaterial("ParticleGlow_Glow", new Color(1f, 0.85f, 0.2f, 1f));
        if (mat != null) ps.GetComponent<ParticleSystemRenderer>().material = mat;

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 0.6f, 0.9f),
            new Color(1f, 0.8f, 0.2f, 0.7f));
        main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 20f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f,1f,0.5f), 0f), new GradientColorKey(new Color(1f,0.6f,0f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);
    }

    private static void SetupFlyTrail(ParticleSystem ps)
    {
        var mat = CreateParticleMaterial("ParticleGlow_Trail", new Color(1f, 0.7f, 0.1f, 1f));
        if (mat != null) ps.GetComponent<ParticleSystemRenderer>().material = mat;

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.3f, 1f),
            new Color(1f, 0.7f, 0.0f, 0.8f));
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 40f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f,0.9f,0.2f), 0f), new GradientColorKey(new Color(1f,0.5f,0f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }
}
#endif
