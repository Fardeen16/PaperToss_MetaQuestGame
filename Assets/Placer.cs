// using UnityEngine;
// using Meta.XR.MRUtilityKit; // adjust if namespace differs
// using System.Collections;

// public class Placer : MonoBehaviour
// {
//     public enum PlacementType { Bin, Fan }

//     [Header("Prefabs")]
//     public GameObject binPrefab;
//     public GameObject fanPrefab;
//     public GameObject previewPrefab;

//     [Header("References")]
//     public Transform rightHandAnchor; // OVRCameraRig/TrackingSpace/RightHandAnchor
//     public InteractionManager interactionManager;
//     public AudioClip invalidPlaceSfx;
//     public AudioClip placedSfx;
//     public GameObject scoreParticlePrefab;

//     [Header("Placement")]
//     public float raycastDistance = 10f;
//     public float fanMaxAngleDeg = 45f; // relative to camera forward
//     public float fanAxisTolerance = 0.6f; // how close to camera->bin axis (0..1)
//     public float minBetweenT = 0.15f, maxBetweenT = 0.95f;

//     GameObject previewInstance;
//     PlacementType currentType = PlacementType.Bin;
//     bool previewActive = false;

//     // references to placed objects
//     public GameObject placedBin;
//     public GameObject placedFan;

//     void Start()
//     {
//         if (previewPrefab != null)
//         {
//             previewInstance = Instantiate(previewPrefab);
//             // make it non-interactable
//             foreach (var c in previewInstance.GetComponentsInChildren<Collider>()) Destroy(c);
//             previewInstance.SetActive(false);
//         }
//     }

//     public void SetPlacementType(PlacementType t)
//     {
//         currentType = t;
//         previewActive = true;
//         if (previewInstance) previewInstance.SetActive(true);
//     }

//     void Update()
//     {
//         if (!previewActive) return;
//         UpdatePreview();

//         // place on trigger press
//         if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
//         {
//             TryPlace();
//         }
//     }

//     void UpdatePreview()
//     {
//         Vector3 origin = rightHandAnchor.position;
//         Quaternion rot = rightHandAnchor.rotation;
//         Ray ray = new Ray(origin, rot * Vector3.forward);

//         MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
//         bool hit = false;
//         RaycastHit hitInfo = default;
//         MRUKAnchor mrukAnchor = null;

//         if (room != null)
//         {
//             // choose label filter: floor for bin, any surface or wall for fan (we still constrain fan later)
//             LabelFilter filter = (currentType == PlacementType.Bin)
//                 ? new LabelFilter(MRUKAnchor.SceneLabels.FLOOR)
//                 : new LabelFilter(MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.OTHER);

//             hit = room.Raycast(ray, raycastDistance, filter, out hitInfo, out mrukAnchor);
//         }

//         Vector3 candidatePos;
//         Quaternion candidateRot = Quaternion.identity;
//         bool valid = false;

//         if (hit)
//         {
//             candidatePos = hitInfo.point;
//             // rotate to face camera horizontally
//             Vector3 toCam = Camera.main.transform.position - candidatePos; toCam.y = 0;
//             candidateRot = toCam.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toCam) : Quaternion.identity;

//             // check fan constraints if placing fan
//             if (currentType == PlacementType.Fan)
//                 valid = IsValidFanPosition(candidatePos);
//             else valid = true;
//         }
//         else
//         {
//             // fallback: place a short distance in front of controller projected to floor Y
//             candidatePos = origin + (rot * Vector3.forward) * 0.6f;
//             candidateRot = rot;
//             valid = (currentType == PlacementType.Bin); // only allow fallback for bin; fan needs MRUK constraint
//         }

//         // update preview visuals (green/red)
//         if (previewInstance)
//         {
//             previewInstance.transform.position = candidatePos;
//             previewInstance.transform.rotation = candidateRot;
//             SetPreviewColor(valid ? Color.green : Color.red);
//         }

//         previewInstance.SetActive(true);
//     }

//     bool IsValidFanPosition(Vector3 candidate)
//     {
//         if (placedBin == null) return false;

//         Transform cam = Camera.main.transform;
//         Vector3 camPos = cam.position;
//         Vector3 binPos = placedBin.transform.position;

