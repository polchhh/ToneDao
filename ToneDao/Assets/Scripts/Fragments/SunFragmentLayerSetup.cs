using UnityEngine;

public class SunFragmentLayerSetup : MonoBehaviour
{
    public string fragmentLayerName = "SunFragment";

    private void Awake()
    {
        int layer = LayerMask.NameToLayer(fragmentLayerName);
        if (layer < 0)
        {
            return;
        }

        var effect = GetComponent<SunFragmentEffect>();
        if (effect != null && effect.fragmentRoot != null)
        {
            SetLayerRecursive(effect.fragmentRoot.gameObject, layer);
        }
        else
        {

            SetLayerRecursive(gameObject, layer);
        }
    }

    private void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
