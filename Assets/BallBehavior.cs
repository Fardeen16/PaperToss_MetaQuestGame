using UnityEngine;

public class BallBehavior : MonoBehaviour
{
    public int points = 10;
    public AudioClip scoreSfx;
    public GameObject scoreParticlePrefab;

    void OnCollisionEnter(Collision col)
    {
        if (col.collider.CompareTag("Bin"))
        {
            ScoreUI.Instance.AddScore(points);

            if (scoreSfx) AudioSource.PlayClipAtPoint(scoreSfx, transform.position);
            if (scoreParticlePrefab) Instantiate(scoreParticlePrefab, transform.position, Quaternion.identity);

            // destroy ball
            Destroy(gameObject);
        }
        else
        {
            // optional: destroy after timeout to avoid garbage
            Destroy(gameObject, 8f);
        }
    }
}