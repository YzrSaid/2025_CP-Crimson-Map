using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using UnityEngine.UI;

public class UnifiedARNavigationMarkerSpawner : MonoBehaviour
{
    [Header("AR Marker Prefabs")]
    public GameObject buildingMarkerPrefab;
    public GameObject journeyMarkerPrefab;

    [Header("AR Components")]
    public Camera arCamera;
    public ARRaycastManager arRaycastManager;
    public ARPlaneManager arPlaneManager;
    public UnifiedARManager unifiedARManager;

    [Header("Compass Arrow")]
    public CompassNavigationArrow compassArrow;

    [Header("Marker Settings")]
    public float markerScale = 0.3f;
    public float markerHeightOffset = 0.1f;
    public float maxVisibleDistance = 200f;
    public float minMarkerDistance = 2f;

    [Header("Journey Marker Animation")]
    public float pulsateSpeed = 2f;
    public float pulsateMinScale = 0.9f;
    public float pulsateMaxScale = 1.3f;
    public float auraSpeed = 1f;
    public float auraMaxAlpha = 0.5f;

    [Header("Settings")]
    public bool showMarkers = true;

    [Header("Floor Settings")]
    public float floorHeightMeters = 3.048f;

    private List<Node> allNodes = new List<Node>();
    private List<Infrastructure> allInfrastructures = new List<Infrastructure>();
    private Dictionary<string, GameObject> spawnedMarkers = new Dictionary<string, GameObject>();
    private HashSet<string> journeyNodeIds = new HashSet<string>();

    private Vector2 userLocation;
    private DirectionDisplayManager directionManager;
    private bool isARNavigationMode = false;
    private bool isExitingAR = false;

    private Vector2 userXY;
    private float groundPlaneY = 0f;
    private bool markersInitialized = false;

    private List<ARRaycastHit> arRaycastHits = new List<ARRaycastHit>();

    private Vector2 referenceGPS;
    private Vector3 referenceARWorldPosition;

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        if (arRaycastManager == null)
            arRaycastManager = FindObjectOfType<ARRaycastManager>();

        if (arPlaneManager == null)
            arPlaneManager = FindObjectOfType<ARPlaneManager>();

        if (unifiedARManager == null)
            unifiedARManager = FindObjectOfType<UnifiedARManager>();

        directionManager = GetComponent<DirectionDisplayManager>();
        if (directionManager == null)
            directionManager = FindObjectOfType<DirectionDisplayManager>();

        if (compassArrow == null)
            compassArrow = FindObjectOfType<CompassNavigationArrow>();

        DetermineNavigationMode();

