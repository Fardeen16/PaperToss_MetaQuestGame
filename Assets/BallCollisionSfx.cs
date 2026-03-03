using UnityEngine;

public class BallCollisionSfx : MonoBehaviour
{
    InteractionManager interactionManager;

    void Awake()
    {
        // Cache the InteractionManager reference once at startup.
        // Uses the modern Unity API. If your Unity version doesn't have
        // FindFirstObjectByType, update Unity or change to FindObjectOfType.
        interactionManager = UnityEngine.Object.FindFirstObjectByType<InteractionManager>();

        if (interactionManager == null)
        {
            Debug.LogWarning("[BallCollisionSfx] InteractionManager not found in scene at Awake().");
            // we keep running; OnCollisionEnter will try again if needed.
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Only trigger on collisions with the bin. Use a tag for reliability.
        // Make sure your bin prefab has Tag = "DustBin" (or change this string).
        if (!collision.collider.CompareTag("DustBin"))
            return;

        // determine hit point (use first contact if available)
        Vector3 hitPoint = (collision.contacts != null && collision.contacts.Length > 0)
            ? collision.contacts[0].point
            : transform.position;

        if (interactionManager == null)
        {
            // try to find it once more (in case it was created after Awake)
            interactionManager = UnityEngine.Object.FindFirstObjectByType<InteractionManager>();
            if (interactionManager == null)
            {
                Debug.LogWarning("[BallCollisionSfx] InteractionManager still not found on collision.");
                return;
            }
        }

        // call the InteractionManager to play the bin hit sfx
        interactionManager.OnBallHitBin(hitPoint);
    }
}