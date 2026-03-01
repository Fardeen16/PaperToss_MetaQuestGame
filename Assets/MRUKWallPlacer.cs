using UnityEngine;
using Meta.XR.MRUtilityKit;
using System.Collections;

public class MRUKWallPlacer : MonoBehaviour
{
    [Header("Prefab to Place on Floor")]
    public GameObject dustbinPrefab;

    [Header("References")]
    public Transform rightHandAnchor;

    [Header("Settings")]
    public float raycastDistance = 10f;

    private bool mrukReady = false;
    private float floorY = 0f;

    void Start()
    {
        StartCoroutine(WaitForMRUK());
    }

    IEnumerator WaitForMRUK()
    {
        while (MRUK.Instance == null)
            yield return new WaitForSeconds(0.3f);

        while (MRUK.Instance.GetCurrentRoom() == null)
            yield return new WaitForSeconds(0.3f);

        FindFloorY();
        mrukReady = true;
        Debug.Log($"[FloorPlacer] Ready! Floor Y = {floorY}");
    }

    void FindFloorY()
    {
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null) return;

        foreach (MRUKAnchor anchor in room.Anchors)
        {
            if ((anchor.Label & MRUKAnchor.SceneLabels.FLOOR) != 0)
            {
                floorY = anchor.transform.position.y;
                Debug.Log($"[FloorPlacer] Found FLOOR anchor at Y={floorY}");
                return;
            }
        }

        floorY = room.FloorAnchor != null
            ? room.FloorAnchor.transform.position.y
            : 0f;

        Debug.Log($"[FloorPlacer] FloorAnchor fallback Y={floorY}");
    }

    void Update()
    {
        if (!mrukReady) return;

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            TryRaycastThenPlace();
        }
    }

    void TryRaycastThenPlace()
    {
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            PlaceDustbin(GetFloorPositionBelowController());
            return;
        }

        // Aim ray from controller forward direction (where you're pointing)
        Ray aimRay = new Ray(rightHandAnchor.position, rightHandAnchor.forward);

        bool hit = room.Raycast(
            aimRay,
            raycastDistance,
            new LabelFilter(MRUKAnchor.SceneLabels.FLOOR),
            out RaycastHit hitInfo,
            out MRUKAnchor hitAnchor
        );

        if (hit)
        {
            Debug.Log($"[FloorPlacer] Raycast hit floor at {hitInfo.point}");
            PlaceDustbin(hitInfo.point);
        }
        else
        {
            // Fallback: project controller forward ray onto stored floor Y plane
            Ray ray = new Ray(rightHandAnchor.position, rightHandAnchor.forward);
            float t = (floorY - ray.origin.y) / ray.direction.y;
            if (t > 0)
            {
                Vector3 projected = ray.GetPoint(t);
                Debug.Log($"[FloorPlacer] Projected aim onto floor at {projected}");
                PlaceDustbin(projected);
            }
            else
            {
                Debug.Log("[FloorPlacer] Aim not toward floor, using below controller");
                PlaceDustbin(GetFloorPositionBelowController());
            }
        }
    }

    Vector3 GetFloorPositionBelowController()
    {
        return new Vector3(
            rightHandAnchor.position.x,
            floorY,
            rightHandAnchor.position.z
        );
    }

    void PlaceDustbin(Vector3 floorPoint)
    {
        Vector3 spawnPos = new Vector3(floorPoint.x, floorPoint.y + 0.10f, floorPoint.z);

        Vector3 dirToCamera = Camera.main.transform.position - spawnPos;
        dirToCamera.y = 0;
        Quaternion spawnRot = dirToCamera.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(dirToCamera)
            : Quaternion.identity;

        GameObject placed = Instantiate(dustbinPrefab, spawnPos, spawnRot);

        placed.AddComponent<OVRSpatialAnchor>();

        Rigidbody rb = placed.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        OVRGrabbable grabbable = placed.GetComponent<OVRGrabbable>();
        if (grabbable != null) Destroy(grabbable);

        Debug.Log($"[FloorPlacer] Dustbin placed at {spawnPos}");
    }
}