        if (isARNavigationMode)
        {
            LoadJourneyNodeIds();
            
            // FIXED: Set ground plane to 0 for proper marker spawning
            groundPlaneY = 0f;
            Debug.Log($"[MarkerSpawner] Ground Plane Y initialized to: {groundPlaneY}");
            
            StartCoroutine(InitializeMarkerSystem());
        }
    }

    private void DetermineNavigationMode()
    {
        string arMode = PlayerPrefs.GetString("ARMode", "DirectAR");
        isARNavigationMode = (arMode == "Navigation");
    }

    private void LoadJourneyNodeIds()
    {
        journeyNodeIds.Clear();
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);

        for (int i = 0; i < pathNodeCount; i++)
        {
            string nodeId = PlayerPrefs.GetString($"ARNavigation_PathNode_{i}", "");
            if (!string.IsNullOrEmpty(nodeId))
            {
                journeyNodeIds.Add(nodeId);
            }
        }
    }

    private IEnumerator InitializeMarkerSystem()
    {
        yield return new WaitForSeconds(0.5f);

        CancelInvoke(nameof(UpdateMarkerSystem));

        yield return new WaitUntil(() => unifiedARManager != null);
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(LoadAllNodesAndInfrastructure());

        referenceGPS = unifiedARManager.GetReferenceGPS();
        referenceARWorldPosition = unifiedARManager.GetReferenceARWorldPosition();

        Debug.Log($"[MarkerSpawner] Reference GPS: {referenceGPS}");
        Debug.Log($"[MarkerSpawner] Reference AR World Position: {referenceARWorldPosition}");

        markersInitialized = true;

        InvokeRepeating(nameof(UpdateMarkerSystem), 0.5f, 0.2f);
    }

    private IEnumerator LoadAllNodesAndInfrastructure()
    {
        string mapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");

        yield return StartCoroutine(LoadNodesData(mapId));
        yield return StartCoroutine(LoadInfrastructureData());
    }

    private IEnumerator LoadNodesData(string mapId)
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    allNodes = nodes.Where(n =>
                        (n.type == "infrastructure" || n.type == "intermediate") && n.is_active
                    ).ToList();
                    loadComplete = true;
                }
                catch (System.Exception)
                {
                    loadComplete = true;
                }
            },
            (error) =>
            {
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
    }

    private IEnumerator LoadInfrastructureData()
    {
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            (jsonContent) =>
            {
                try
                {
                    Infrastructure[] infrastructures = JsonHelper.FromJson<Infrastructure>(jsonContent);
                    allInfrastructures = infrastructures.Where(i => !i.is_deleted).ToList();
                    loadComplete = true;
                }
                catch (System.Exception)
                {
                    loadComplete = true;
                }
            },
            (error) =>
            {
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
    }

    private void UpdateMarkerSystem()
    {
        if (!isARNavigationMode || !markersInitialized)
            return;

        if (unifiedARManager != null)
        {
            userLocation = unifiedARManager.GetUserXY();
            userXY = userLocation;
        }
        else if (GPSManager.Instance != null)
        {
            userLocation = GPSManager.Instance.GetSmoothedCoordinates();
        }

        if (userLocation.magnitude < 0.0001f)
        {
            return;
        }

        UpdateCompassArrow();

        if (showMarkers)
        {
            UpdateAllMarkers();
        }
    }

    private void UpdateCompassArrow()
    {
        if (compassArrow == null || directionManager == null)
            return;

        NavigationDirection currentDir = directionManager.GetCurrentDirection();
        if (currentDir == null || currentDir.destinationNode == null)
            return;

        Node targetNode = currentDir.destinationNode;

        compassArrow.SetTargetNode(targetNode);
        compassArrow.SetActive(true);
    }

    private void UpdateAllMarkers()
    {
        if (buildingMarkerPrefab == null && journeyMarkerPrefab == null)
            return;

        bool isIndoor = (unifiedARManager != null && unifiedARManager.IsIndoorMode());

        List<string> markersToRemove = new List<string>();

        foreach (var kvp in spawnedMarkers)
        {
            Node node = allNodes.FirstOrDefault(n => n.node_id == kvp.Key);
            if (node == null || !ShouldShowMarker(node, isIndoor))
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
                markersToRemove.Add(kvp.Key);
            }
        }

        foreach (string nodeId in markersToRemove)
        {
            spawnedMarkers.Remove(nodeId);
        }

        foreach (Node node in allNodes)
        {
            if (node.type == "indoorinfra")
                continue;

            if (spawnedMarkers.ContainsKey(node.node_id))
                continue;

            if (ShouldShowMarker(node, isIndoor))
            {
                CreateMarkerForNode(node, isIndoor);
            }
        }

        foreach (var marker in spawnedMarkers.Values)
        {
            if (marker != null && arCamera != null)
            {
                marker.transform.LookAt(arCamera.transform);
                marker.transform.Rotate(0, 180, 0);
            }
        }
    }

    private bool ShouldShowMarker(Node node, bool isIndoor)
    {
        if (node.type == "indoorinfra")
            return false;

        float distance = CalculateDistance(node, isIndoor);

        return distance <= maxVisibleDistance && distance >= minMarkerDistance;
    }

    private void CreateMarkerForNode(Node node, bool isIndoor)
    {
        bool isJourneyNode = journeyNodeIds.Contains(node.node_id);
        GameObject prefabToUse = isJourneyNode ? journeyMarkerPrefab : buildingMarkerPrefab;

        if (prefabToUse == null)
        {
            return;
        }

        Infrastructure infra = allInfrastructures.FirstOrDefault(i => i.infra_id == node.related_infra_id);
        if (infra == null && node.type == "infrastructure")
        {
            return;
        }

        Vector3 worldPos = GetNodeWorldPosition(node, isIndoor);

        float floorHeight = 0f;
        if (isIndoor && node.indoor != null && !string.IsNullOrEmpty(node.indoor.floor))
        {
            if (int.TryParse(node.indoor.floor, out int parsedFloor) && parsedFloor > 1)
            {
                floorHeight = (parsedFloor - 1) * floorHeightMeters;
            }
        }

        if (isIndoor)
        {
            worldPos = GetGroundPosition(worldPos);
        }
        else
        {
            worldPos.y = groundPlaneY + markerHeightOffset;
        }

        worldPos.y += floorHeight;

        GameObject marker = Instantiate(prefabToUse, worldPos, Quaternion.identity);
        marker.transform.localScale = Vector3.one * markerScale;

        if (infra != null)
        {
            UpdateMarkerText(marker, infra);
        }
        else
        {
            UpdateMarkerText(marker, node.name);
        }

        if (isJourneyNode)
        {
            StartCoroutine(AnimateJourneyMarker(marker));
        }

        spawnedMarkers[node.node_id] = marker;
    }

    private IEnumerator AnimateJourneyMarker(GameObject marker)
    {
        Vector3 baseScale = marker.transform.localScale;
        float timeOffset = Random.Range(0f, 2f * Mathf.PI);

        Transform auraTransform = marker.transform.Find("Aura");
        Renderer auraRenderer = auraTransform?.GetComponent<Renderer>();

        while (marker != null)
        {
            float pulsate = Mathf.Lerp(pulsateMinScale, pulsateMaxScale,
                (Mathf.Sin(Time.time * pulsateSpeed + timeOffset) + 1f) / 2f);
            marker.transform.localScale = baseScale * pulsate;

            if (auraRenderer != null && auraRenderer.material != null)
            {
                float alpha = Mathf.Lerp(0f, auraMaxAlpha,
                    (Mathf.Sin(Time.time * auraSpeed + timeOffset) + 1f) / 2f);
                Color auraColor = auraRenderer.material.color;
                auraColor.a = alpha;
                auraRenderer.material.color = auraColor;
            }

            yield return null;
        }
    }

    void UpdateMarkerText(GameObject marker, Infrastructure infra)
    {
        TextMeshPro textMeshPro = marker.GetComponentInChildren<TextMeshPro>();
        if (textMeshPro != null)
        {
            textMeshPro.text = infra.name;
            textMeshPro.fontSize = 8;

            if (gameObject != null && gameObject.activeInHierarchy && !isExitingAR)
            {
                StartCoroutine(UpdateTextRotation(textMeshPro.transform));
            }
        }

        Text nameText = marker.GetComponentInChildren<Text>();
        if (nameText != null && textMeshPro == null)
        {
            nameText.text = infra.name;
            nameText.fontSize = 12;
        }
    }

    void UpdateMarkerText(GameObject marker, string nodeName)
    {
        TextMeshPro textMeshPro = marker.GetComponentInChildren<TextMeshPro>();
        if (textMeshPro != null)
        {
            textMeshPro.text = nodeName;
            textMeshPro.fontSize = 8;

            if (gameObject != null && gameObject.activeInHierarchy && !isExitingAR)
            {
                StartCoroutine(UpdateTextRotation(textMeshPro.transform));
            }
        }

        Text nameText = marker.GetComponentInChildren<Text>();
        if (nameText != null && textMeshPro == null)
        {
            nameText.text = nodeName;
            nameText.fontSize = 12;
        }
    }

    IEnumerator UpdateTextRotation(Transform textTransform)
    {
        while (textTransform != null && !isExitingAR && gameObject != null && gameObject.activeInHierarchy)
        {
            if (arCamera != null)
            {
                textTransform.LookAt(arCamera.transform);
                textTransform.Rotate(0, 180, 0);
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    public void ReloadPathNodes()
    {
        CancelInvoke(nameof(UpdateMarkerSystem));

        ClearAllMarkers();
        spawnedMarkers.Clear();
        markersInitialized = false;

        LoadJourneyNodeIds();

        StartCoroutine(ReinitializeAfterReload());
    }

    private IEnumerator ReinitializeAfterReload()
    {
        yield return new WaitForSeconds(0.5f);

        if (isARNavigationMode && allNodes.Count > 0)
        {
            yield return StartCoroutine(InitializeMarkerSystem());
        }
    }

    private float CalculateDistance(Node node, bool isIndoor)
    {
        if (isIndoor)
        {
            Vector2 nodeXY;
            if (node.indoor != null)
            {
                nodeXY = new Vector2(node.indoor.x, node.indoor.y);
            }
            else
            {
                nodeXY = new Vector2(node.x_coordinate, node.y_coordinate);
            }
            return CalculateDistanceXY(userLocation, nodeXY);
        }
        else
        {
            Vector2 nodeGPS = new Vector2(node.latitude, node.longitude);
            return CalculateDistanceGPS(userLocation, nodeGPS);
        }
    }

    private Vector3 GetNodeWorldPosition(Node node, bool isIndoor)
    {
        if (isIndoor)
        {
            float nodeX = node.indoor != null ? node.indoor.x : node.x_coordinate;
            float nodeY = node.indoor != null ? node.indoor.y : node.y_coordinate;

            int floor = 1;
            if (node.indoor != null && !string.IsNullOrEmpty(node.indoor.floor))
            {
                if (int.TryParse(node.indoor.floor, out int parsedFloor))
                {
                    floor = parsedFloor;
                }
            }

            return XYToWorldPositionWithFloor(nodeX, nodeY, floor);
        }
        else
        {
            return GPSToWorldPosition(node.latitude, node.longitude);
        }
    }

    private Vector3 GetGroundPosition(Vector3 targetWorldPos)
    {
        bool isIndoor = (unifiedARManager != null && unifiedARManager.IsIndoorMode());

        if (!isIndoor || arRaycastManager == null || arCamera == null)
        {
            targetWorldPos.y = groundPlaneY + markerHeightOffset;
            return targetWorldPos;
        }

        Vector3 screenPoint = arCamera.WorldToScreenPoint(targetWorldPos);

        if (screenPoint.z < 0)
        {
            targetWorldPos.y = groundPlaneY + markerHeightOffset;
            return targetWorldPos;
        }

        arRaycastHits.Clear();
        if (arRaycastManager.Raycast(screenPoint, arRaycastHits, TrackableType.PlaneWithinPolygon))
        {
            foreach (var hit in arRaycastHits)
            {
                ARPlane plane = arPlaneManager.GetPlane(hit.trackableId);
                if (plane != null && plane.alignment == PlaneAlignment.HorizontalUp)
                {
                    Vector3 groundPos = hit.pose.position;
                    groundPos.y += markerHeightOffset;
                    return groundPos;
                }
            }
        }

        targetWorldPos.y = groundPlaneY + markerHeightOffset;
        return targetWorldPos;
    }

    private void ClearAllMarkers()
    {
        foreach (var marker in spawnedMarkers.Values)
        {
            if (marker != null)
                Destroy(marker);
        }
        spawnedMarkers.Clear();
    }

    private Vector3 GPSToWorldPosition(float latitude, float longitude)
    {
        float deltaLat = latitude - referenceGPS.x;
        float deltaLng = longitude - referenceGPS.y;

        float meterPerDegree = 111000f;
        float x = deltaLng * meterPerDegree * Mathf.Cos(referenceGPS.x * Mathf.Deg2Rad);
        float z = deltaLat * meterPerDegree;

        Vector3 worldPos = referenceARWorldPosition;
        worldPos.x += x;
        worldPos.z += z;

        return worldPos;
    }

    private float CalculateDistanceGPS(Vector2 coord1, Vector2 coord2)
    {
        float lat1Rad = coord1.x * Mathf.Deg2Rad;
        float lat2Rad = coord2.x * Mathf.Deg2Rad;
        float deltaLatRad = (coord2.x - coord1.x) * Mathf.Deg2Rad;
        float deltaLngRad = (coord2.y - coord1.y) * Mathf.Deg2Rad;

        float a = Mathf.Sin(deltaLatRad / 2) * Mathf.Sin(deltaLatRad / 2) +
                Mathf.Cos(lat1Rad) * Mathf.Cos(lat2Rad) *
                Mathf.Sin(deltaLngRad / 2) * Mathf.Sin(deltaLngRad / 2);

        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return 6371000 * c;
    }

    private Vector3 XYToWorldPosition(float nodeX, float nodeY)
    {
        float deltaX = nodeX - userXY.x;
        float deltaY = nodeY - userXY.y;

        Vector3 worldPos = arCamera.transform.position;
        worldPos.x += deltaX;
        worldPos.z += deltaY;

        return worldPos;
    }

    private Vector3 XYToWorldPositionWithFloor(float nodeX, float nodeY, int floor)
    {
        float deltaX = nodeX - userXY.x;
        float deltaY = nodeY - userXY.y;

        Vector3 worldPos = arCamera.transform.position;
        worldPos.x += deltaX;
        worldPos.z += deltaY;

        if (floor > 1)
        {
            worldPos.y += (floor - 1) * floorHeightMeters;
        }

        return worldPos;
    }

    private float CalculateDistanceXY(Vector2 point1, Vector2 point2)
    {
        return Vector2.Distance(point1, point2);
    }

    public void UpdateUserXY(Vector2 newUserXY)
    {
        userXY = newUserXY;
    }

    public Vector2 GetUserLocation()
    {
        return userLocation;
    }

    void OnDestroy()
    {
        isExitingAR = true;
        CancelInvoke();
        ClearAllMarkers();
        StopAllCoroutines();

        if (compassArrow != null)
        {
            compassArrow.SetActive(false);
        }
    }
}