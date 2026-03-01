using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ScoreUI : MonoBehaviour
{
    public static ScoreUI Instance { get; private set; }

    public Text scoreText;
    public Text popupText;
    int score = 0;

    void Awake()
    {
        Instance = this;
        UpdateUI();
        if (popupText != null) popupText.gameObject.SetActive(false);
    }

    public void AddScore(int v)
    {
        score += v;
        UpdateUI();
        if (popupText != null) StartCoroutine(ShowPopup("+" + v));
    }

    void UpdateUI()
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
    }

    IEnumerator ShowPopup(string s)
    {
        popupText.gameObject.SetActive(true);
        popupText.text = s;
        yield return new WaitForSeconds(1.2f);
        popupText.gameObject.SetActive(false);
    }
}