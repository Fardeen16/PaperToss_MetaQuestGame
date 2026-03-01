// using UnityEngine;

// public class spatialAnchoring : MonoBehaviour
// {
//     public GameObject anchorPrefab;

//     void Update()
//     {
//         if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
//         {
//             CreateSpatialAnchor();
//         }
//     }

//     public void CreateSpatialAnchor()
//     {
//         GameObject prefab = Instantiate(anchorPrefab, OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch), OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch));
//         prefab.AddComponent<OVRSpatialAnchor>();
//     }
// }

// AnchorManager.cs (place on an empty GameObject in scene)

using UnityEngine;

// Simple placer: spawns anchorPrefab at the right controller world pose when right trigger pressed.
// Prefers a supplied RightHandAnchor Transform (OVRCameraRig/TrackingSpace/RightHandAnchor).
public class SimpleControllerPlacer : MonoBehaviour
{
    [Header("References")]
    public GameObject anchorPrefab;
    public Transform rightHandAnchor;   // Optional: drag OVRCameraRig/TrackingSpace/RightHandAnchor here

    [Header("Input")]
    public OVRInput.Button placeButton = OVRInput.Button.PrimaryIndexTrigger;
    public OVRInput.Controller controller = OVRInput.Controller.RTouch;

    [Header("Spawn options")]
    public bool removeGrabComponents = true;   // remove OVRGrabbable/XRGrabInteractable if present
    public bool makeKinematic = true;          // make rigidbody kinematic so anchor stays fixed
    public bool addOVRSpatialAnchor = true;    // add OVRSpatialAnchor component for session anchoring

    void Update()
    {
        if (anchorPrefab == null) return;

        if (OVRInput.GetDown(placeButton, controller))
        {
            PlaceAtController();
        }
    }

    void PlaceAtController()
    {
        Vector3 worldPos;
        Quaternion worldRot;

        // Preferred: use a scene RightHandAnchor Transform (most stable)
        if (rightHandAnchor != null)
        {
            worldPos = rightHandAnchor.position;
            worldRot = rightHandAnchor.rotation;
        }
        else
        {
            // Fallback: convert OVR local controller pose to world using tracking origin (parent of center eye)
            Vector3 localPos = OVRInput.GetLocalControllerPosition(controller);
            Quaternion localRot = OVRInput.GetLocalControllerRotation(controller);

            Transform trackingOrigin = Camera.main != null ? Camera.main.transform.parent : null;
            if (trackingOrigin != null)
            {
                worldPos = trackingOrigin.TransformPoint(localPos);
                worldRot = trackingOrigin.rotation * localRot; // correct local->world rotation conversion
            }
            else
            {
                // If no tracking origin is available, use local values (less ideal)
                worldPos = localPos;
                worldRot = localRot;
            }
        }

        // Instantiate
        GameObject go = Instantiate(anchorPrefab, worldPos, worldRot);
        go.transform.parent = null; // ensure it's world-locked (not parented to camera or rig)

        // Optionally remove grab components so anchor can't be grabbed again
        if (removeGrabComponents)
        {
            var ovrGrabbable = go.GetComponent<OVRGrabbable>();
            if (ovrGrabbable != null) Destroy(ovrGrabbable);

            var xrGrab = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (xrGrab != null) Destroy(xrGrab);
        }

        // Make kinematic/static so physics won't move it
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null && makeKinematic)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        // Add OVRSpatialAnchor for session-local anchoring (optional)
        if (addOVRSpatialAnchor && go.GetComponent<OVRSpatialAnchor>() == null)
        {
            go.AddComponent<OVRSpatialAnchor>();
        }

        Debug.Log($"Placed anchor '{go.name}' at {worldPos} (rot: {worldRot.eulerAngles})");
    }
}