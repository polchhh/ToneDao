using UnityEngine;

public class FallZone : MonoBehaviour
{
    public ParticleSystem respawnEffect;

    public AudioClip respawnSound;

    private AudioSource _audioSource;

    private void Awake()
    {

        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }

        if (respawnSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        Vector3 returnPos = player.LastSafePosition;

        player.Respawn();

        if (respawnEffect != null)
        {
            respawnEffect.transform.position = returnPos;
            respawnEffect.Play();
        }

        if (_audioSource != null && respawnSound != null)
            _audioSource.PlayOneShot(respawnSound);
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(col.center, col.size);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireCube(col.center, col.size);
    }
}
