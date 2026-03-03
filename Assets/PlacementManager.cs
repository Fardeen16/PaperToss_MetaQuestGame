

using System;
using System.Collections;
using UnityEngine;

public class PlacementManager : MonoBehaviour
{
    public enum Mode { PlaceBin, PlaceFan }

    [Header("Prefabs")]
    public GameObject binPrefab;           // dustbin prefab
    public GameObject fanPrefab;           // fan prefab
    public GameObject previewPrefab;       // ghost preview object (semi-transparent model)

    [Header("References")]
    public Transform rightHandAnchor;      // assign: RealHands/RightHandAnchor (or controller transform)
    public Transform cameraTransform;      // assign: Main Camera (optional; auto-find)
    [Header("External Refs")]
    public InteractionManager interactionManager; // notify when placed
    

    [Header("Ray & Floor")]
    public float maxRaycastDistance = 10f;
    public LayerMask raycastMask = ~0;     // layers to hit with physics raycast
    public float floorY = 0f;              // fallback floor Y (if no hits) — set to 0 or MRUK-found value
    public float floorYOffset = 0.10f;     // place objects slightly above floor

    [Header("Placement rules")]
    public float placeCooldown = 0.2f;
    public float fanMaxAngleDeg = 40f;     // allowed angle from camera->bin direction for fan
    public float fanMaxDistanceExtra = 0.5f; // fan shouldn't be further than bin + this
    public float fanMinBetween = 0.15f;    // fraction along cam->bin that fan must be (0..1)
    public float fanMaxBetween = 0.95f;

    [Header("Visuals / Debug")]
    public Color previewValidColor = Color.green;
    public Color previewInvalidColor = Color.red;
    public bool logDebug = true;

    // runtime
    GameObject previewInstance;
    Mode currentMode = Mode.PlaceBin;
    bool canPlace = true;
    GameObject placedBin;
    GameObject placedFan;

    // toggle for placement phase (secondary trigger/grip toggles)
    bool placementEnabled = true;

    void Start()
    {
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (rightHandAnchor == null) Debug.LogWarning("[PlacementManager] rightHandAnchor not assigned!");

        if (previewPrefab != null)
        {
            previewInstance = Instantiate(previewPrefab);
            previewInstance.name = "Preview_" + previewPrefab.name;
            // remove colliders so preview doesn't block raycasts
            foreach (var c in previewInstance.GetComponentsInChildren<Collider>()) Destroy(c);
            previewInstance.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[PlacementManager] previewPrefab not assigned.");
        }
    }

