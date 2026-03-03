using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BallCollisionSfx : MonoBehaviour
{
    [Tooltip("Sound to play when ball hits the bin top")]
    public AudioClip binHitClip;

    [Tooltip("Minimum contact normal Y to count as a top hit (0..1). 0.7 is a good start.")]
    public float minTopNormalY = 0.7f;

    [Tooltip("Points awarded for a top hit")]
    public int pointsOnTopHit = 1;

    [Tooltip("Minimum collision relative speed to count (optional)")]
    public float minRelativeSpeed = 0.5f;

    bool alreadyScored = false;

    void OnCollisionEnter(Collision collision)
    {
        if (alreadyScored) return;

        // Quick early-out: require some speed to avoid light grazes
        if (collision.relativeVelocity.sqrMagnitude < minRelativeSpeed * minRelativeSpeed) return;

        // Is the other object a bin? Use tag if available, fallback to name contains check.
        var other = collision.collider;
        bool isBin = other.CompareTag("Bin") || other.gameObject.name.ToLower().Contains("dustbin") || other.gameObject.name.ToLower().Contains("bin");

        if (!isBin) return;

        // Check contacts for an upward-facing contact normal (i.e., hitting the top)
        foreach (var contact in collision.contacts)
        {
            Vector3 n = contact.normal.normalized;
            // contact.normal is the normal on the other collider pointing *away* from the surface.
            // For top of bin this should be roughly Vector3.up -> so n.y should be high.
            if (n.y >= minTopNormalY)
            {
                // count it
                ScoreUI.Instance?.AddScore(pointsOnTopHit);

                // play SFX at contact point
                if (binHitClip != null)
                    AudioSource.PlayClipAtPoint(binHitClip, contact.point, 1f);

                // prevent double-count for this ball
                alreadyScored = true;

                // optional: you might want to destroy the ball or disable collisions
                // Destroy(gameObject, 0.1f);
                return;
            }
        }
    }
}



// using UnityEngine;

// public class BallCollisionSfx : MonoBehaviour
// {
//     [Tooltip("Optional: drag your InteractionManager here. If left empty, will try to FindObjectOfType.")]
//     public InteractionManager interactionManager;

//     [Tooltip("Tag to check for bin collisions. Set your DustBin prefab tag to 'DustBin'.")]
//     public string binTag = "DustBin";

//     void Start()
//     {
//         if (interactionManager == null)
//         {
//             interactionManager = FindObjectOfType<InteractionManager>();
//             // If your Unity version warns about deprecation, this still works. Alternatively assign via Inspector.
//         }
//     }

//     void OnCollisionEnter(Collision collision)
//     {
//         if (collision == null) return;

//         // If you use a tag on bin
//         if (!string.IsNullOrEmpty(binTag) && collision.collider.CompareTag(binTag))
//         {
//             Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
//             if (interactionManager != null)
//                 interactionManager.PlayBinHitSound(hitPoint);
//             else
//                 Debug.LogWarning("[BallCollisionSfx] InteractionManager not found to play bin hit sound.");
//         }
//     }
// }