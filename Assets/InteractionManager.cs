// using UnityEngine;
// using UnityEngine.UI;

// public class InteractionManager : MonoBehaviour
// {
//     public enum Mode { Idle, PlaceBin, PlaceFan, Play }

//     [Header("UI")]
//     public Button placeBinButton;
//     public Button placeFanButton;
//     public Text instructionText;

//     [Header("Refs")]
//     public Placer placer;            // assign MRUK Placer script
//     public BallSpawner spawner;      // assign BallSpawner
//     public ScoreUI scoreUI;         // assign Score UI

//     public AudioClip placeSound;
//     public AudioClip errorSound;
//     AudioSource audioSource;

//     public Mode currentMode { get; private set; } = Mode.Idle;
//     bool binPlaced = false;
//     bool fanPlaced = false;

//     void Awake()
//     {
//         audioSource = gameObject.AddComponent<AudioSource>();
//         // Wire buttons (if not wired in Inspector)
//         if (placeBinButton != null) placeBinButton.onClick.AddListener(OnPlaceBinClicked);
//         if (placeFanButton != null) placeFanButton.onClick.AddListener(OnPlaceFanClicked);

//         UpdateUI();
//     }

//     void UpdateUI()
//     {
//         if (instructionText != null)
//         {
//             switch (currentMode)
//             {
//                 case Mode.Idle: instructionText.text = "Choose: Place Bin"; break;
//                 case Mode.PlaceBin: instructionText.text = "Place the Bin (Trigger to place)"; break;
//                 case Mode.PlaceFan: instructionText.text = "Place the Fan (must be between you and bin)"; break;
//                 case Mode.Play: instructionText.text = "Throw paper balls! Press 'B' to spawn"; break;
//             }
//         }
//         if (placeFanButton != null)
//         {
//             placeFanButton.interactable = binPlaced;
//         }
//     }

//     public void OnPlaceBinClicked()
//     {
//         currentMode = Mode.PlaceBin;
//         placer.SetPlacementType(Placer.PlacementType.Bin);
//         UpdateUI();
//     }

//     public void OnPlaceFanClicked()
//     {
//         if (!binPlaced)
//         {
//             // can't place fan yet
//             audioSource.PlayOneShot(errorSound);
//             return;
//         }
//         currentMode = Mode.PlaceFan;
//         placer.SetPlacementType(Placer.PlacementType.Fan);
//         UpdateUI();
//     }

//     // Called by Placer when bin/fan is placed
//     public void NotifyPlaced(Placer.PlacementType type)
//     {
//         audioSource.PlayOneShot(placeSound);
//         if (type == Placer.PlacementType.Bin) binPlaced = true;
//         if (type == Placer.PlacementType.Fan) fanPlaced = true;

//         if (binPlaced && fanPlaced)
//         {
//             currentMode = Mode.Play;
//             spawner.EnableSpawning(true);
//         }
//         else
//         {
//             // return to idle so user can choose next
//             currentMode = Mode.Idle;
//         }
//         UpdateUI();
//     }
// }






using UnityEngine;
using UnityEngine.UI;
using System;

/// InteractionManager: controls UI mode switching and audio feedback.
/// Updated to use PlacementManager (non-MRUK) and be resilient if that component is absent.
public class InteractionManager : MonoBehaviour
{
    public enum Mode { Idle, PlaceBin, PlaceFan, Play }

    [Header("UI")]
    public Button placeBinButton;
    public Button placeFanButton;
    public Text instructionText;

    [Header("Refs")]
    // Use PlacementManager (your working, non-MRUK placer). It's optional.
    public PlacementManager placementManager;
    public BallSpawner spawner;
    public ScoreUI scoreUI;

    [Header("Audio (assign clips in Inspector)")]
    public AudioClip placeSound;     // play when bin/fan successfully placed
    public AudioClip errorSound;     // play when user tries invalid placement or forbids action
    public AudioClip binHitSound;    // play when ball hits bin (call OnBallHitBin)

    [Header("Audio Settings")]
    public bool spatializePlacementSfx = false; // if true, play placement sfx at object pos via one-shot object
    public float sfxVolume = 1.0f;

    AudioSource audioSource;

    public Mode currentMode { get; private set; } = Mode.Idle;
    bool binPlaced = false;
    bool fanPlaced = false;

    void Awake()
    {
        // ensure we have an AudioSource to play non-spatial one-shots
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // wire UI buttons if not wired in Inspector
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
        // If PlacementManager is present, tell it to enter bin mode
        if (placementManager != null) placementManager.SetModePlaceBin();
        UpdateUI();
    }

