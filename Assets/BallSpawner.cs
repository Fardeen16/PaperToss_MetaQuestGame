using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    public GameObject ballPrefab;
    public Transform rightHandAnchor;

    
     public Transform spawnOrigin;
     public float spawnForce = 6f;



    public float spawnDistance = 0.15f;
    public float initialSpeed = 6f;
    public bool spawningEnabled = false;

    public AudioClip spawnSfx;

    public void EnableSpawning(bool enabled) { spawningEnabled = enabled; }

    void Update()
    {
        if (!spawningEnabled) return;

        // Use secondary button to spawn (e.g., A or One)
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            SpawnBall();
        }
    }

    public void SpawnBall()
    {
        // Vector3 pos = rightHandAnchor.position + rightHandAnchor.forward * spawnDistance;
        // Quaternion rot = rightHandAnchor.rotation;
        // GameObject ball = Instantiate(ballPrefab, pos, rot);
        // Rigidbody rb = ball.GetComponent<Rigidbody>();
        // if (rb == null) rb = ball.AddComponent<Rigidbody>();

        // // apply initial velocity forward
        // rb.linearVelocity = rightHandAnchor.forward * initialSpeed;

        // if (spawnSfx) AudioSource.PlayClipAtPoint(spawnSfx, pos);
        if (ballPrefab == null || spawnOrigin == null) return;

        // instantiate and apply physics
        GameObject b = Instantiate(ballPrefab, spawnOrigin.position, spawnOrigin.rotation);
        var rb = b.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(spawnOrigin.forward * spawnForce, ForceMode.VelocityChange);
        }
    }
}