//         // axis from camera to bin
//         Vector3 axis = (binPos - camPos);
//         float axisLen = axis.magnitude;
//         if (axisLen < 0.05f) return false;
//         Vector3 axisDir = axis / axisLen;

//         // projection of candidate onto axis
//         Vector3 rel = candidate - camPos;
//         float t = Vector3.Dot(rel, axisDir) / axisLen; // normalized 0..1 along cam->bin
//         // but here t is fraction along axis in absolute units; convert to 0..1
//         float proj = Vector3.Dot(rel, axisDir) / axisLen;

//         if (proj < minBetweenT || proj > maxBetweenT) return false;

//         // distance perpendicular to axis
//         Vector3 along = camPos + axisDir * Vector3.Dot(rel, axisDir);
//         float perpDist = Vector3.Distance(candidate, along);

//         // allow some tolerance relative to axis length
//         float maxPerp = Mathf.Lerp(0.3f, 1.5f, Mathf.Clamp01(axisLen / 5f)); // adjust scale
//         if (perpDist > maxPerp * fanAxisTolerance) return false;

//         // angle constraint to camera forward
//         float angle = Vector3.Angle(cam.forward, (candidate - camPos));
//         if (angle > fanMaxAngleDeg) return false;

//         return true;
//     }

//     void TryPlace()
//     {
//         Vector3 origin = rightHandAnchor.position;
//         Quaternion rot = rightHandAnchor.rotation;
//         Ray ray = new Ray(origin, rot * Vector3.forward);

//         MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
//         bool hit = false;
//         RaycastHit hitInfo = default;
//         MRUKAnchor mrukAnchor = null;

//         if (room != null)
//         {
//             LabelFilter filter = (currentType == PlacementType.Bin)
//                 ? new LabelFilter(MRUKAnchor.SceneLabels.FLOOR)
//                 : new LabelFilter(MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.OTHER);

//             hit = room.Raycast(ray, raycastDistance, filter, out hitInfo, out mrukAnchor);
//         }

//         Vector3 placePos;
//         Quaternion placeRot;
//         bool allowPlace = false;

//         if (hit)
//         {
//             placePos = hitInfo.point;
//             Vector3 toCam = Camera.main.transform.position - placePos; toCam.y = 0;
//             placeRot = toCam.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toCam) : Quaternion.identity;

//             if (currentType == PlacementType.Fan)
//                 allowPlace = IsValidFanPosition(placePos);
//             else allowPlace = true;
//         }
//         else
//         {
//             if (currentType == PlacementType.Bin)
//             {
//                 placePos = origin + (rot * Vector3.forward) * 0.6f;
//                 placeRot = rot;
//                 allowPlace = true;
//             }
//             else
//             {
//                 allowPlace = false;
//                 placePos = Vector3.zero;
//                 placeRot = Quaternion.identity;
//             }
//         }

//         if (!allowPlace)
//         {
//             // feedback invalid
//             if (invalidPlaceSfx) AudioSource.PlayClipAtPoint(invalidPlaceSfx, Camera.main.transform.position);
//             return;
//         }

//         // instantiate and anchor
//         GameObject go = Instantiate(
//             currentType == PlacementType.Bin ? binPrefab : fanPrefab,
//             placePos, placeRot
//         );
//         go.transform.parent = null;

//         // remove grabbable if exists
//         var g = go.GetComponent<OVRGrabbable>();
//         if (g != null) Destroy(g);

//         // set rigidbody static
//         var rb = go.GetComponent<Rigidbody>();
//         if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

//         // add spatial anchor
//         if (go.GetComponent<OVRSpatialAnchor>() == null) go.AddComponent<OVRSpatialAnchor>();

//         // store references
//         if (currentType == PlacementType.Bin) placedBin = go;
//         if (currentType == PlacementType.Fan) placedFan = go;

//         // notify manager
//         interactionManager.NotifyPlaced(currentType);

//         // sound
//         if (placedSfx) AudioSource.PlayClipAtPoint(placedSfx, placePos);

//         // hide preview and stop preview mode
//         previewActive = false;
//         if (previewInstance) previewInstance.SetActive(false);
//     }

//     void SetPreviewColor(Color c)
//     {
//         var rends = previewInstance.GetComponentsInChildren<Renderer>();
//         foreach (var r in rends)
//         {
//             if (r.material != null) r.material.color = c;
//         }
//     }
// }


