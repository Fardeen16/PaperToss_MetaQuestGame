using System.Collections;
using UnityEngine;
using TMPro;   // IMPORTANT

public class ScoreUI : MonoBehaviour
{
    public static ScoreUI Instance { get; private set; }

    [Header("Assign TMP Score Text")]
    public TMP_Text scoreText;   // <-- changed from Text to TMP_Text

    public int score = 0;

    [Header("Assign TMP Popup Text")]
    public TMP_Text popupText;
    public float popupDuration = 1.2f;

    private Coroutine popupRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        UpdateUI();

        if (popupText != null)
            popupText.gameObject.SetActive(false);
    }

    public void AddScore(int points)
    {
        score += points;
        UpdateUI();
    }

    public void ShowPopup(string message)
    {
        if (popupText == null) return;

        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(PopupCoroutine(message));
    }

    private IEnumerator PopupCoroutine(string message)
    {
        popupText.text = message;
        popupText.gameObject.SetActive(true);
        yield return new WaitForSeconds(popupDuration);
        popupText.gameObject.SetActive(false);
        popupRoutine = null;
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