using UnityEngine;

public class TreeWindSway : MonoBehaviour
{
    public Transform[] targets;

    public float swayAngle = 5f;
    public float swaySpeed = 0.7f;

    public float flutterAngle = 2f;
    public float flutterSpeed = 7f;

    public Vector2 windDirection = new Vector2(1f, 0.2f);

    private Quaternion[] _baseRotations;
    private float[] _phases;

    private void Start()
    {
        if (targets == null || targets.Length == 0) return;

        _baseRotations = new Quaternion[targets.Length];
        _phases = new float[targets.Length];

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            _baseRotations[i] = targets[i].localRotation;
            _phases[i] = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void Update()
    {
        if (targets == null) return;

        float t = Time.time;
        var dir = new Vector3(windDirection.x, 0f, windDirection.y).normalized;
        var swayAxis = Vector3.Cross(dir, Vector3.up).normalized;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;

            float main = Mathf.Sin(t * swaySpeed + _phases[i]) * swayAngle;
            float flutter = Mathf.Sin(t * flutterSpeed + _phases[i] * 2.1f) * flutterAngle
                          + Mathf.Sin(t * flutterSpeed * 0.6f + _phases[i]) * flutterAngle * 0.4f;

            targets[i].localRotation = _baseRotations[i] *
                Quaternion.AngleAxis(main + flutter, swayAxis);
        }
    }
}
