using UnityEngine;
using Meta.XR.MRUtilityKit; // adjust if namespace differs
using System.Collections;

public class Placer : MonoBehaviour
{
    public enum PlacementType { Bin, Fan }

    [Header("Prefabs")]
    public GameObject binPrefab;
    public GameObject fanPrefab;
    public GameObject previewPrefab;

    [Header("References")]
    public Transform rightHandAnchor; // OVRCameraRig/TrackingSpace/RightHandAnchor
    public InteractionManager interactionManager;
    public AudioClip invalidPlaceSfx;
    public AudioClip placedSfx;
    public GameObject scoreParticlePrefab;

    [Header("Placement")]
    public float raycastDistance = 10f;
    public float fanMaxAngleDeg = 45f; // relative to camera forward
    public float fanAxisTolerance = 0.6f; // how close to camera->bin axis (0..1)
    public float minBetweenT = 0.15f, maxBetweenT = 0.95f;

    GameObject previewInstance;
    PlacementType currentType = PlacementType.Bin;
    bool previewActive = false;

    // references to placed objects
    public GameObject placedBin;
    public GameObject placedFan;

    void Start()
    {
        if (previewPrefab != null)
        {
            previewInstance = Instantiate(previewPrefab);
            // make it non-interactable
            foreach (var c in previewInstance.GetComponentsInChildren<Collider>()) Destroy(c);
            previewInstance.SetActive(false);
        }
    }

    public void SetPlacementType(PlacementType t)
    {
        currentType = t;
        previewActive = true;
        if (previewInstance) previewInstance.SetActive(true);
    }

