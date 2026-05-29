using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(-10f, 2f, 0f);
    [Range(0.05f, 0.5f)] public float smoothTime = 0.15f;
    [Range(0.05f, 0.8f)] public float smoothTimeY = 0.25f;

    [Range(0f, 5f)] public float lookAheadDistance = 2f;
    [Range(1f, 10f)] public float lookAheadSpeed = 4f;

    public bool clampY = false;
    public float minY = -5f;
    public float maxY = 20f;

    private Vector3 _vel;
    private float _velY;
    private float _prevTargetZ;
    private float _lookAheadCurrent;

    private void Start()
    {
        if (target == null)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null) target = pc.transform;
        }

        if (target == null)
        {
            Debug.LogWarning("[CameraFollow] Target не задан и PlayerController не найден.");
            return;
        }

        _prevTargetZ = target.position.z;
        transform.position = target.position + offset;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float zDelta = (target.position.z - _prevTargetZ) / Mathf.Max(Time.deltaTime, 0.001f);
        _prevTargetZ = target.position.z;

        float targetLookAhead = Mathf.Sign(zDelta) * lookAheadDistance;

        if (Mathf.Abs(zDelta) < 0.5f) targetLookAhead = 0f;

        _lookAheadCurrent = Mathf.Lerp(_lookAheadCurrent, targetLookAhead,
                                        Time.deltaTime * lookAheadSpeed);

        float targetY = target.position.y + offset.y;
        if (clampY) targetY = Mathf.Clamp(targetY, minY + offset.y, maxY + offset.y);

        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            targetY,
            target.position.z + offset.z + _lookAheadCurrent
        );

        Vector3 cur = transform.position;

        float newX = Mathf.SmoothDamp(cur.x, desired.x, ref _vel.x, smoothTime);
        float newZ = Mathf.SmoothDamp(cur.z, desired.z, ref _vel.z, smoothTime);
        float newY = Mathf.SmoothDamp(cur.y, desired.y, ref _velY, smoothTimeY);

        transform.position = new Vector3(newX, newY, newZ);
    }
}
