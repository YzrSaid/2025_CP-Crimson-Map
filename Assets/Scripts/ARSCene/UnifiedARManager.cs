using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class UnifiedARManager : MonoBehaviour
{
    [SerializeField] private string mainSceneName = "MainAppScene";
    private bool isExitingAR = false;

    [Header("GPS Debug Display")]
    public TextMeshProUGUI gpsDebugText;

    [Header("AR Components")]
    public XROrigin xrOrigin;
    public ARRaycastManager arRaycastManager;
    public ARPlaneManager arPlaneManager;
    public ARCameraManager arCameraManager;
    public Camera arCamera;

    [Header("GPS Strength Indicators")]
    public GameObject gpsStrongImage;
    public GameObject gpsWeakImage;
    public GameObject gpsNoneImage;

    [Header("GPS Recalibration Panel")]
    public GameObject recalibrationPanel;
    public GameObject recalibrationBGPanel;
    public float recalibrationAnimDuration = 0.3f;
    public Ease recalibrationEaseType = Ease.OutBack;
    private Vector3 recalibrationPanelOriginalScale;
    private bool recalibrationPanelShown = false;

    [Header("GPS Strength Thresholds")]
    public float strongGPSAccuracyThreshold = 5f;
    public float weakGPSAccuracyThreshold = 10f;
    public float gpsCheckInterval = 2f;
    private float lastGPSCheckTime = 0f;

    [Header("Debug GPS Strength Testing (Editor Only)")]
    public bool useDebugGPSStrength = false;
    public GPSStrength debugGPSStrength = GPSStrength.Strong;

    [Header("Top Panel UI - Main Display")]
    public TextMeshProUGUI fromLocationText;
    public TextMeshProUGUI toDestinationText;
    public TextMeshProUGUI currentLocationText;

    [Header("Debug Panel UI - Toggleable")]
    public GameObject debugPanel;
    public Button debugToggleButton;
    public TextMeshProUGUI trackingStatusText;
    public TextMeshProUGUI debugInfoText;
    public TextMeshProUGUI loadingText;

    [Header("GPS Settings")]
    public int gpsHistorySize = 5;
    public float positionUpdateThreshold = 1f;
    public float positionSmoothingFactor = 0.3f;
    public float nearestNodeSearchRadius = 500f;
    private Queue<Vector2> gpsLocationHistory = new Queue<Vector2>();
    private Vector2 lastStableGPSLocation;
    private bool gpsInitialized = false;

    [Header("GPS Lock Timer")]
    public float gpsLockDuration = 7f;
    private float gpsLockTimer = 0f;
    private bool isGPSLocked = false;

    [Header("Fixed AR Origin")]
    private Vector2 referenceGPS;
    private Vector3 referenceARWorldPosition;
    private float referenceCompassHeading;
    private bool arOriginInitialized = false;

    [Header("Data")]
    private List<Node> currentNodes = new List<Node>();
    private List<Infrastructure> currentInfrastructures = new List<Infrastructure>();

    [Header("Position Tracking")]
    private Vector2 userLocation;
    private Node currentNearestNode;
    private float currentGPSAccuracy = -1f;

    private bool isTrackingStarted = false;
    private bool isDebugPanelVisible = false;
    private float groundPlaneY = 0f;

    private string navigationFromNodeId = "";
    private string navigationToNodeId = "";

    private List<ARRaycastHit> arRaycastHits = new List<ARRaycastHit>();

    void Start()
    {
        InitializeComponents();
        SetupDebugToggle();
        LoadNavigationData();
        InitializeRecalibrationPanel();

        UpdateLoadingUI("Initializing AR...");
        StartCoroutine(InitializeARScene());
    }

    private void InitializeComponents()
    {
        if (arCamera == null) arCamera = Camera.main;
        if (xrOrigin == null) xrOrigin = FindObjectOfType<XROrigin>();
        if (arCameraManager == null) arCameraManager = FindObjectOfType<ARCameraManager>();
        if (arRaycastManager == null) arRaycastManager = FindObjectOfType<ARRaycastManager>();
        if (arPlaneManager == null) arPlaneManager = FindObjectOfType<ARPlaneManager>();
    }

    private void InitializeRecalibrationPanel()
    {
        if (recalibrationPanel != null)
        {
            recalibrationPanelOriginalScale = recalibrationPanel.transform.localScale;
            recalibrationPanel.SetActive(false);
        }

        if (recalibrationBGPanel != null)
        {
            recalibrationBGPanel.SetActive(false);
        }

        HideAllGPSIndicators();
    }

    private void SetupDebugToggle()
    {
        if (debugPanel != null)
        {
            debugPanel.SetActive(false);
            isDebugPanelVisible = false;
        }

        if (debugToggleButton != null)
        {
            debugToggleButton.onClick.AddListener(ToggleDebugPanel);
        }

        UpdateTopPanelVisibility();
    }

    private void ToggleDebugPanel()
    {
        isDebugPanelVisible = !isDebugPanelVisible;
        if (debugPanel != null)
        {
            debugPanel.SetActive(isDebugPanelVisible);
        }
    }

    public void ReloadNavigationData()
    {
        navigationFromNodeId = PlayerPrefs.GetString("ARNavigation_OriginalFromNodeId", "");
        navigationToNodeId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");

        UpdateNavigationTexts();
    }

    private void UpdateNavigationTexts()
    {
        if (fromLocationText != null)
        {
            fromLocationText.gameObject.SetActive(true);

            if (!string.IsNullOrEmpty(navigationFromNodeId) && currentNodes != null)
            {
                Node fromNode = currentNodes.FirstOrDefault(n => n.node_id == navigationFromNodeId);
                if (fromNode != null)
                {
                    if (fromNode.type == "indoorinfra" && !string.IsNullOrEmpty(fromNode.related_infra_id))
                    {
                        string buildingName = GetBuildingNameFromInfraId(fromNode.related_infra_id);
                        fromLocationText.text = $"FROM: {buildingName} ({fromNode.name})";
                    }
                    else
                    {
                        fromLocationText.text = $"FROM: {fromNode.name}";
                    }
                }
                else
                {
                    fromLocationText.text = "FROM: Unknown";
                }
            }
            else
            {
                fromLocationText.text = "FROM: Not Set";
            }
        }

        if (toDestinationText != null)
        {
            toDestinationText.gameObject.SetActive(true);

            if (!string.IsNullOrEmpty(navigationToNodeId) && currentNodes != null)
            {
                Node toNode = currentNodes.FirstOrDefault(n => n.node_id == navigationToNodeId);
                if (toNode != null)
                {
                    if (toNode.type == "indoorinfra" && !string.IsNullOrEmpty(toNode.related_infra_id))
                    {
                        string buildingName = GetBuildingNameFromInfraId(toNode.related_infra_id);
                        toDestinationText.text = $"TO: {buildingName} ({toNode.name})";
                    }
                    else
                    {
                        toDestinationText.text = $"TO: {toNode.name}";
                    }
                }
                else
                {
                    toDestinationText.text = "TO: Unknown";
                }
            }
            else
            {
                toDestinationText.text = "TO: Not Set";
            }
        }
    }

    private string GetBuildingNameFromInfraId(string infraId)
    {
        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == infraId);
        if (infra != null)
        {
            return infra.name;
        }
        Node infrastructureNode = currentNodes.FirstOrDefault(n =>
            n.type == "infrastructure" && n.related_infra_id == infraId);

        if (infrastructureNode != null)
        {
            return infrastructureNode.name;
        }

        return "Building";
    }

    private void LoadNavigationData()
    {
        navigationFromNodeId = PlayerPrefs.GetString("ARNavigation_OriginalFromNodeId", "");
        navigationToNodeId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");
    }

    public void ExitARScene()
    {
        if (isExitingAR) return;
        isExitingAR = true;

        if (GPSManager.Instance != null)
        {
            GPSManager.Instance.UnlockLocationForPathfinding(); 
            GPSManager.Instance.ClearQRLocationOverride(); 
        }

        CancelInvoke();

        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(mainSceneName));
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(mainSceneName);
        }
    }

    IEnumerator InitializeARScene()
    {
        UpdateLoadingUI("Waiting for AR Session...");
        yield return new WaitForSeconds(0.5f);

        UpdateLoadingUI("Waiting for GPS Manager...");
        while (GPSManager.Instance == null)
        {
            yield return new WaitForSeconds(0.1f);
        }
        UpdateLoadingUI("GPS Manager found, starting location services...");
        yield return new WaitForSeconds(0.5f);

        UpdateLoadingUI("Loading map data...");
        string currentMapId = GetCurrentMapId();
        yield return StartCoroutine(LoadCurrentMapData(currentMapId));

        UpdateLoadingUI("Establishing AR world origin...");
        yield return StartCoroutine(InitializeFixedAROrigin());

        UpdateLoadingUI("Starting tracking...");
        StartGPSTracking();
        HideLoadingUI();
    }

    private IEnumerator InitializeFixedAROrigin()
    {
        if (!string.IsNullOrEmpty(navigationFromNodeId))
        {
            Node fromNode = currentNodes.FirstOrDefault(n => n.node_id == navigationFromNodeId);
            if (fromNode != null && fromNode.type != "indoorinfra")
            {
                referenceGPS = new Vector2(fromNode.latitude, fromNode.longitude);
                if (GPSManager.Instance != null)
                {
                    GPSManager.Instance.LockLocationForPathfinding(fromNode.latitude, fromNode.longitude);
                }
            }
            else
            {
                referenceGPS = GPSManager.Instance.GetSmoothedCoordinates();
            }
        }
        else
        {
            referenceGPS = GPSManager.Instance.GetSmoothedCoordinates();
        }

        referenceARWorldPosition = arCamera.transform.position;
        if (GPSManager.Instance != null && GPSManager.Instance.IsARCompassInitialized())
        {
            referenceCompassHeading = GPSManager.Instance.GetARSceneCompassHeading();
        }
        userLocation = referenceGPS;
        lastStableGPSLocation = referenceGPS;
        gpsLocationHistory.Enqueue(referenceGPS);
        gpsInitialized = true;

        isGPSLocked = true;
        gpsLockTimer = gpsLockDuration;

        arOriginInitialized = true;
        yield break;
    }

    private void StartGPSTracking()
    {
        if (isTrackingStarted)
        {
            return;
        }

        isTrackingStarted = true;
        InvokeRepeating(nameof(UpdateGPSTracking), 0.5f, 0.2f);
        InvokeRepeating(nameof(UpdateNearestNode), 0.5f, 2f);
    }

    public void OnQRCodeScanned(Node scannedNode)
    {
        bool isIndoorNode = scannedNode.type == "indoorinfra";

        if (isIndoorNode)
        {
            return;
        }

        referenceGPS = new Vector2(scannedNode.latitude, scannedNode.longitude);
        referenceARWorldPosition = arCamera.transform.position;
        referenceCompassHeading = GPSManager.Instance.GetHeading();
        arOriginInitialized = true;

        userLocation = referenceGPS;
        lastStableGPSLocation = referenceGPS;
        gpsLocationHistory.Clear();
        gpsLocationHistory.Enqueue(referenceGPS);
        gpsInitialized = true;

        isGPSLocked = true;
        gpsLockTimer = gpsLockDuration;

        HideRecalibrationPanel();

        UpdateTopPanelUI();
        UpdateTrackingStatusUI();
        UpdateDebugInfo();
    }

    public string GetInfrastructureName(string infraId)
    {
        Infrastructure infra = currentInfrastructures.FirstOrDefault(i => i.infra_id == infraId);
        return infra != null ? infra.name : infraId;
    }

    private string GetCurrentMapId()
    {
        if (PlayerPrefs.HasKey("ARScene_MapId"))
        {
            return PlayerPrefs.GetString("ARScene_MapId");
        }

        return null;
    }

    IEnumerator LoadCurrentMapData(string currentMapId)
    {
        bool nodesLoaded = false;
        bool infraLoaded = false;

        UpdateLoadingUI($"Loading nodes for map {currentMapId}...");

        yield return StartCoroutine(LoadNodesData(currentMapId, (success) =>
        {
            nodesLoaded = success;
        }));

        UpdateLoadingUI("Loading infrastructure data...");

        yield return StartCoroutine(LoadInfrastructureData((success) =>
        {
            infraLoaded = success;
        }));
    }

    IEnumerator LoadNodesData(string mapId, System.Action<bool> onComplete)
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadSuccess = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonData) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonData);

                    currentNodes = nodes.Where(n =>
                        (n.type == "infrastructure" || n.type == "intermediate") && n.is_active
                    ).ToList();

                    loadSuccess = true;
                }
                catch (System.Exception)
                {
                    loadSuccess = false;
                }
            },
            (error) =>
            {
                loadSuccess = false;
            }
        ));

        onComplete?.Invoke(loadSuccess);
    }

    IEnumerator LoadInfrastructureData(System.Action<bool> onComplete)
    {
        string fileName = "infrastructure.json";
        bool loadSuccess = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonData) =>
            {
                try
                {
                    Infrastructure[] infrastructures = JsonHelper.FromJson<Infrastructure>(jsonData);
                    currentInfrastructures = infrastructures.Where(i => !i.is_deleted).ToList();
                    loadSuccess = true;
                }
                catch (System.Exception)
                {
                    loadSuccess = false;
                }
            },
            (error) =>
            {
                loadSuccess = false;
            }
        ));

        onComplete?.Invoke(loadSuccess);
    }

    void Update()
    {
        if (!arOriginInitialized || !gpsInitialized)
        {
            return;
        }

        if (Time.time - lastGPSCheckTime >= gpsCheckInterval)
        {
            CheckGPSStrength();
            lastGPSCheckTime = Time.time;
        }
    }

    void UpdateGPSTracking()
    {
        if (isExitingAR) return;

        if (!arOriginInitialized)
        {
            return;
        }

        if (isGPSLocked)
        {
            gpsLockTimer -= Time.deltaTime;
            if (gpsLockTimer <= 0)
            {
                isGPSLocked = false;
                if (GPSManager.Instance != null)
                {
                    GPSManager.Instance.UnlockLocationForPathfinding();
                    Debug.Log("[UnifiedARManager] GPS lock timer expired - GPS unlocked in GPSManager");
                }
            }
        }

        Vector2 rawGpsLocation = GPSManager.Instance.GetSmoothedCoordinates();

        if (rawGpsLocation.magnitude < 0.0001f)
        {
            return;
        }

        if (!isGPSLocked)
        {
            userLocation = StabilizeGPSLocation(rawGpsLocation);
        }

        UpdateTrackingStatusUI();
        UpdateDebugInfo();
        UpdateTopPanelUI();
    }

    private void CheckGPSStrength()
    {
        Vector2 gpsCoords = GPSManager.Instance.GetSmoothedCoordinates();

        if (gpsCoords.magnitude < 0.0001f)
        {
            currentGPSAccuracy = -1f;
            UpdateGPSStrengthIndicator(GPSStrength.None);
            UpdateGPSDebugText(GPSStrength.None, "No GPS Signal");
            return;
        }

#if UNITY_EDITOR
        if (useDebugGPSStrength)
        {
            switch (debugGPSStrength)
            {
                case GPSStrength.Strong:
                    currentGPSAccuracy = 10f;
                    break;
                case GPSStrength.Weak:
                    currentGPSAccuracy = 35f;
                    break;
                case GPSStrength.None:
                    currentGPSAccuracy = 100f;
                    break;
            }
            UpdateGPSStrengthIndicator(debugGPSStrength);
            UpdateGPSDebugText(debugGPSStrength, "[DEBUG MODE]");
        }
        else
        {
            currentGPSAccuracy = GPSManager.Instance.GetGPSAccuracy();
            if (currentGPSAccuracy < 0)
            {
                UpdateGPSStrengthIndicator(GPSStrength.None);
                UpdateGPSDebugText(GPSStrength.None, "GPS Error");
            }
            else if (currentGPSAccuracy <= strongGPSAccuracyThreshold)
            {
                UpdateGPSStrengthIndicator(GPSStrength.Strong);
                UpdateGPSDebugText(GPSStrength.Strong, "Editor GPS");
            }
            else if (currentGPSAccuracy <= weakGPSAccuracyThreshold)
            {
                UpdateGPSStrengthIndicator(GPSStrength.Weak);
                UpdateGPSDebugText(GPSStrength.Weak, "Editor GPS");
            }
            else
            {
                UpdateGPSStrengthIndicator(GPSStrength.None);
                UpdateGPSDebugText(GPSStrength.None, "Editor GPS");
            }
        }
#else
    // THIS IS THE CODE THAT RUNS ON YOUR PHONE
    if (Input.location.status == LocationServiceStatus.Running)
    {
        currentGPSAccuracy = Input.location.lastData.horizontalAccuracy;

        if (currentGPSAccuracy <= strongGPSAccuracyThreshold)
        {
            UpdateGPSStrengthIndicator(GPSStrength.Strong);
            UpdateGPSDebugText(GPSStrength.Strong, "Device GPS");
        }
        else if (currentGPSAccuracy <= weakGPSAccuracyThreshold)
        {
            UpdateGPSStrengthIndicator(GPSStrength.Weak);
            UpdateGPSDebugText(GPSStrength.Weak, "Device GPS");
        }
        else
        {
            UpdateGPSStrengthIndicator(GPSStrength.None);
            UpdateGPSDebugText(GPSStrength.None, "Device GPS");
        }
    }
    else
    {
        currentGPSAccuracy = -1f;
        UpdateGPSStrengthIndicator(GPSStrength.None);
        UpdateGPSDebugText(GPSStrength.None, $"GPS Status: {Input.location.status}");
    }
#endif
    }
    private void UpdateGPSDebugText(GPSStrength strength, string source)
    {
        if (gpsDebugText == null) return;

        string strengthColor = "";
        string strengthText = "";

        switch (strength)
        {
            case GPSStrength.Strong:
                strengthColor = "<color=green>STRONG ✅</color>";
                strengthText = "Should be GREEN";
                break;
            case GPSStrength.Weak:
                strengthColor = "<color=yellow>WEAK ⚠️</color>";
                strengthText = "Should be YELLOW";
                break;
            case GPSStrength.None:
                strengthColor = "<color=red>NONE/POOR ❌</color>";
                strengthText = "Should be RED";
                break;
        }

        gpsDebugText.text = $"<b>GPS DEBUG INFO</b>\n" +
                            $"Accuracy: <b>{currentGPSAccuracy:F2}m</b>\n" +
                            $"Strength: {strengthColor}\n" +
                            $"Expected: {strengthText}\n" +
                            $"Strong Threshold: ≤{strongGPSAccuracyThreshold}m\n" +
                            $"Weak Threshold: ≤{weakGPSAccuracyThreshold}m\n" +
                            $"Source: {source}\n" +
                            $"Status: {Input.location.status}";
    }

    private void UpdateGPSStrengthIndicator(GPSStrength strength)
    {
        HideAllGPSIndicators();

        switch (strength)
        {
            case GPSStrength.Strong:
                if (gpsStrongImage != null)
                    gpsStrongImage.SetActive(true);
                break;

            case GPSStrength.Weak:
                if (gpsWeakImage != null)
                    gpsWeakImage.SetActive(true);
                break;

            case GPSStrength.None:
                if (gpsNoneImage != null)
                    gpsNoneImage.SetActive(true);
                ShowRecalibrationPanel();
                break;
        }
    }

    private void HideAllGPSIndicators()
    {
        if (gpsStrongImage != null)
            gpsStrongImage.SetActive(false);

        if (gpsWeakImage != null)
            gpsWeakImage.SetActive(false);

        if (gpsNoneImage != null)
            gpsNoneImage.SetActive(false);
    }

    private void ShowRecalibrationPanel()
    {
        if (!gpsInitialized || recalibrationPanel == null || recalibrationPanelShown)
            return;

        recalibrationPanelShown = true;

        if (recalibrationBGPanel != null)
        {
            recalibrationBGPanel.SetActive(true);
        }

        recalibrationPanel.SetActive(true);
        recalibrationPanel.transform.localScale = Vector3.zero;

        recalibrationPanel.transform.DOScale(recalibrationPanelOriginalScale, recalibrationAnimDuration)
            .SetEase(recalibrationEaseType)
            .SetUpdate(true);
    }

    private void HideRecalibrationPanel()
    {
        if (recalibrationPanel == null)
            return;

        recalibrationPanelShown = false;

        recalibrationPanel.transform.DOScale(Vector3.zero, recalibrationAnimDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                recalibrationPanel.SetActive(false);

                if (recalibrationBGPanel != null)
                {
                    recalibrationBGPanel.SetActive(false);
                }
            });
    }

    private Vector2 StabilizeGPSLocation(Vector2 rawLocation)
    {
        if (!gpsInitialized && rawLocation.magnitude > 0.0001f)
        {
            lastStableGPSLocation = rawLocation;
            gpsLocationHistory.Enqueue(rawLocation);
            gpsInitialized = true;
            return lastStableGPSLocation;
        }

        gpsLocationHistory.Enqueue(rawLocation);
        if (gpsLocationHistory.Count > gpsHistorySize)
        {
            gpsLocationHistory.Dequeue();
        }

        Vector2 averagedLocation = Vector2.zero;
        foreach (Vector2 loc in gpsLocationHistory)
        {
            averagedLocation += loc;
        }
        averagedLocation /= gpsLocationHistory.Count;

        float distanceFromLast = Vector2.Distance(averagedLocation, lastStableGPSLocation);

        if (distanceFromLast >= positionUpdateThreshold)
        {
            lastStableGPSLocation = Vector2.Lerp(lastStableGPSLocation, averagedLocation, positionSmoothingFactor);
        }

        return lastStableGPSLocation;
    }

    private void UpdateNearestNode()
    {
        if (currentNodes == null || currentNodes.Count == 0)
        {
            currentNearestNode = null;
            return;
        }

        Node nearestNode = null;
        float nearestDistance = float.MaxValue;

        foreach (Node node in currentNodes)
        {
            if (node.type == "indoorinfra") continue;

            float distance = CalculateDistanceGPS(userLocation, new Vector2(node.latitude, node.longitude));

            if (distance < nearestDistance && distance <= nearestNodeSearchRadius)
            {
                nearestDistance = distance;
                nearestNode = node;
            }
        }

        currentNearestNode = nearestNode;
        UpdateTopPanelUI();
    }

    float CalculateDistanceGPS(Vector2 coord1, Vector2 coord2)
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

    private void UpdateTopPanelUI()
    {
        UpdateCurrentLocationText();
        UpdateNavigationTexts();
    }

    private void UpdateTopPanelVisibility()
    {
        if (fromLocationText != null)
        {
            fromLocationText.gameObject.SetActive(true);
        }

        if (toDestinationText != null)
        {
            toDestinationText.gameObject.SetActive(true);
        }

        if (currentLocationText != null)
        {
            currentLocationText.gameObject.SetActive(true);
        }
    }

    private void UpdateCurrentLocationText()
    {
        if (currentLocationText == null) return;

        currentLocationText.gameObject.SetActive(true);

        Vector2 coords = userLocation;
        string locationDisplay = $"({coords.x:F5}, {coords.y:F5})";

        if (currentNearestNode != null)
        {
            locationDisplay += $" | {currentNearestNode.name}";
        }

        currentLocationText.text = locationDisplay;
    }

    void UpdateTrackingStatusUI()
    {
        if (trackingStatusText != null)
        {
            Vector2 coords = GPSManager.Instance.GetCoordinates();
            string lockStatus = isGPSLocked ? $" Locked ({gpsLockTimer:F0}s)" : "";

            if (coords.magnitude > 0)
            {
                trackingStatusText.text = $"Outdoor (GPS){lockStatus}\n{coords.x:F5}, {coords.y:F5}\nAccuracy: {currentGPSAccuracy:F1}m";
                trackingStatusText.color = isGPSLocked ? Color.yellow : Color.green;
            }
            else
            {
                trackingStatusText.text = "Outdoor\nGPS: No Signal";
                trackingStatusText.color = Color.red;
            }
        }
    }

    void UpdateDebugInfo()
    {
        if (debugInfoText != null)
        {
            string lockStatus = isGPSLocked ? $" (Locked: {gpsLockTimer:F1}s)" : "";
            string debugMode = "";

#if UNITY_EDITOR
            if (useDebugGPSStrength)
            {
                debugMode = $"\n[DEBUG GPS: {debugGPSStrength}]";
            }
#endif

            debugInfoText.text = $"Navigation + Outdoor (GPS){lockStatus}{debugMode}\n" +
                                 $"User: {userLocation.x:F5}, {userLocation.y:F5}\n" +
                                 $"Reference: {referenceGPS.x:F5}, {referenceGPS.y:F5}\n" +
                                 $"GPS Accuracy: {currentGPSAccuracy:F1}m";
        }
    }

    void UpdateLoadingUI(string message)
    {
        if (loadingText != null)
        {
            loadingText.text = message;
            loadingText.gameObject.SetActive(true);
        }
    }

    void HideLoadingUI()
    {
        if (loadingText != null)
        {
            loadingText.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        isExitingAR = true;
        CancelInvoke();
        StopAllCoroutines();

        if (debugToggleButton != null)
        {
            debugToggleButton.onClick.RemoveListener(ToggleDebugPanel);
        }
    }

    public Vector2 GetUserXY()
    {
        return userLocation;
    }

    public bool IsIndoorMode()
    {
        return false;
    }
    public Vector2 GetUserRawGPS()
    {
        if (GPSManager.Instance != null && GPSManager.Instance.IsUsingQROverride())
        {
            return GPSManager.Instance.GetCoordinates();
        }
        if (GPSManager.Instance != null)
        {
            return GPSManager.Instance.GetRawSmoothedGPSCoordinates();
        }

        return Vector2.zero;
    }


    public string GetCurrentIndoorInfraId()
    {
        return "";
    }

    public Vector2 GetReferenceGPS()
    {
        return referenceGPS;
    }

    public Vector3 GetReferenceARWorldPosition()
    {
        return referenceARWorldPosition;
    }

    public float GetReferenceCompassHeading()
    {
        return referenceCompassHeading;
    }

    public enum GPSStrength
    {
        Strong,
        Weak,
        None
    }
}