    public void OnPlaceFanClicked()
    {
        if (!binPlaced)
        {
            PlayErrorSound();
            return;
        }

        currentMode = Mode.PlaceFan;
        if (placementManager != null) placementManager.SetModePlaceFan();
        UpdateUI();
    }

    // Called by PlacementManager when placed (new API)
    // Use PlacementManager.Mode enum mapping: Mode.PlaceBin => placed bin, etc.
    public void NotifyPlaced(PlacementManager.Mode placementMode)
    {
        // convert to local semantics
        if (placementMode == PlacementManager.Mode.PlaceBin) NotifyPlacedBin();
        else if (placementMode == PlacementManager.Mode.PlaceFan) NotifyPlacedFan();
        else Debug.Log("[InteractionManager] NotifyPlaced called with unknown PlacementManager.Mode: " + placementMode);
    }

    // Backwards-compatible: Called by older Placer (if you had it)
    public void NotifyPlaced(Placer.PlacementType type)
    {
        if (type == Placer.PlacementType.Bin) NotifyPlacedBin();
        if (type == Placer.PlacementType.Fan) NotifyPlacedFan();
    }

    // Helper when a bin is placed
    void NotifyPlacedBin()
    {
        binPlaced = true;
        PlayPlacementSound(); // placement sfx
        AdvanceAfterPlacement();
    }

    // Helper when a fan is placed
    void NotifyPlacedFan()
    {
        fanPlaced = true;
        PlayPlacementSound();
        AdvanceAfterPlacement();
    }

    void AdvanceAfterPlacement()
    {
        if (binPlaced && fanPlaced)
        {
            currentMode = Mode.Play;
            if (spawner != null) spawner.EnableSpawning(true);
        }
        else
        {
            currentMode = Mode.Idle;
        }
        UpdateUI();
    }

    // inside InteractionManager class (replace previous audio methods)

    void PlayPlacementSound(Vector3? worldPos = null)
    {
        if (placeSound == null)
        {
            Debug.LogWarning("[InteractionManager] placeSound not assigned.");
            return;
        }

        // If a world position is provided, play spatialized at that position; otherwise non-spatial one-shot
        if (worldPos.HasValue)
        {
            Vector3 pos = worldPos.Value;
            GameObject go = new GameObject("PlacementSfx");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = placeSound;
            src.spatialBlend = 1.0f;
            src.volume = sfxVolume;
            src.Play();
            Destroy(go, placeSound.length + 0.1f);
            Debug.Log("[InteractionManager] Played placement sfx at " + pos);
        }
        else
        {
            // non-spatial UI sound
            audioSource.spatialBlend = 0f;
            audioSource.PlayOneShot(placeSound, sfxVolume);
            Debug.Log("[InteractionManager] Played placement sfx (UI).");
        }
    }

    void PlayErrorSound()
    {
        if (errorSound == null)
        {
            Debug.LogWarning("[InteractionManager] errorSound not assigned.");
            return;
        }
        audioSource.spatialBlend = 0f;
        audioSource.PlayOneShot(errorSound, sfxVolume);
        Debug.Log("[InteractionManager] Played error sfx.");
    }

    // Called by ball collision (see BallCollision below)
    public void OnBallHitBin(Vector3 hitPosition)
    {
        if (binHitSound == null)
        {
            Debug.LogWarning("[InteractionManager] binHitSound not assigned.");
            return;
        }

        GameObject go = new GameObject("BinHitSfx");
        go.transform.position = hitPosition;
        var src = go.AddComponent<AudioSource>();
        src.clip = binHitSound;
        src.spatialBlend = 1.0f;
        src.volume = sfxVolume;
        src.Play();
        Destroy(go, binHitSound.length + 0.1f);

        Debug.Log("[InteractionManager] Bin hit sound played at " + hitPosition);
    }

    // Backwards-compatible overload if position not known
    public void OnBallHitBin()
    {
        Vector3 p = Camera.main != null ? Camera.main.transform.position : transform.position;
        OnBallHitBin(p);
    }

    // Safety: if PlacementManager is missing we shouldn't crash. But it's nice to log
    void OnValidate()
    {
        if (placementManager == null)
        {
            // not an error — just reminder in Editor
            // Debug.Log("[InteractionManager] placementManager reference is not assigned (this is OK if not using placement).");
        }
    }
}