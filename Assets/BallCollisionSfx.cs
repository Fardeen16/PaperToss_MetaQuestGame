using UnityEngine;

public class BallCollisionSfx : MonoBehaviour
{
    [Tooltip("Optional: drag your InteractionManager here. If left empty, will try to FindObjectOfType.")]
    public InteractionManager interactionManager;

    [Tooltip("Tag to check for bin collisions. Set your DustBin prefab tag to 'DustBin'.")]
    public string binTag = "DustBin";

    void Start()
    {
        if (interactionManager == null)
        {
            interactionManager = FindObjectOfType<InteractionManager>();
            // If your Unity version warns about deprecation, this still works. Alternatively assign via Inspector.
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null) return;

        // If you use a tag on bin
        if (!string.IsNullOrEmpty(binTag) && collision.collider.CompareTag(binTag))
        {
            Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
            if (interactionManager != null)
                interactionManager.PlayBinHitSound(hitPoint);
            else
                Debug.LogWarning("[BallCollisionSfx] InteractionManager not found to play bin hit sound.");
        }
    }
}