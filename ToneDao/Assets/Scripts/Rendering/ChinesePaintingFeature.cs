using UnityEngine;

public class ChinesePaintingController : MonoBehaviour
{
    public Material paintMaterial;

    [Range(0.5f, 5f)] public float outlineThickness = 1.5f;
    [Range(0f, 1f)] public float outlineStrength = 0.90f;

    public Color inkColor = new Color(0.07f, 0.06f, 0.11f);
    public Color paperColor = new Color(0.95f, 0.89f, 0.75f);
    [Range(0f, 1f)] public float desaturation = 0.80f;
    [Range(0f, 1f)] public float inkWashBlend = 0.75f;

    [Range(0f, 1f)] public float paperGrain = 0.35f;

    private static readonly int s_InkColor = Shader.PropertyToID("_InkColor");
    private static readonly int s_PaperColor = Shader.PropertyToID("_PaperColor");
    private static readonly int s_OutlineThickness = Shader.PropertyToID("_OutlineThickness");
    private static readonly int s_OutlineStrength = Shader.PropertyToID("_OutlineStrength");
    private static readonly int s_DesaturationAmt = Shader.PropertyToID("_DesaturationAmount");
    private static readonly int s_PaperGrain = Shader.PropertyToID("_PaperGrain");
    private static readonly int s_InkWashBlend = Shader.PropertyToID("_InkWashBlend");

    private void OnValidate() => Apply();

    private void Update() => Apply();

    private void Apply()
    {
        if (paintMaterial == null) return;

        paintMaterial.SetColor(s_InkColor, inkColor);
        paintMaterial.SetColor(s_PaperColor, paperColor);
        paintMaterial.SetFloat(s_OutlineThickness, outlineThickness);
        paintMaterial.SetFloat(s_OutlineStrength, outlineStrength);
        paintMaterial.SetFloat(s_DesaturationAmt, desaturation);
        paintMaterial.SetFloat(s_PaperGrain, paperGrain);
        paintMaterial.SetFloat(s_InkWashBlend, inkWashBlend);
    }
}
