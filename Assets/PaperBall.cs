using UnityEngine;
using Oculus.Interaction;

public class PaperBall : MonoBehaviour
{
    [Header("Wind Settings")]
    public Transform fanTransform; 
    public float windStrength = 2.0f;
    
    private Rigidbody rb;
    private Grabbable grabbable;
    private bool isReleased = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();

        // Listen for Meta ISDK grab events
        grabbable.WhenPointerEventRaised += (evt) => {
            if (evt.Type == PointerEventType.Unselect) isReleased = true;
            else if (evt.Type == PointerEventType.Select) isReleased = false;
        };
    }

    void FixedUpdate()
    {
        // Apply wind only when the ball is airborne
        if (isReleased && fanTransform != null)
        {
            Vector3 windDirection = fanTransform.forward;
            rb.AddForce(windDirection * windStrength, ForceMode.Force);
        }
    }
}