    void Update()
    {
        if (previewInstance == null || rightHandAnchor == null) return;

        // toggle placement on/off with right-hand grip (PrimaryHandTrigger on RTouch)
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
        {
            placementEnabled = !placementEnabled;
            if (previewInstance != null) previewInstance.SetActive(placementEnabled);
            if (logDebug) Debug.Log("[PlacementManager] placementEnabled = " + placementEnabled);
        }

        if (!placementEnabled)
        {
            // early return so we don't update preview or place
            return;
        }

        // update preview position each frame
        UpdatePreview();

        // place on right-hand index trigger down (RTouch)
        if (canPlace && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            if (logDebug) Debug.Log("[PlacementManager] Trigger pressed -> TryPlaceAtPreview()");
            TryPlaceAtPreview();
        }

        // optional quick mode switch with Button.Two on right controller
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            currentMode = (currentMode == Mode.PlaceBin) ? Mode.PlaceFan : Mode.PlaceBin;
            if (logDebug) Debug.Log("[PlacementManager] Mode switched -> " + currentMode);
        }
    }

    void UpdatePreview()
    {
        Vector3 origin = rightHandAnchor.position;
        Vector3 dir = rightHandAnchor.forward.normalized;
        Ray r = new Ray(origin, dir);

        Vector3 candidatePos;
        Quaternion candidateRot = rightHandAnchor.rotation;

        RaycastHit hit;
        bool hitSomething = Physics.Raycast(r, out hit, maxRaycastDistance, raycastMask);

        if (hitSomething)
        {
            // Use the real hit point if we hit geometry
            candidatePos = hit.point;

            // If hit y is close to floorY, snap y to floorY+offset to avoid tiny floats
            if (Mathf.Abs(candidatePos.y - floorY) < 0.5f)
                candidatePos.y = floorY + floorYOffset;

            // face camera horizontally if camera assigned
            if (cameraTransform != null)
            {
                Vector3 toCam = cameraTransform.position - candidatePos; toCam.y = 0;
                if (toCam.sqrMagnitude > 0.001f) candidateRot = Quaternion.LookRotation(toCam, Vector3.up);
            }
        }
        else
        {
            // No hit -> try to find intersection of the ray with horizontal plane at floorY
            bool intersects = GetRayFloorIntersection(r, floorY, out Vector3 floorHit, out float t);

            if (intersects && t > 0f && t <= maxRaycastDistance)
            {
                // Use the intersection point (so preview projects far away to where the ray meets the floor)
                candidatePos = floorHit;
                candidatePos.y = floorY + floorYOffset; // ensure consistent offset
                // rotate to face camera horizontally
                if (cameraTransform != null)
                {
                    Vector3 toCam = cameraTransform.position - candidatePos; toCam.y = 0;
                    if (toCam.sqrMagnitude > 0.001f) candidateRot = Quaternion.LookRotation(toCam, Vector3.up);
                }
            }
            else
            {
                // Fallback: place preview at a default distance forward, but projected down to floorY
                float fallbackDistance = Mathf.Min(maxRaycastDistance, 3.0f);
                Vector3 forwardPoint = origin + dir * fallbackDistance;
                forwardPoint.y = floorY + floorYOffset;
                candidatePos = forwardPoint;
                candidateRot = Quaternion.LookRotation(dir, Vector3.up);
            }
        }

        // validation
        bool valid = true;
        if (currentMode == Mode.PlaceFan)
            valid = IsFanPlacementValid(candidatePos);

        // update preview transform + color
        previewInstance.transform.position = candidatePos;
        previewInstance.transform.rotation = candidateRot;
        SetPreviewColor(valid ? previewValidColor : previewInvalidColor);
    }

    bool GetRayFloorIntersection(Ray ray, float floorY, out Vector3 hitPoint, out float t)
    {
        hitPoint = Vector3.zero;
        t = 0f;

        Vector3 origin = ray.origin;
        Vector3 dir = ray.direction;

        if (Mathf.Abs(dir.y) < 1e-5f) return false;

        t = (floorY - origin.y) / dir.y;
        if (t < 0f) return false;

        hitPoint = origin + dir * t;
        return true;
    }

    void TryPlaceAtPreview()
    {
        if (previewInstance == null) return;

        Vector3 pos = previewInstance.transform.position;
        Quaternion rot = previewInstance.transform.rotation;
        //interactionManager = UnityEngine.Object.FindFirstObjectByType<InteractionManager>();

        if (cameraTransform != null && (pos - cameraTransform.position).sqrMagnitude < 0.01f)
        {
            if (logDebug) Debug.Log("[PlacementManager] Aborting: preview too close to camera.");
            return;
        }

        if (currentMode == Mode.PlaceBin)
        {
            PlacePrefabAt(binPrefab, pos, rot, (g) => placedBin = g);
            // notify interaction manager
            if (interactionManager != null)
            {
                interactionManager.NotifyPlaced(Mode.PlaceBin);
            }
            currentMode = Mode.PlaceFan;    // require fan next
        }
        else // PlaceFan
        {
            if (placedBin == null)
            {
                if (logDebug) Debug.Log("[PlacementManager] Place the bin first.");
                return;
            }

            if (!IsFanPlacementValid(pos))
            {
                if (logDebug) Debug.Log("[PlacementManager] Fan placement invalid — must be between you and the bin and in FOV.");
                return;
            }

            PlacePrefabAt(fanPrefab, pos, rot, (g) => placedFan = g);
            //interactionManager?.NotifyPlaced(Mode.PlaceFan);
            if (interactionManager != null)
            {
                interactionManager.NotifyPlaced(Mode.PlaceFan);
            }
        }

        StartCoroutine(PlacementCooldown());
    }

    IEnumerator PlacementCooldown()
    {
        canPlace = false;
        yield return new WaitForSeconds(placeCooldown);
        canPlace = true;
    }

    void PlacePrefabAt(GameObject prefab, Vector3 pos, Quaternion rot, Action<GameObject> onPlaced)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[PlacementManager] Missing prefab to place.");
            return;
        }

        GameObject go = Instantiate(prefab, pos, rot);
        go.transform.parent = null;

        // try to remove grabbable components by name (safe against missing packages)
        RemoveComponentByName(go, "OVRGrabbable");
        RemoveComponentByName(go, "XRGrabInteractable");

        // set rigidbody to kinematic if exists
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        // add spatial anchor if OVRSpatialAnchor type exists (reflection)
        TryAddSpatialAnchor(go);

        onPlaced?.Invoke(go);

        if (logDebug) Debug.Log("[PlacementManager] Placed " + prefab.name + " at " + pos);
    }

    bool IsFanPlacementValid(Vector3 fanPos)
    {
        if (placedBin == null || cameraTransform == null) return false;

        Vector3 camPos = cameraTransform.position;
        Vector3 binPos = placedBin.transform.position;

        Vector3 camToBin = binPos - camPos;
        float camToBinLen = camToBin.magnitude;
        if (camToBinLen < 0.1f) return false;
        Vector3 axisDir = camToBin / camToBinLen;

        Vector3 rel = fanPos - camPos;
        float proj = Vector3.Dot(rel, axisDir) / camToBinLen;
        if (proj < fanMinBetween || proj > fanMaxBetween) return false;

        Vector3 closestOnAxis = camPos + axisDir * Vector3.Dot(rel, axisDir);
        float perpDist = Vector3.Distance(fanPos, closestOnAxis);
        float maxPerpAllowed = Mathf.Lerp(0.3f, 1.5f, Mathf.Clamp01(camToBinLen / 5f));
        if (perpDist > maxPerpAllowed) return false;

        float angle = Vector3.Angle(cameraTransform.forward, (fanPos - camPos));
        if (angle > fanMaxAngleDeg) return false;

        float distFan = Vector3.Distance(camPos, fanPos);
        if (distFan > camToBinLen + fanMaxDistanceExtra) return false;

        return true;
    }

    void SetPreviewColor(Color c)
    {
        if (previewInstance == null) return;
        var rends = previewInstance.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r.material != null) r.material.color = c;
        }
    }

    void RemoveComponentByName(GameObject go, string typeName)
    {
        var t = Type.GetType(typeName) ?? FindTypeInAppDomain(typeName);
        if (t == null) return;
        var comp = go.GetComponent(t);
        if (comp != null) Destroy(comp);
    }

    void TryAddSpatialAnchor(GameObject go)
    {
        Type anchorType = FindTypeInAppDomain("OVRSpatialAnchor") ?? FindTypeInAppDomain("Unity.XR.Oculus.OVRSpatialAnchor");
        if (anchorType != null && go.GetComponent(anchorType) == null)
        {
            go.AddComponent(anchorType);
            if (logDebug) Debug.Log("[PlacementManager] Added spatial anchor via reflection.");
        }
    }

    Type FindTypeInAppDomain(string shortName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == shortName || t.FullName == shortName) return t;
                }
            }
            catch { }
        }
        return null;
    }

    // External helper: allow other UI to switch modes
    public void SetModePlaceBin() { currentMode = Mode.PlaceBin; placementEnabled = true; if (previewInstance) previewInstance.SetActive(true); }
    public void SetModePlaceFan() { currentMode = Mode.PlaceFan; placementEnabled = true; if (previewInstance) previewInstance.SetActive(true); }

    // Allow external systems (MRUK) to set floor Y if you get it from MRUK
    public void SetFloorY(float y) { floorY = y; }
}

