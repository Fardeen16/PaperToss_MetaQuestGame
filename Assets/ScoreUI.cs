using UnityEngine;
using TMPro;   // IMPORTANT

public class ScoreUI : MonoBehaviour
{
    public static ScoreUI Instance { get; private set; }

    [Header("Assign TMP Score Text")]
    public TMP_Text scoreText;   // <-- changed from Text to TMP_Text

    public int score = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(this);

        Instance = this;
        UpdateUI();
    }

    public void AddScore(int points)
    {
        score += points;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: " + score;
    }
}

// using UnityEngine;
// using UnityEngine.UI;
// using System.Collections;

// public class ScoreUI : MonoBehaviour
// {
//     public static ScoreUI Instance { get; private set; }

//     public Text scoreText;
//     public Text popupText;
//     int score = 0;

//     void Awake()
//     {
//         Instance = this;
//         UpdateUI();
//         if (popupText != null) popupText.gameObject.SetActive(false);
//     }

//     public void AddScore(int v)
//     {
//         score += v;
//         UpdateUI();
//         if (popupText != null) StartCoroutine(ShowPopup("+" + v));
//     }

//     void UpdateUI()
//     {
//         if (scoreText != null) scoreText.text = "Score: " + score;
//     }

//     IEnumerator ShowPopup(string s)
//     {
//         popupText.gameObject.SetActive(true);
//         popupText.text = s;
//         yield return new WaitForSeconds(1.2f);
//         popupText.gameObject.SetActive(false);
//     }
// }