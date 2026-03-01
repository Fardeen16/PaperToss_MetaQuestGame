// PlacementManager.cs
using UnityEngine;
using System.Collections;

public class PlacementManager : MonoBehaviour
{
    public enum Mode { PlaceBin, PlaceFan }
    [Header("Prefabs")]
    public GameObject binPrefab;
    public GameObject fanPrefab;
    public GameObject previewPrefab; // ghost preview you said is working

    [Header("References")]
    public Transform rightHandAnchor; // assign RealHands right anchor
    public Transform cameraTransform; // assign Main Camera

    [Header("Settings")]
    public float maxRaycastDist = 10f;
    public LayerMask floorLayers = ~0; // which layers count as floor
    public float snapToFloorIfCloserThan = 0.5f; // if preview is very close to camera, snap to floor below it
    public float floorYOffset = 0.10f; // how high above floor to place
    public float placeCooldown = 0.25f; // prevents accidental double-spawn

    GameObject previewObj;
    Mode currentMode = Mode.PlaceBin;
    GameObject placedBin;
    GameObject placedFan;

    bool canPlace = true;

    void Start()
    {
        if (cameraTransform == null) cameraTransform = Camera.main.transform;
        if (previewPrefab != null)
        {
            previewObj = Instantiate(previewPrefab);
            previewObj.SetActive(true);
            // ensure preview has no colliders blocking raycasts
            foreach (var c in previewObj.GetComponentsInChildren<Collider>()) Destroy(c);
        }
    }

    void Update()
    {
        // assume preview is updated elsewhere (you said preview is fine).
        // But keep a small safety: if previewObj is null, do nothing.
        if (previewObj == null) return;

        // Place with right index trigger (RTouch)
        if (canPlace && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            TryPlaceAtPreview();
        }

        // optional: short debug switch between modes with button Two
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            currentMode = currentMode == Mode.PlaceBin ? Mode.PlaceFan : Mode.PlaceBin;
            Debug.Log("Placement mode = " + currentMode);
        }
    }

    void TryPlaceAtPreview()
    {
        if (previewObj == null) return;

        Vector3 desired = previewObj.transform.position;
        Quaternion desiredRot = previewObj.transform.rotation;

        // if preview is very close to camera (e.g., you're pointing at yourself), snap the spawn down to the floor under the preview
        float distCamToPreview = Vector3.Distance(cameraTransform.position, desired);
        if (distCamToPreview < snapToFloorIfCloserThan)
        {
            // cast downwards from a little above the preview to find the floor
            Vector3 downOrigin = desired + Vector3.up * 1.0f;
            if (Physics.Raycast(downOrigin, Vector3.down, out RaycastHit hit, 5.0f, floorLayers))
            {
                desired.y = hit.point.y + floorYOffset;
                // keep rotation as facing camera horizontally
                Vector3 dirToCam = cameraTransform.position - desired;
                dirToCam.y = 0;
                if (dirToCam.sqrMagnitude > 0.001f)
                    desiredRot = Quaternion.LookRotation(dirToCam);
            }
            else
            {
                // if floor not found, fallback to keeping preview y but move it slightly forward
                desired += (cameraTransform.forward * 0.2f);
            }
        }

        // final validity check: preview should be at reasonable distance -> optional
        if ((desired - cameraTransform.position).sqrMagnitude < 0.01f)
        {
            Debug.Log("Placement aborted: preview position too close.");
            return;
        }

        // place according to current mode
        if (currentMode == Mode.PlaceBin)
        {
            PlacePrefab(binPrefab, desired, desiredRot, out placedBin);
            currentMode = Mode.PlaceFan; // require fan after bin
            Debug.Log("[Placement] Bin placed at " + desired);
        }
        else // PlaceFan
        {
            if (placedBin == null)
            {
                Debug.Log("[Placement] Place the bin first before placing the fan.");
                return;
            }

            // optional additional rule: ensure fan is between camera and bin (simple dot/angle test)
            if (!IsFanPlacementValid(desired))
            {
                Debug.Log("[Placement] Fan placement invalid — must be between you and the bin (in FOV).");
                return;
            }

            PlacePrefab(fanPrefab, desired, desiredRot, out placedFan);
            Debug.Log("[Placement] Fan placed at " + desired);
        }

        // small cooldown to prevent multiple placements
        StartCoroutine(PlaceCooldown());
    }

    IEnumerator PlaceCooldown()
    {
        canPlace = false;
        yield return new WaitForSeconds(placeCooldown);
        canPlace = true;
    }

    void PlacePrefab(GameObject prefab, Vector3 pos, Quaternion rot, out GameObject placed)
    {
        placed = null;
        if (prefab == null) { Debug.LogWarning("Prefab missing"); return; }

        placed = Instantiate(prefab, pos, rot);

        // add spatial anchor for world-locked placement
        if (placed.GetComponent<OVRSpatialAnchor>() == null)
            placed.AddComponent<OVRSpatialAnchor>();

        // make static/kinematic so physics doesn't push it around
        var rb = placed.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // destroy grabbable components so anchor stays put
        var ovrGrabbable = placed.GetComponent<OVRGrabbable>();
        if (ovrGrabbable != null) Destroy(ovrGrabbable);
        var xrGrab = placed.GetComponent("XRGrabInteractable") as Component;
        if (xrGrab != null) Destroy(xrGrab);
    }

    bool IsFanPlacementValid(Vector3 fanPos)
    {
        if (placedBin == null) return false;

        Vector3 camPos = cameraTransform.position;
        Vector3 toBin = (placedBin.transform.position - camPos);
        toBin.y = 0;
        Vector3 toFan = (fanPos - camPos);
        toFan.y = 0;

        // ensure fan is roughly in the same direction as the bin (angle check)
        float angle = Vector3.Angle(toBin, toFan);
        if (angle > 40f) return false;

        // ensure fan is roughly between camera and bin distance-wise
        float distFan = Vector3.Distance(camPos, fanPos);
        float distBin = Vector3.Distance(camPos, placedBin.transform.position);
        if (distFan > distBin + 0.5f) return false; // fan shouldn't be much further than bin

        // ensure fan is in front of camera
        if (Vector3.Dot(cameraTransform.forward, (fanPos - camPos)) < 0f) return false;

        return true;
    }
}