// Placer.cs (updated)
// Requires: Meta.XR.MRUtilityKit & OVR packages
using UnityEngine;
using Meta.XR.MRUtilityKit; // adjust if namespace differs
using System.Collections;

public class Placer : MonoBehaviour
{
    public enum PlacementType { Bin, Fan }

    [Header("Prefabs")]
    public GameObject binPrefab;
    public GameObject fanPrefab;

    // Two separate preview prefabs so we can swap easily
    public GameObject binPreviewPrefab;
    public GameObject fanPreviewPrefab;

    [Header("References")]
    public Transform rightHandAnchor; // OVRCameraRig/TrackingSpace/RightHandAnchor
    public InteractionManager interactionManager;
    public AudioClip invalidPlaceSfx;
    public AudioClip placedSfx;
    public GameObject scoreParticlePrefab;

    [Header("Placement")]
    public float raycastDistance = 10f;
    public float fanMaxAngleDeg = 45f; // relative to camera forward
    public float fanAxisTolerance = 0.6f; // multiplier on perpendicular allowance
    public float minBetweenT = 0.15f, maxBetweenT = 0.95f;

    // preview instance currently shown
    GameObject previewInstance;
    PlacementType currentType = PlacementType.Bin;
    bool previewActive = false;

    // references to placed objects
    public GameObject placedBin;
    public GameObject placedFan;

    // cached MRUK & camera
    MRUKRoom cachedRoom;
    Transform cam;

    void Awake()
    {
        if (rightHandAnchor == null) Debug.LogWarning("[Placer] rightHandAnchor not assigned.");
        cam = Camera.main?.transform;
        if (cam == null) Debug.LogWarning("[Placer] Main Camera not found.");
    }

    void Start()
    {
        // create preview instance for the initial type
        CreatePreviewForType(currentType);
    }

    /// <summary>Switch active placement type and ensure preview visible.</summary>
    public void SetPlacementType(PlacementType t)
    {
        currentType = t;
        previewActive = true;
        CreatePreviewForType(t);
    }

    void CreatePreviewForType(PlacementType t)
    {
        // destroy old preview if any
        if (previewInstance != null) Destroy(previewInstance);

        GameObject prefab = (t == PlacementType.Bin) ? binPreviewPrefab : fanPreviewPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[Placer] Preview prefab missing for " + t);
            previewInstance = null;
            return;
        }

        previewInstance = Instantiate(prefab);
        // remove colliders so preview doesn't block raycasts
        foreach (var c in previewInstance.GetComponentsInChildren<Collider>())
            Destroy(c);