// using System;
// using System.Collections;
// using UnityEngine;

// public class PlacementManager : MonoBehaviour
// {
//     public enum Mode { PlaceBin, PlaceFan }

//     [Header("Prefabs")]
//     public GameObject binPrefab;           // dustbin prefab
//     public GameObject fanPrefab;           // fan prefab
//     public GameObject previewPrefab;       // ghost preview object (semi-transparent model)

//     [Header("References")]
//     public Transform rightHandAnchor;      // assign: RealHands/RightHandAnchor (or controller transform)
//     public Transform cameraTransform;      // assign: Main Camera (optional; auto-find)

//     [Header("Ray & Floor")]
//     public float maxRaycastDistance = 10f;
//     public LayerMask raycastMask = ~0;     // layers to hit with physics raycast
//     public float floorY = 0f;              // fallback floor Y (if no hits) — set to 0 or MRUK-found value
//     public float floorYOffset = 0.10f;     // place objects slightly above floor

//     [Header("Placement rules")]
//     public float placeCooldown = 0.2f;
//     public float fanMaxAngleDeg = 40f;     // allowed angle from camera->bin direction for fan
//     public float fanMaxDistanceExtra = 0.5f; // fan shouldn't be further than bin + this
//     public float fanMinBetween = 0.15f;    // fraction along cam->bin that fan must be (0..1)
//     public float fanMaxBetween = 0.95f;

