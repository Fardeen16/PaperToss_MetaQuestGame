
using UnityEngine;

public class rightHandTrigger : MonoBehaviour
{
    public OVRInput.RawButton grabButton;
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
    }
}
