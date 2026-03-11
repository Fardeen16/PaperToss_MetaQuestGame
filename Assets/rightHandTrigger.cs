
using UnityEngine;

public class rightHandTrigger : MonoBehaviour
{
    public OVRInput.RawButton grabButton;
    public string popupMessage = "Secondary trigger used";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (OVRInput.GetDown(grabButton))
        {
            grabbed();
            
        }
    }

    public void grabbed()
    {
        Debug.Log("Caught");
        if (ScoreUI.Instance != null)
                ScoreUI.Instance.ShowPopup(popupMessage);
    }
}