//     [Header("Visuals / Debug")]
//     public Color previewValidColor = Color.green;
//     public Color previewInvalidColor = Color.red;
//     public bool logDebug = true;

//     // runtime
//     GameObject previewInstance;
//     Mode currentMode = Mode.PlaceBin;
//     bool canPlace = true;
//     GameObject placedBin;
//     GameObject placedFan;

//     // Input toggle (NEW - can be changed in Inspector)
//     [Header("Input (toggle placement)")]
//     public OVRInput.Button placementToggleButton = OVRInput.Button.PrimaryHandTrigger; // grip/secondary trigger
//     public OVRInput.Controller placementToggleController = OVRInput.Controller.RTouch;

//     bool placementEnabled = true;

//     // add these fields near the top of the class if you don't already have them:

//     void Start()
//     {
//         if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
//         if (rightHandAnchor == null) Debug.LogWarning("[PlacementManager] rightHandAnchor not assigned!");

//         if (previewPrefab != null)
//         {
//             previewInstance = Instantiate(previewPrefab);
//             previewInstance.name = "Preview_" + previewPrefab.name;
//             // remove colliders so preview doesn't block raycasts
//             foreach (var c in previewInstance.GetComponentsInChildren<Collider>()) Destroy(c);
//             previewInstance.SetActive(true);
//         }
//         else
//         {
//             Debug.LogWarning("[PlacementManager] previewPrefab not assigned.");
//         }
//     }

//     void Update()
//     {
//         // if (previewInstance == null || rightHandAnchor == null) return;

//         // // update preview position each frame
//         // UpdatePreview();

//         // // place on right-hand index trigger down (RTouch)
//         // if (canPlace && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
//         // {
//         //     if (logDebug) Debug.Log("[PlacementManager] Trigger pressed -> TryPlaceAtPreview()");
//         //     TryPlaceAtPreview();
//         // }

//         // // optional quick mode switch with Button.Two on right controller
//         // if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
//         // {
//         //     currentMode = (currentMode == Mode.PlaceBin) ? Mode.PlaceFan : Mode.PlaceBin;
//         //     if (logDebug) Debug.Log("[PlacementManager] Mode switched -> " + currentMode);
//         // }
//         if (previewInstance == null || rightHandAnchor == null) return;

//         // --- NEW: toggle placement mode on secondary/grip press ---
//         if (OVRInput.GetDown(placementToggleButton, placementToggleController))
//         {
//             placementEnabled = !placementEnabled;
//             if (previewInstance != null) previewInstance.SetActive(placementEnabled);
//             if (logDebug) Debug.Log("[PlacementManager] placementEnabled -> " + placementEnabled);
//         }

//         // If placement is disabled, do not update preview or allow placement
//         if (!placementEnabled)
//         {
//             // Early return so index trigger (and A button etc.) can be used by other systems (e.g., throwing)
//             return;
//         }

//         // update preview position each frame
//         UpdatePreview();

//         // place on right-hand index trigger down (RTouch)
//         if (canPlace && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
//         {
//             if (logDebug) Debug.Log("[PlacementManager] Trigger pressed -> TryPlaceAtPreview()");
//             TryPlaceAtPreview();
//         }

//         // optional quick mode switch with Button.Two on right controller
//         if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
//         {
//             currentMode = (currentMode == Mode.PlaceBin) ? Mode.PlaceFan : Mode.PlaceBin;
//             if (logDebug) Debug.Log("[PlacementManager] Mode switched -> " + currentMode);
//         }
//     }

    

//     // --- Replace the existing UpdatePreview() with this ---
// void UpdatePreview()
// {
//     Vector3 origin = rightHandAnchor.position;
//     Vector3 dir = rightHandAnchor.forward.normalized;
//     Ray r = new Ray(origin, dir);

//     Vector3 candidatePos;
//     Quaternion candidateRot = rightHandAnchor.rotation;

//     RaycastHit hit;
//     bool hitSomething = Physics.Raycast(r, out hit, maxRaycastDistance, raycastMask);

//     if (hitSomething)
//     {
//         // Use the real hit point if we hit geometry
//         candidatePos = hit.point;

//         // If hit y is close to floorY, snap y to floorY+offset to avoid tiny floats
//         if (Mathf.Abs(candidatePos.y - floorY) < 0.5f)
//             candidatePos.y = floorY + floorYOffset;

