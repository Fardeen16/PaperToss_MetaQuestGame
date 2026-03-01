using UnityEngine;
using UnityEngine.UI;

public class InteractionManager : MonoBehaviour
{
    public enum Mode { Idle, PlaceBin, PlaceFan, Play }

    [Header("UI")]
    public Button placeBinButton;
    public Button placeFanButton;
    public Text instructionText;

    [Header("Refs")]
    public Placer placer;            // assign MRUK Placer script
    public BallSpawner spawner;      // assign BallSpawner
    public ScoreUI scoreUI;         // assign Score UI

    public AudioClip placeSound;
    public AudioClip errorSound;
    AudioSource audioSource;

    public Mode currentMode { get; private set; } = Mode.Idle;
    bool binPlaced = false;
    bool fanPlaced = false;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        // Wire buttons (if not wired in Inspector)
        if (placeBinButton != null) placeBinButton.onClick.AddListener(OnPlaceBinClicked);
        if (placeFanButton != null) placeFanButton.onClick.AddListener(OnPlaceFanClicked);

        UpdateUI();
    }

    void UpdateUI()
    {
        if (instructionText != null)
        {
            switch (currentMode)
            {
                case Mode.Idle: instructionText.text = "Choose: Place Bin"; break;
                case Mode.PlaceBin: instructionText.text = "Place the Bin (Trigger to place)"; break;
                case Mode.PlaceFan: instructionText.text = "Place the Fan (must be between you and bin)"; break;
                case Mode.Play: instructionText.text = "Throw paper balls! Press 'B' to spawn"; break;
            }
        }
        if (placeFanButton != null)
        {
            placeFanButton.interactable = binPlaced;
        }
    }

    public void OnPlaceBinClicked()
    {
        currentMode = Mode.PlaceBin;
        placer.SetPlacementType(Placer.PlacementType.Bin);
        UpdateUI();
    }

    public void OnPlaceFanClicked()
    {
        if (!binPlaced)
        {
            // can't place fan yet
            audioSource.PlayOneShot(errorSound);
            return;
        }
        currentMode = Mode.PlaceFan;
        placer.SetPlacementType(Placer.PlacementType.Fan);
        UpdateUI();
    }

    // Called by Placer when bin/fan is placed
    public void NotifyPlaced(Placer.PlacementType type)
    {
        audioSource.PlayOneShot(placeSound);
        if (type == Placer.PlacementType.Bin) binPlaced = true;
        if (type == Placer.PlacementType.Fan) fanPlaced = true;

        if (binPlaced && fanPlaced)
        {
            currentMode = Mode.Play;
            spawner.EnableSpawning(true);
        }
        else
        {
            // return to idle so user can choose next
            currentMode = Mode.Idle;
        }
        UpdateUI();
    }
}