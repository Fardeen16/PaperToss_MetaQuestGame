using UnityEngine;

public class BallBehavior : MonoBehaviour
{
    public int points = 10;
    public AudioClip scoreSfx;
    public GameObject scoreParticlePrefab;

    [Tooltip("Minimum upward normal required to count as top hit")]
    public float minTopNormalY = 0.7f;

    void OnCollisionEnter(Collision col)
    {
        if (!col.collider.CompareTag("Bin")) return;

        foreach (var contact in col.contacts)
        {
            // Only count if hit from above (top surface)
            if (contact.normal.y >= minTopNormalY)
            {
                if (ScoreUI.Instance != null)
                {
                    ScoreUI.Instance.AddScore(points);
                }

                if (scoreSfx)
                    AudioSource.PlayClipAtPoint(scoreSfx, contact.point);

                if (scoreParticlePrefab)
                    Instantiate(scoreParticlePrefab, contact.point, Quaternion.identity);

                Destroy(gameObject);
                return;
            }
        }

        // Side hit — do nothing or destroy later
        Destroy(gameObject, 8f);
    }
}