//         // face camera horizontally if camera assigned
//         if (cameraTransform != null)
//         {
//             Vector3 toCam = cameraTransform.position - candidatePos; toCam.y = 0;
//             if (toCam.sqrMagnitude > 0.001f) candidateRot = Quaternion.LookRotation(toCam, Vector3.up);
//         }
//     }
//     else
//     {
//         // No hit -> try to find intersection of the ray with horizontal plane at floorY
//         bool intersects = GetRayFloorIntersection(r, floorY, out Vector3 floorHit, out float t);

//         if (intersects && t > 0f && t <= maxRaycastDistance)
//         {
//             // Use the intersection point (so preview projects far away to where the ray meets the floor)
//             candidatePos = floorHit;
//             candidatePos.y = floorY + floorYOffset; // ensure consistent offset
//             // rotate to face camera horizontally
//             if (cameraTransform != null)
//             {
//                 Vector3 toCam = cameraTransform.position - candidatePos; toCam.y = 0;
//                 if (toCam.sqrMagnitude > 0.001f) candidateRot = Quaternion.LookRotation(toCam, Vector3.up);
//             }
//         }
//         else
//         {
//             // Ray doesn't hit floor in front (ray might be near-horizontal or pointing upwards).
//             // Fallback: place preview at a default distance forward, but projected down to floorY so it doesn't float.
//             float fallbackDistance = Mathf.Min(maxRaycastDistance, 3.0f); // you can adjust fallback distance
//             Vector3 forwardPoint = origin + dir * fallbackDistance;
//             forwardPoint.y = floorY + floorYOffset;
//             candidatePos = forwardPoint;
//             candidateRot = Quaternion.LookRotation(dir, Vector3.up);
//         }
//     }

//     // validation
//     bool valid = true;
//     if (currentMode == Mode.PlaceFan)
//         valid = IsFanPlacementValid(candidatePos);

//     // update preview transform + color
//     previewInstance.transform.position = candidatePos;
//     previewInstance.transform.rotation = candidateRot;
//     SetPreviewColor(valid ? previewValidColor : previewInvalidColor);
// }

// // --- Add this helper function somewhere in the class (e.g., below UpdatePreview) ---
// bool GetRayFloorIntersection(Ray ray, float floorY, out Vector3 hitPoint, out float t)
// {
//     hitPoint = Vector3.zero;
//     t = 0f;

//     Vector3 origin = ray.origin;
//     Vector3 dir = ray.direction;

//     // If ray direction y is zero (parallel to plane) there's no intersection or it's infinite
//     if (Mathf.Abs(dir.y) < 1e-5f) return false;

//     // Solve origin.y + t * dir.y = floorY  => t = (floorY - origin.y) / dir.y
//     t = (floorY - origin.y) / dir.y;
//     if (t < 0f) return false; // intersection is behind origin

//     hitPoint = origin + dir * t;
//     return true;
// }

//     void TryPlaceAtPreview()
//     {
//         if (previewInstance == null) return;

//         Vector3 pos = previewInstance.transform.position;
//         Quaternion rot = previewInstance.transform.rotation;

//         // ensure not tiny distance from camera (finger accidentally pressed)
//         if (cameraTransform != null && (pos - cameraTransform.position).sqrMagnitude < 0.01f)
//         {
//             if (logDebug) Debug.Log("[PlacementManager] Aborting: preview too close to camera.");
//             return;
//         }

//         // validation
//         if (currentMode == Mode.PlaceBin)
//         {
//             PlacePrefabAt(binPrefab, pos, rot, (g) => placedBin = g);
//             currentMode = Mode.PlaceFan;    // require fan next
//         }
//         else // PlaceFan
//         {
//             if (placedBin == null)
//             {
//                 if (logDebug) Debug.Log("[PlacementManager] Place the bin first.");
//                 return;
//             }

//             if (!IsFanPlacementValid(pos))
//             {
//                 if (logDebug) Debug.Log("[PlacementManager] Fan placement invalid — must be between you and the bin and in FOV.");
//                 // optional: play invalid sfx here
//                 return;
//             }

//             PlacePrefabAt(fanPrefab, pos, rot, (g) => placedFan = g);
//         }

//         StartCoroutine(PlacementCooldown());
//     }

//     IEnumerator PlacementCooldown()
//     {
//         canPlace = false;
//         yield return new WaitForSeconds(placeCooldown);
//         canPlace = true;
//     }