        // start hidden; UpdatePreview will enable when valid
        previewInstance.SetActive(false);
    }

    void Update()
    {
        if (!previewActive) return;

        // update the live MRUK room reference (if available)
        cachedRoom = MRUK.Instance?.GetCurrentRoom();

        // update preview position each frame
        UpdatePreview();

        // place on trigger press
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            TryPlace();
        }
    }

    void UpdatePreview()
    {
        if (previewInstance == null || rightHandAnchor == null) return;

        // Ray from controller forward
        Vector3 origin = rightHandAnchor.position;
        Vector3 forward = rightHandAnchor.forward;
        Ray ray = new Ray(origin, forward);

        bool hit = false;
        RaycastHit hitInfo = default;

        // prefer MRUK room raycast (semantic)
        if (cachedRoom != null)
        {
            LabelFilter filter = (currentType == PlacementType.Bin)
                ? new LabelFilter(MRUKAnchor.SceneLabels.FLOOR)
                : new LabelFilter(MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.OTHER);

            MRUKAnchor mrukAnchor;
            hit = cachedRoom.Raycast(ray, raycastDistance, filter, out hitInfo, out mrukAnchor);
        }

        // fallback to physics raycast (project to floor)
        if (!hit)
        {
            // cast a ray down from a point in front of controller to find floor
            Vector3 forwardPoint = origin + forward * 0.6f;
            if (Physics.Raycast(forwardPoint + Vector3.up * 1.0f, Vector3.down, out RaycastHit downHit, 5.0f))
            {
                hit = true;
                hitInfo = downHit;
            }
        }

        Vector3 candidatePos;
        Quaternion candidateRot = Quaternion.identity;
        bool valid = false;

        if (hit)
        {
            // place slightly above the floor (keep preview visible)
            candidatePos = hitInfo.point;
            if (currentType == PlacementType.Bin)
                candidatePos.y += 0.10f; // small offset for bin preview

            // make preview face the camera horizontally
            if (cam != null)
            {
                Vector3 toCam = cam.position - candidatePos;
                toCam.y = 0;
                if (toCam.sqrMagnitude > 0.001f) candidateRot = Quaternion.LookRotation(toCam);
            }
            else candidateRot = Quaternion.identity;

            // validity rules
            valid = (currentType == PlacementType.Bin) ? true : IsValidFanPosition(candidatePos);
        }
        else
        {
            // no valid hit -> hide preview
            previewInstance.SetActive(false);
            return;
        }

        // update preview
        previewInstance.transform.position = candidatePos;
        previewInstance.transform.rotation = candidateRot;
        previewInstance.SetActive(true);
        SetPreviewColor(valid ? Color.green : Color.red);
    }

    bool IsValidFanPosition(Vector3 candidate)
    {
        if (placedBin == null) return false;
        if (cam == null) return false;

        Vector3 camPos = cam.position;
        Vector3 binPos = placedBin.transform.position;

        Vector3 axis = (binPos - camPos);
        float axisLen = axis.magnitude;
        if (axisLen < 0.05f) return false;
        Vector3 axisDir = axis / axisLen;

        // projection (t) of candidate onto camera->bin axis in normalized 0..1 along the segment
        float t = Vector3.Dot(candidate - camPos, axisDir) / axisLen;

        if (t < minBetweenT || t > maxBetweenT) return false;

        // perpendicular distance from axis line
        Vector3 projPoint = camPos + axisDir * (t * axisLen);
        float perpDist = Vector3.Distance(candidate, projPoint);

        // allowable perpendicular distance scales with axis length (so close setups are stricter)
        float maxPerp = Mathf.Lerp(0.3f, 1.5f, Mathf.Clamp01(axisLen / 5f));
        if (perpDist > maxPerp * fanAxisTolerance) return false;

        // angle relative to camera forward
        float angle = Vector3.Angle(cam.forward, (candidate - camPos));
        if (angle > fanMaxAngleDeg) return false;

        // ensure candidate is in front of camera
        if (Vector3.Dot(cam.forward, (candidate - camPos)) < 0f) return false;

        return true;
    }

    void TryPlace()
{
    if (previewInstance == null || !previewInstance.activeInHierarchy)
    {
        Debug.Log("[Placer] Cannot place - preview inactive.");
        return;
    }

    Vector3 placePos = previewInstance.transform.position;
    Quaternion placeRot = previewInstance.transform.rotation;

    bool allow = (currentType == PlacementType.Bin) 
        ? true 
        : IsValidFanPosition(placePos);

    if (!allow)
    {
        if (invalidPlaceSfx)
            AudioSource.PlayClipAtPoint(invalidPlaceSfx, Camera.main.transform.position);
        Debug.Log("[Placer] Placement rejected by validation.");
        return;
    }

    GameObject prefab = (currentType == PlacementType.Bin) 
        ? binPrefab 
        : fanPrefab;

    if (prefab == null)
    {
        Debug.LogWarning("[Placer] Missing prefab for " + currentType);
        return;
    }

    GameObject go = Instantiate(prefab, placePos, placeRot);
    go.transform.parent = null;

    var rb = go.GetComponent<Rigidbody>();
    if (rb != null)
    {
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    var grab = go.GetComponent<OVRGrabbable>();
    if (grab != null) Destroy(grab);

    if (go.GetComponent<OVRSpatialAnchor>() == null)
        go.AddComponent<OVRSpatialAnchor>();

    if (currentType == PlacementType.Bin) placedBin = go;
    if (currentType == PlacementType.Fan) placedFan = go;

    if (interactionManager != null)
        interactionManager.NotifyPlaced(currentType);

    if (placedSfx)
        AudioSource.PlayClipAtPoint(placedSfx, placePos);

    previewActive = false;
    previewInstance.SetActive(false);

    Debug.Log("[Placer] " + currentType + " placed at " + placePos);
}

    void SetPreviewColor(Color c)
    {
        if (previewInstance == null) return;
        var rends = previewInstance.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            // assume preview materials support color setting
            if (r.material != null) r.material.color = c;
        }
    }
}