    void Update()
    {
        if (!previewActive) return;
        UpdatePreview();

        // place on trigger press
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            TryPlace();
        }
    }

    void UpdatePreview()
    {
        Vector3 origin = rightHandAnchor.position;
        Quaternion rot = rightHandAnchor.rotation;
        Ray ray = new Ray(origin, rot * Vector3.forward);

        MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
        bool hit = false;
        RaycastHit hitInfo = default;
        MRUKAnchor mrukAnchor = null;

        if (room != null)
        {
            // choose label filter: floor for bin, any surface or wall for fan (we still constrain fan later)
            LabelFilter filter = (currentType == PlacementType.Bin)
                ? new LabelFilter(MRUKAnchor.SceneLabels.FLOOR)
                : new LabelFilter(MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.OTHER);

            hit = room.Raycast(ray, raycastDistance, filter, out hitInfo, out mrukAnchor);
        }

        Vector3 candidatePos;
        Quaternion candidateRot = Quaternion.identity;
        bool valid = false;

        if (hit)
        {
            candidatePos = hitInfo.point;
            // rotate to face camera horizontally
            Vector3 toCam = Camera.main.transform.position - candidatePos; toCam.y = 0;
            candidateRot = toCam.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toCam) : Quaternion.identity;

            // check fan constraints if placing fan
            if (currentType == PlacementType.Fan)
                valid = IsValidFanPosition(candidatePos);
            else valid = true;
        }
        else
        {
            // fallback: place a short distance in front of controller projected to floor Y
            candidatePos = origin + (rot * Vector3.forward) * 0.6f;
            candidateRot = rot;
            valid = (currentType == PlacementType.Bin); // only allow fallback for bin; fan needs MRUK constraint
        }

        // update preview visuals (green/red)
        if (previewInstance)
        {
            previewInstance.transform.position = candidatePos;
            previewInstance.transform.rotation = candidateRot;
            SetPreviewColor(valid ? Color.green : Color.red);
        }

        previewInstance.SetActive(true);
    }

    bool IsValidFanPosition(Vector3 candidate)
    {
        if (placedBin == null) return false;

        Transform cam = Camera.main.transform;
        Vector3 camPos = cam.position;
        Vector3 binPos = placedBin.transform.position;

        // axis from camera to bin
        Vector3 axis = (binPos - camPos);
        float axisLen = axis.magnitude;
        if (axisLen < 0.05f) return false;
        Vector3 axisDir = axis / axisLen;

        // projection of candidate onto axis
        Vector3 rel = candidate - camPos;
        float t = Vector3.Dot(rel, axisDir) / axisLen; // normalized 0..1 along cam->bin
        // but here t is fraction along axis in absolute units; convert to 0..1
        float proj = Vector3.Dot(rel, axisDir) / axisLen;

        if (proj < minBetweenT || proj > maxBetweenT) return false;

        // distance perpendicular to axis
        Vector3 along = camPos + axisDir * Vector3.Dot(rel, axisDir);
        float perpDist = Vector3.Distance(candidate, along);

        // allow some tolerance relative to axis length
        float maxPerp = Mathf.Lerp(0.3f, 1.5f, Mathf.Clamp01(axisLen / 5f)); // adjust scale
        if (perpDist > maxPerp * fanAxisTolerance) return false;

        // angle constraint to camera forward
        float angle = Vector3.Angle(cam.forward, (candidate - camPos));
        if (angle > fanMaxAngleDeg) return false;

        return true;
    }

    void TryPlace()
    {
        Vector3 origin = rightHandAnchor.position;
        Quaternion rot = rightHandAnchor.rotation;
        Ray ray = new Ray(origin, rot * Vector3.forward);

        MRUKRoom room = MRUK.Instance?.GetCurrentRoom();
        bool hit = false;
        RaycastHit hitInfo = default;
        MRUKAnchor mrukAnchor = null;

        if (room != null)
        {
            LabelFilter filter = (currentType == PlacementType.Bin)
                ? new LabelFilter(MRUKAnchor.SceneLabels.FLOOR)
                : new LabelFilter(MRUKAnchor.SceneLabels.WALL_FACE | MRUKAnchor.SceneLabels.FLOOR | MRUKAnchor.SceneLabels.OTHER);

            hit = room.Raycast(ray, raycastDistance, filter, out hitInfo, out mrukAnchor);
        }

        Vector3 placePos;
        Quaternion placeRot;
        bool allowPlace = false;

        if (hit)
        {
            placePos = hitInfo.point;
            Vector3 toCam = Camera.main.transform.position - placePos; toCam.y = 0;
            placeRot = toCam.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toCam) : Quaternion.identity;

            if (currentType == PlacementType.Fan)
                allowPlace = IsValidFanPosition(placePos);
            else allowPlace = true;
        }
        else
        {
            if (currentType == PlacementType.Bin)
            {
                placePos = origin + (rot * Vector3.forward) * 0.6f;
                placeRot = rot;
                allowPlace = true;
            }
            else
            {
                allowPlace = false;
                placePos = Vector3.zero;
                placeRot = Quaternion.identity;
            }
        }

        if (!allowPlace)
        {
            // feedback invalid
            if (invalidPlaceSfx) AudioSource.PlayClipAtPoint(invalidPlaceSfx, Camera.main.transform.position);
            return;
        }

        // instantiate and anchor
        GameObject go = Instantiate(
            currentType == PlacementType.Bin ? binPrefab : fanPrefab,
            placePos, placeRot
        );
        go.transform.parent = null;

        // remove grabbable if exists
        var g = go.GetComponent<OVRGrabbable>();
        if (g != null) Destroy(g);

        // set rigidbody static
        var rb = go.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        // add spatial anchor
        if (go.GetComponent<OVRSpatialAnchor>() == null) go.AddComponent<OVRSpatialAnchor>();

        // store references
        if (currentType == PlacementType.Bin) placedBin = go;
        if (currentType == PlacementType.Fan) placedFan = go;

        // notify manager
        interactionManager.NotifyPlaced(currentType);

        // sound
        if (placedSfx) AudioSource.PlayClipAtPoint(placedSfx, placePos);

        // hide preview and stop preview mode
        previewActive = false;
        if (previewInstance) previewInstance.SetActive(false);
    }

    void SetPreviewColor(Color c)
    {
        var rends = previewInstance.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            if (r.material != null) r.material.color = c;
        }
    }
}