//     void PlacePrefabAt(GameObject prefab, Vector3 pos, Quaternion rot, Action<GameObject> onPlaced)
//     {
//         if (prefab == null)
//         {
//             Debug.LogWarning("[PlacementManager] Missing prefab to place.");
//             return;
//         }

//         GameObject go = Instantiate(prefab, pos, rot);
//         go.transform.parent = null;

//         // try to remove grabbable components by name (safe against missing packages)
//         RemoveComponentByName(go, "OVRGrabbable");
//         RemoveComponentByName(go, "XRGrabInteractable");

//         // set rigidbody to kinematic if exists
//         var rb = go.GetComponent<Rigidbody>();
//         if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

//         // add spatial anchor if OVRSpatialAnchor type exists (reflection)
//         TryAddSpatialAnchor(go);

//         onPlaced?.Invoke(go);

//         if (logDebug) Debug.Log("[PlacementManager] Placed " + prefab.name + " at " + pos);
//     }

//     // Validation rule for fan: roughly between camera and bin and in front
//     bool IsFanPlacementValid(Vector3 fanPos)
//     {
//         if (placedBin == null || cameraTransform == null) return false;

//         Vector3 camPos = cameraTransform.position;
//         Vector3 binPos = placedBin.transform.position;

//         Vector3 camToBin = binPos - camPos;
//         float camToBinLen = camToBin.magnitude;
//         if (camToBinLen < 0.1f) return false;
//         Vector3 axisDir = camToBin / camToBinLen;

//         // projection t along the axis (in world units / normalized by axis length)
//         Vector3 rel = fanPos - camPos;
//         float proj = Vector3.Dot(rel, axisDir) / camToBinLen;

//         if (proj < fanMinBetween || proj > fanMaxBetween) return false;

//         // perpendicular distance from axis
//         Vector3 closestOnAxis = camPos + axisDir * Vector3.Dot(rel, axisDir);
//         float perpDist = Vector3.Distance(fanPos, closestOnAxis);
//         float maxPerpAllowed = Mathf.Lerp(0.3f, 1.5f, Mathf.Clamp01(camToBinLen / 5f));
//         if (perpDist > maxPerpAllowed) return false;

//         // angle relative to camera forward: ensure roughly in front
//         float angle = Vector3.Angle(cameraTransform.forward, (fanPos - camPos));
//         if (angle > fanMaxAngleDeg) return false;

//         // distance-wise: fan shouldn't be much further than bin
//         float distFan = Vector3.Distance(camPos, fanPos);
//         if (distFan > camToBinLen + fanMaxDistanceExtra) return false;

//         return true;
//     }

//     void SetPreviewColor(Color c)
//     {
//         if (previewInstance == null) return;
//         var rends = previewInstance.GetComponentsInChildren<Renderer>();
//         foreach (var r in rends)
//         {
//             // make an instance material so we don't overwrite shared material permanently
//             if (r.material != null) r.material.color = c;
//         }
//     }

//     void RemoveComponentByName(GameObject go, string typeName)
//     {
//         var t = Type.GetType(typeName) ?? FindTypeInAppDomain(typeName);
//         if (t == null) return;
//         var comp = go.GetComponent(t);
//         if (comp != null) Destroy(comp);
//     }

//     void TryAddSpatialAnchor(GameObject go)
//     {
//         // safe reflection add to avoid compile errors if OVRSpatialAnchor isn't available at compile time.
//         Type anchorType = FindTypeInAppDomain("OVRSpatialAnchor") ?? FindTypeInAppDomain("Unity.XR.Oculus.OVRSpatialAnchor");
//         if (anchorType != null && go.GetComponent(anchorType) == null)
//         {
//             go.AddComponent(anchorType);
//             if (logDebug) Debug.Log("[PlacementManager] Added spatial anchor via reflection.");
//         }
//     }

//     // helper: search loaded assemblies for type by name
//     Type FindTypeInAppDomain(string shortName)
//     {
//         foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
//         {
//             try
//             {
//                 foreach (var t in asm.GetTypes())
//                 {
//                     if (t.Name == shortName || t.FullName == shortName) return t;
//                 }
//             }
//             catch { /* ignore reflection issues */ }
//         }
//         return null;
//     }

//     // External helper: allow other UI to switch modes
//     public void SetModePlaceBin() { currentMode = Mode.PlaceBin; }
//     public void SetModePlaceFan() { currentMode = Mode.PlaceFan; }

//     // Allow external systems (MRUK) to set floor Y if you get it from MRUK
//     public void SetFloorY(float y) { floorY = y; }
// }






