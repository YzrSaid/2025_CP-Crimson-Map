using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using DG.Tweening;

public class PathfindingController : MonoBehaviour
{
    [Header("References")]
    public AStarPathfinding pathfinding;
    public InfrastructurePopulator infrastructurePopulator;
    public GPSManager gpsManager;

    [Header("UI Elements")]
    public TMP_Dropdown toDropdown;
    public Button findPathButton;
    public GameObject BGPanel;

    [Header("Location Lock Display")]
    public GameObject locationLockDisplay;
    public TextMeshProUGUI locationLockText;
    public Button lockButton;
    public Button unlockButton;

    [Header("GPS Unavailable Panel")]
    public GameObject gpsUnavailablePanel;
    public GameObject gpsUnavailableBGPanel;
    public float gpsUnavailablePanelAnimDuration = 0.3f;
    public Ease gpsUnavailablePanelEaseType = Ease.OutBack;
    private Vector3 gpsUnavailablePanelOriginalScale;
    public Button gpsUnavailableOkButton;
    public float gpsUnavailableDelay = 40f;
    private Coroutine gpsUnavailableCoroutine;
    private bool gpsUnavailablePanelShown = false;

    [Header("Confirmation Panel")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmFromText;
    public TextMeshProUGUI confirmToText;
    public TextMeshProUGUI confirmErrorText;
    public float confirmationPanelAnimDuration = 0.3f;
    public Ease confirmationPanelEaseType = Ease.OutBack;
    private Vector3 confirmationPanelOriginalScale;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Confirmation Panel 2")]
    public GameObject confirmationPanel2;
    public TextMeshProUGUI confirmErrorText2;
    public float confirmationPanelAnimDuration2 = 0.3f;
    public Ease confirmationPanelEaseType2 = Ease.OutBack;
    private Vector3 confirmationPanelOriginalScale2;
    public Button cancelButton2;

    [Header("Location Conflict Panel")]
    public GameObject locationConflictPanel;
    public TextMeshProUGUI conflictMessageText;
    public Button conflictConfirmButton;
    public Button conflictCancelButton;

    [Header("Result Display")]
    public GameObject resultPanel;
    public GameObject destinationPanel;
    public float resultPanelAnimDuration = 0.3f;
    public Ease resultPanelEaseType = Ease.OutBack;
    private Vector3 resultPanelOriginalScale;
    public TextMeshProUGUI fromText;
    public TextMeshProUGUI toText;

    [Header("Route List")]
    public Transform routeListContainer;
    public GameObject routeItemPrefab;
    public ScrollRect routeScrollView;
    public Button confirmRouteButton;

    [Header("Indoor Data")]
    private Dictionary<string, IndoorInfrastructure> indoorInfrastructures = new Dictionary<string, IndoorInfrastructure>();
    private Dictionary<string, Node> indoorNodes = new Dictionary<string, Node>();

    [Header("Static Test Settings")]
    public bool useStaticTesting = false;
    public string staticFromNodeId = "ND-015";
    public string staticToNodeId = "ND-033";

    [Header("GPS Settings")]
    public bool useGPSForFromLocation = true;
    public float nearestNodeSearchRadius = 50f;
    public bool autoUpdateGPSLocation = true;
    public float gpsUpdateInterval = 5f;
    public float qrConflictThresholdMeters = 100f;

    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, string> infraIdToNodeId = new Dictionary<string, string>();
    private string selectedFromNodeId;
    private string selectedToNodeId;
    private Node currentNearestNode;
    private Node qrScannedNode;
    private bool isLocationLocked = false;
    private Node lockedNode;
    private bool hasShownConflictPanel = false;
    private float lastGPSUpdateTime;
    private bool nodesLoaded = false;
    private bool lastGPSWasAvailable = true;

    private string currentMapId;
    private List<string> currentCampusIds;

    private List<RouteData> currentRoutes = new List<RouteData>();
    private List<RouteItem> routeItemInstances = new List<RouteItem>();
    private int selectedRouteIndex = -1;

    void Start()
    {
        if (findPathButton != null)
        {
            findPathButton.onClick.AddListener(OnFindPathClicked);
        }

        if (toDropdown != null)
        {
            toDropdown.onValueChanged.AddListener(OnToDropdownChanged);
        }

        if (lockButton != null)
        {
            lockButton.onClick.AddListener(LockCurrentLocation);
        }

        if (unlockButton != null)
        {
            unlockButton.onClick.AddListener(UnlockLocation);
        }

        if (confirmRouteButton != null)
        {
            confirmRouteButton.onClick.AddListener(OnConfirmRouteClicked);
            confirmRouteButton.gameObject.SetActive(false);
        }

        if (conflictConfirmButton != null)
        {
            conflictConfirmButton.onClick.AddListener(OnLocationConflictConfirm);
        }

        if (conflictCancelButton != null)
        {
            conflictCancelButton.onClick.AddListener(OnLocationConflictCancel);
        }

        if (gpsUnavailableOkButton != null)
        {
            gpsUnavailableOkButton.onClick.AddListener(OnGPSUnavailableOkClicked);
        }

        if (destinationPanel != null)
        {
            destinationPanel.SetActive(false);
        }

        if (locationConflictPanel != null)
        {
            locationConflictPanel.SetActive(false);
        }

        if (locationLockDisplay != null)
        {
            locationLockDisplay.SetActive(true);
        }

        isLocationLocked = false;
        UpdateLocationLockUI(false);

        if (locationLockText != null)
        {
            locationLockText.text = "Loading map data...";
        }

        if (MapManager.Instance != null)
        {
            MapManager.Instance.OnMapChanged += OnMapChanged;
        }

        if (gpsManager == null)
        {
            gpsManager = GPSManager.Instance;
        }
        InitializePanels();
    }
    private void InitializePanels()
    {
        // For Confirmation Panel
        if (confirmationPanel != null)
        {
            confirmationPanelOriginalScale = confirmationPanel.transform.localScale;
            confirmationPanel.SetActive(false);
        }
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        // For Confirmation Panel 2
        if (confirmationPanel2 != null)
        {
            confirmationPanelOriginalScale2 = confirmationPanel2.transform.localScale;
            confirmationPanel2.SetActive(false);
        }

        if (cancelButton2 != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        // For Result Panel
        if (resultPanel != null)
        {
            resultPanelOriginalScale = resultPanel.transform.localScale;
            resultPanel.SetActive(false);
        }

        // For GPS Unavailable Panel
        if (gpsUnavailablePanel != null)
        {
            gpsUnavailablePanelOriginalScale = gpsUnavailablePanel.transform.localScale;
            gpsUnavailablePanel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (findPathButton != null)
            findPathButton.onClick.RemoveListener(OnFindPathClicked);
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        if (toDropdown != null)
            toDropdown.onValueChanged.RemoveListener(OnToDropdownChanged);
        if (lockButton != null)
            lockButton.onClick.RemoveListener(LockCurrentLocation);
        if (unlockButton != null)
            unlockButton.onClick.RemoveListener(UnlockLocation);
        if (confirmRouteButton != null)
            confirmRouteButton.onClick.RemoveListener(OnConfirmRouteClicked);
        if (conflictConfirmButton != null)
            conflictConfirmButton.onClick.RemoveListener(OnLocationConflictConfirm);
        if (conflictCancelButton != null)
            conflictCancelButton.onClick.RemoveListener(OnLocationConflictCancel);
        if (gpsUnavailableOkButton != null)
            gpsUnavailableOkButton.onClick.RemoveListener(OnGPSUnavailableOkClicked);
        if (MapManager.Instance != null)
            MapManager.Instance.OnMapChanged -= OnMapChanged;

        ClearQRData();

        if (gpsUnavailableCoroutine != null)
        {
            StopCoroutine(gpsUnavailableCoroutine);
        }
    }

    void Update()
    {
        if (nodesLoaded && currentNearestNode == null && !isLocationLocked && useGPSForFromLocation && !useStaticTesting)
        {
            UpdateFromLocationByGPS();
            lastGPSUpdateTime = Time.time;
        }

        if (useGPSForFromLocation && autoUpdateGPSLocation && !useStaticTesting && nodesLoaded)
        {
            if (!isLocationLocked)
            {
                if (Time.time - lastGPSUpdateTime >= gpsUpdateInterval)
                {
                    UpdateFromLocationByGPS();
                    lastGPSUpdateTime = Time.time;
                }
            }
        }

        CheckGPSAvailability();
    }
    private void CheckGPSAvailability()
    {
        if (MainAppManager.Instance == null || isLocationLocked || useStaticTesting)
            return;

        float gpsAccuracy = MainAppManager.Instance.GetCurrentGPSAccuracy();

        bool isGPSAvailable = gpsAccuracy > 0 && gpsAccuracy < 60f;

        if (lastGPSWasAvailable && !isGPSAvailable)
        {
            OnGPSBecameUnavailable();
        }
        else if (!lastGPSWasAvailable && isGPSAvailable)
        {
            OnGPSBecameAvailable();
        }

        lastGPSWasAvailable = isGPSAvailable;
    }
    private void OnGPSBecameUnavailable()
    {
        Debug.Log("[PathfindingController] GPS became unavailable");

        if (locationLockText != null && !isLocationLocked)
        {
            locationLockText.text = "Loading...";
        }

        currentNearestNode = null;
        selectedFromNodeId = null;

        if (gpsUnavailableCoroutine != null)
        {
            StopCoroutine(gpsUnavailableCoroutine);
        }
        gpsUnavailableCoroutine = StartCoroutine(ShowGPSUnavailablePanelAfterDelay());
    }

    private void OnGPSBecameAvailable()
    {
        Debug.Log("[PathfindingController] GPS became available");

        if (gpsUnavailableCoroutine != null)
        {
            StopCoroutine(gpsUnavailableCoroutine);
            gpsUnavailableCoroutine = null;
        }

        if (gpsUnavailablePanel != null && gpsUnavailablePanel.activeSelf)
        {
            gpsUnavailablePanel.SetActive(false);
        }

        gpsUnavailablePanelShown = false;

        if (nodesLoaded && !isLocationLocked)
        {
            UpdateFromLocationByGPS();
        }
    }

    private IEnumerator ShowGPSUnavailablePanelAfterDelay()
    {
        gpsUnavailablePanelShown = false;
        yield return new WaitForSeconds(gpsUnavailableDelay);

        if (MainAppManager.Instance != null)
        {
            float gpsAccuracy = MainAppManager.Instance.GetCurrentGPSAccuracy();
            bool isGPSStillUnavailable = gpsAccuracy <= 0 || gpsAccuracy >= 60f;

            if (isGPSStillUnavailable && !gpsUnavailablePanelShown && !isLocationLocked)
            {
                ShowGPSUnavailablePanel();
            }
        }
    }

    private void ShowGPSUnavailablePanel()
    {
        if (gpsUnavailablePanel != null && gpsUnavailableBGPanel != null && !gpsUnavailablePanelShown)
        {
            gpsUnavailableBGPanel.SetActive(true);
            gpsUnavailablePanel.SetActive(true);
            gpsUnavailablePanelShown = true;
            gpsUnavailablePanel.transform.localScale = Vector3.zero;

            gpsUnavailablePanel.transform.DOScale(gpsUnavailablePanelOriginalScale, gpsUnavailablePanelAnimDuration)
                .SetEase(gpsUnavailablePanelEaseType)
                .SetUpdate(true);
        }
    }

    private void OnGPSUnavailableOkClicked()
    {
        if (gpsUnavailablePanel != null)
        {
            gpsUnavailablePanelShown = false;
            gpsUnavailablePanel.transform.DOScale(Vector3.zero, gpsUnavailablePanelAnimDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (gpsUnavailableBGPanel != null)
                {
                    gpsUnavailableBGPanel.SetActive(false);
                }
                gpsUnavailablePanel.SetActive(false);
            });
        }
    }


    private void CheckForScannedQRData()
    {
        string scannedNodeId = PlayerPrefs.GetString("ScannedNodeID", "");

        if (!string.IsNullOrEmpty(scannedNodeId) && nodesLoaded)
        {
            LoadQRScannedNode(scannedNodeId);
        }
    }

    private IEnumerator LoadIndoorData()
    {
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor.json",
            (jsonContent) =>
            {
                try
                {
                    IndoorInfrastructure[] indoorArray = JsonHelper.FromJson<IndoorInfrastructure>(jsonContent);

                    indoorInfrastructures.Clear();
                    foreach (var indoor in indoorArray)
                    {
                        if (!indoor.is_deleted)
                        {
                            indoorInfrastructures[indoor.room_id] = indoor;
                        }
                    }

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

        foreach (var node in allNodes.Values)
        {
            if (node.type == "indoorinfra")
            {
                indoorNodes[node.node_id] = node;
            }
        }
    }

    private bool IsIndoorNode(string nodeId)
    {
        if (allNodes.TryGetValue(nodeId, out Node node))
        {
            return node.type == "indoorinfra";
        }
        return indoorNodes.ContainsKey(nodeId);
    }
    private void UpdateLocationDisplayTextIndoor(Node node, string buildingName)
    {
        if (locationLockText != null && node != null)
        {
            locationLockText.text = $"{node.name} ({buildingName})";
        }
    }

    private void LoadQRScannedNode(string nodeId)
    {
        if (allNodes.TryGetValue(nodeId, out Node node))
        {
            qrScannedNode = node;
            lockedNode = node;
            selectedFromNodeId = nodeId;
            isLocationLocked = true;
            hasShownConflictPanel = false;

            if (GPSManager.Instance != null && node.type != "indoorinfra")
            {
                GPSManager.Instance.LockLocationForPathfinding(
                    node.latitude,
                    node.longitude
                );
                Debug.Log($"[PathfindingController] QR location locked at: {node.name} ({node.latitude}, {node.longitude})");
            }

            UpdateLocationLockUI(true);
            UpdateLocationDisplayText(node);
        }
    }


    public void LockCurrentLocation()
    {
        if (currentNearestNode != null)
        {
            lockedNode = currentNearestNode;
            selectedFromNodeId = currentNearestNode.node_id;
            isLocationLocked = true;

            if (GPSManager.Instance != null)
            {
                GPSManager.Instance.LockLocationForPathfinding(
                    currentNearestNode.latitude,
                    currentNearestNode.longitude
                );
            }

            UpdateLocationLockUI(true);
            UpdateLocationDisplayText(lockedNode);

            Debug.Log($"[PathfindingController] Location locked at: {lockedNode.name}");
        }
    }

    public void UnlockLocation()
    {
        isLocationLocked = false;
        lockedNode = null;
        qrScannedNode = null;

        if (GPSManager.Instance != null)
        {
            GPSManager.Instance.UnlockLocationForPathfinding();
            GPSManager.Instance.ClearQRLocationOverride();
        }

        UpdateLocationLockUI(false);
        UpdateFromLocationByGPS();
        ClearQRData();

        Debug.Log("[PathfindingController] Location unlocked");
    }

    private void UpdateLocationLockUI(bool locked)
    {
        if (lockButton != null)
        {
            lockButton.gameObject.SetActive(!locked);
        }

        if (unlockButton != null)
        {
            unlockButton.gameObject.SetActive(locked);
        }
    }

    private void UpdateLocationDisplayText(Node node)
    {
        if (locationLockText != null && node != null)
        {
            locationLockText.text = $"{node.name}";
        }
    }

    public void ClearQRData()
    {
        PlayerPrefs.DeleteKey("ScannedNodeID");
        PlayerPrefs.DeleteKey("ScannedLocationName");
        PlayerPrefs.DeleteKey("ScannedLat");
        PlayerPrefs.DeleteKey("ScannedLng");
        PlayerPrefs.DeleteKey("ScannedCampusID");
        PlayerPrefs.DeleteKey("ScannedX");
        PlayerPrefs.DeleteKey("ScannedY");
        PlayerPrefs.Save();

        qrScannedNode = null;
    }

    public void UnlockFromQR()
    {
        UnlockLocation();
    }

    private void OnToDropdownChanged(int index)
    {
        if (infrastructurePopulator == null || toDropdown == null)
        {
            return;
        }

        var (destinationId, destinationType) = infrastructurePopulator.GetSelectedDestinationFromDropdown(toDropdown);

        if (string.IsNullOrEmpty(destinationId))
        {
            selectedToNodeId = null;
            return;
        }

        SetDestination(destinationId, destinationType);
    }

    private void OnMapChanged(MapInfo mapInfo)
    {
        ClearCurrentPath();

        if (!isLocationLocked && useGPSForFromLocation && !useStaticTesting)
        {
            currentNearestNode = null;
            selectedFromNodeId = null;

            if (locationLockText != null)
            {
                locationLockText.text = "Loading new map...";
            }
        }
    }

    public IEnumerator InitializeForMap(string mapId, List<string> campusIds)
    {
        currentMapId = mapId;
        currentCampusIds = campusIds;

        yield return StartCoroutine(LoadNodesFromJSON(mapId));

        yield return StartCoroutine(LoadIndoorData());

        yield return StartCoroutine(BuildInfrastructureNodeMapping());
        yield return new WaitForSeconds(1f);

        if (nodesLoaded)
        {
            CheckForScannedQRData();

            if (!isLocationLocked && useGPSForFromLocation && !useStaticTesting)
            {
                UpdateFromLocationByGPS();
                lastGPSUpdateTime = Time.time;
            }
        }

        if (pathfinding != null)
        {
            yield return StartCoroutine(pathfinding.LoadGraphDataForMap(mapId, campusIds));
        }
    }

    public void SetDestination(string destinationId, string destinationType)
    {
        if (destinationType == "infrastructure")
        {
            var infraNode = allNodes.Values.FirstOrDefault(n =>
                n.type == "infrastructure" && n.related_infra_id == destinationId);

            if (infraNode != null)
            {
                selectedToNodeId = infraNode.node_id;
            }
        }
        else if (destinationType == "indoorinfra")
        {
            var indoorNode = allNodes.Values.FirstOrDefault(n =>
                n.type == "indoorinfra" && n.related_room_id == destinationId);

            if (indoorNode != null)
            {
                selectedToNodeId = indoorNode.node_id;
            }
        }
    }

    private IEnumerator LoadNodesFromJSON(string mapId)
    {
        string fileName = $"nodes_{mapId}.json";
        string errorMsg = "";

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodesArray = JsonHelper.FromJson<Node>(jsonContent);

                    allNodes.Clear();
                    foreach (Node node in nodesArray)
                    {
                        allNodes[node.node_id] = node;
                    }

                    nodesLoaded = true;
                }
                catch (System.Exception e)
                {
                    errorMsg = $"Error parsing nodes JSON: {e.Message}";
                }
            },
            (error) =>
            {
                errorMsg = $"Failed to load {fileName}: {error}";
            }
        ));

        yield return null;
    }

    private IEnumerator BuildInfrastructureNodeMapping()
    {
        if (allNodes == null || allNodes.Count == 0)
        {
            yield break;
        }

        infraIdToNodeId.Clear();

        foreach (var kvp in allNodes)
        {
            Node node = kvp.Value;

            if (node.type == "infrastructure" && !string.IsNullOrEmpty(node.related_infra_id))
            {
                infraIdToNodeId[node.related_infra_id] = node.node_id;
            }
        }

        yield return null;
    }

    private void UpdateFromLocationByGPS()
    {
        if (gpsManager == null)
        {
            if (locationLockText != null)
            {
                locationLockText.text = "Initializing GPS...";
            }
            return;
        }

        if (!nodesLoaded || allNodes == null || allNodes.Count == 0)
        {
            if (locationLockText != null)
            {
                locationLockText.text = "Loading map data...";
            }
            return;
        }

        if (isLocationLocked)
        {
            return;
        }

        Vector2 coords = gpsManager.GetSmoothedCoordinates();

        Node nearestNode = FindNearestNode(coords.x, coords.y);

        if (nearestNode != null)
        {
            if (nearestNode.type == "barrier")
            {
                if (locationLockText != null)
                {
                    locationLockText.text = "Searching for location...";
                }
                return;
            }
            selectedFromNodeId = nearestNode.node_id;
            currentNearestNode = nearestNode;

            UpdateLocationDisplayText(nearestNode);
        }
        else
        {
            if (locationLockText != null)
            {
                locationLockText.text = "Searching for location...";
            }
        }
    }

    private Node FindNearestNode(float latitude, float longitude)
    {
        if (allNodes == null || allNodes.Count == 0)
        {
            return null;
        }

        Node nearestNode = null;
        float nearestDistance = float.MaxValue;

        foreach (var kvp in allNodes)
        {
            Node node = kvp.Value;

            if (node.type == "barrier")
            {
                continue;
            }

            float distance = CalculateDistance(latitude, longitude, node.latitude, node.longitude);

            if (distance < nearestDistance && distance <= nearestNodeSearchRadius)
            {
                nearestDistance = distance;
                nearestNode = node;
            }
        }

        return nearestNode;
    }

    private float CalculateDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000f;

        float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
        float dLon = (lon2 - lon1) * Mathf.Deg2Rad;

        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(lat1 * Mathf.Deg2Rad) * Mathf.Cos(lat2 * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);

        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));

        return R * c;
    }

    public void NavigateToInfrastructure(string infraId)
    {
        if (string.IsNullOrEmpty(infraId))
        {
            Debug.LogError("Infrastructure ID is null or empty!");
            return;
        }

        SetDestination(infraId, "infrastructure");

        string fromNodeId;
        string toNodeId;

        if (useStaticTesting)
        {
            fromNodeId = staticFromNodeId;
            toNodeId = staticToNodeId;
        }
        else
        {
            if (isLocationLocked && lockedNode != null)
            {
                fromNodeId = lockedNode.node_id;
            }
            else
            {
                UpdateFromLocationByGPS();
                fromNodeId = selectedFromNodeId;
            }
            toNodeId = selectedToNodeId;
        }

        if (string.IsNullOrEmpty(toNodeId))
        {
            ShowConfirmationError("Destination not found in current map");
            return;
        }

        if (string.IsNullOrEmpty(fromNodeId))
        {
            ShowConfirmationError("Cannot determine your location. Please check GPS.");
            return;
        }

        if (fromNodeId == toNodeId)
        {
            ShowConfirmationError("You are already at this location!");
            return;
        }

        // Show confirmation panel
        ShowConfirmationPanel(fromNodeId, toNodeId);
    }

    private void ShowLocationConflictPanel(Node qrNode, Node gpsNode, float distanceMeters)
    {
        if (locationConflictPanel == null)
        {
            return;
        }

        if (conflictMessageText != null)
        {
            conflictMessageText.text =
                $"Your location has changed!\n\n" +
                $"<b>QR Location:</b> {qrNode.name}\n" +
                $"<b>Current GPS:</b> {gpsNode.name}\n" +
                $"<b>Distance:</b> {distanceMeters:F0}m apart\n\n" +
                $"Would you like to update to your current GPS location?";
        }

        locationConflictPanel.SetActive(true);
    }


    private void OnLocationConflictConfirm()
    {
        if (locationConflictPanel != null)
        {
            locationConflictPanel.SetActive(false);
        }
        UnlockLocation();
    }

    private void OnLocationConflictCancel()
    {
        if (locationConflictPanel != null)
        {
            locationConflictPanel.SetActive(false);
        }
    }

    private void OnFindPathClicked()
    {
        string fromNodeId;
        string toNodeId;

        if (useStaticTesting)
        {
            fromNodeId = staticFromNodeId;
            toNodeId = staticToNodeId;
        }
        else
        {
            if (isLocationLocked && lockedNode != null)
            {
                fromNodeId = lockedNode.node_id;
            }
            else
            {
                UpdateFromLocationByGPS();
                fromNodeId = selectedFromNodeId;
            }
            toNodeId = selectedToNodeId;
        }

        if (string.IsNullOrEmpty(toNodeId))
        {
            ShowConfirmationError("Please select a destination");
            return;
        }

        if (string.IsNullOrEmpty(fromNodeId))
        {
            ShowConfirmationError("Cannot determine your location. Please check GPS.");
            return;
        }

        if (fromNodeId == toNodeId)
        {
            ShowConfirmationError("You are already at this location!");
            return;
        }

        ShowConfirmationPanel(fromNodeId, toNodeId);
    }

    private void ShowConfirmationPanel(string fromNodeId, string toNodeId)
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.transform.Find("Title").GetComponent<TextMeshProUGUI>().text = "Confirm Location";
            confirmButton.gameObject.SetActive(true);
            confirmationPanel.SetActive(true);
            confirmationPanel.transform.localScale = Vector3.zero;
            confirmationPanel.transform.DOScale(confirmationPanelOriginalScale, confirmationPanelAnimDuration)
                .SetEase(confirmationPanelEaseType)
                .SetUpdate(true);
            cancelButton.GetComponentInChildren<TextMeshProUGUI>().text = "Cancel";
            confirmErrorText.gameObject.SetActive(false);
            if (BGPanel != null)
            {
                BGPanel.SetActive(true);
            }
        }

        if (allNodes.TryGetValue(fromNodeId, out Node fromNode))
        {
            if (confirmFromText != null)
            {
                confirmFromText.text = $"<b>From:</b> {fromNode.name}";
            }
        }

        if (allNodes.TryGetValue(toNodeId, out Node toNode))
        {
            if (confirmToText != null)
            {
                confirmToText.text = $"<b>To:</b> {toNode.name}";
            }
        }

        if (confirmErrorText != null)
        {
            confirmErrorText.text = "";
        }
    }

    private void ShowConfirmationError(string message)
    {
        if (confirmationPanel2 != null)
        {
            confirmationPanel2.transform.Find("Title").GetComponent<TextMeshProUGUI>().text = "Error";
            cancelButton2.GetComponentInChildren<TextMeshProUGUI>().text = "Close";
            confirmationPanel2.SetActive(true);
            if (BGPanel != null)
            {
                BGPanel.SetActive(true);
            }
            confirmationPanel2.transform.localScale = Vector3.zero;
            confirmationPanel2.transform.DOScale(confirmationPanelOriginalScale2, confirmationPanelAnimDuration2)
                .SetEase(confirmationPanelEaseType2)
                .SetUpdate(true);
            confirmErrorText2.text = message;
            confirmErrorText2.gameObject.SetActive(true);
        }
    }

    private void OnConfirmClicked()
    {
        string fromNodeId;
        string toNodeId;

        if (useStaticTesting)
        {
            fromNodeId = staticFromNodeId;
            toNodeId = staticToNodeId;
        }
        else
        {
            fromNodeId = selectedFromNodeId;
            toNodeId = selectedToNodeId;
        }

        if (confirmationPanel != null)
        {

            confirmationPanel.transform.DOScale(Vector3.zero, confirmationPanelAnimDuration)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    confirmationPanel.SetActive(false);
                });
        }
        StartCoroutine(FindAndDisplayPaths(fromNodeId, toNodeId));
    }

    private void OnCancelClicked()
    {
        if (confirmationPanel != null)
        {

            confirmationPanel.transform.DOScale(Vector3.zero, confirmationPanelAnimDuration)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    confirmationPanel.SetActive(false);
                    if (BGPanel != null)
                    {
                        BGPanel.SetActive(false);
                    }
                });
        }
    }

    private string GetInfraIdFromIndoorNode(Node indoorNode)
    {
        if (indoorNode.type != "indoorinfra" || string.IsNullOrEmpty(indoorNode.related_room_id))
        {
            return null;
        }

        if (indoorInfrastructures.TryGetValue(indoorNode.related_room_id, out IndoorInfrastructure indoor))
        {
            return indoor.infra_id;
        }

        Debug.LogWarning($"[PathfindingController] Room not found in indoor data: {indoorNode.related_room_id}");
        return null;
    }
    private IEnumerator FindAndDisplayPaths(string fromNodeId, string toNodeId)
    {
        if (pathfinding == null)
        {
            yield break;
        }

        if (findPathButton != null)
        {
            findPathButton.interactable = false;
        }

        // ========== VALIDATION: Check if nodes exist ==========
        if (!allNodes.ContainsKey(fromNodeId))
        {
            ShowConfirmationError($"FROM node not found: {fromNodeId}");
            if (findPathButton != null) findPathButton.interactable = true;
            yield break;
        }

        if (!allNodes.ContainsKey(toNodeId))
        {
            ShowConfirmationError($"TO node not found: {toNodeId}");
            if (findPathButton != null) findPathButton.interactable = true;
            yield break;
        }

        Node fromNode = allNodes[fromNodeId];
        Node toNode = allNodes[toNodeId];

        // ========== VALIDATION: Check if nodes are active ==========
        if (!fromNode.is_active)
        {
            ShowConfirmationError($"Your starting location '{fromNode.name}' is currently not available.");
            if (findPathButton != null) findPathButton.interactable = true;
            yield break;
        }

        if (!toNode.is_active)
        {
            ShowConfirmationError($"Your destination '{toNode.name}' is currently not available.");
            if (findPathButton != null) findPathButton.interactable = true;
            yield break;
        }

        // ========== DETERMINE NODE TYPES ==========
        bool fromIsIndoor = IsIndoorNode(fromNodeId);
        bool toIsIndoor = IsIndoorNode(toNodeId);

        Debug.Log($"[PathfindingController] Navigation Setup:");
        Debug.Log($"  FROM: {fromNode.name} (Type: {fromNode.type}, Indoor: {fromIsIndoor})");
        Debug.Log($"  TO: {toNode.name} (Type: {toNode.type}, Indoor: {toIsIndoor})");

        // ========== CASE 1: OUTDOOR TO INDOOR (SAME BUILDING) ==========
        // Check if navigating from a building entrance to a room in the same building
        if (!fromIsIndoor && toIsIndoor)
        {
            // Get the infra_id that the room belongs to
            string toInfraId = GetInfraIdFromIndoorNode(toNode);
            string fromInfraId = null;

            // Check if FROM is a building entrance node
            if (fromNode.type == "infrastructure" && !string.IsNullOrEmpty(fromNode.related_infra_id))
            {
                fromInfraId = fromNode.related_infra_id;
            }

            bool isSameBuilding = !string.IsNullOrEmpty(fromInfraId) &&
                                !string.IsNullOrEmpty(toInfraId) &&
                                fromInfraId == toInfraId;

            Debug.Log($"[PathfindingController] Same Building Check:");
            Debug.Log($"  FROM infra_id: {fromInfraId}");
            Debug.Log($"  TO infra_id: {toInfraId}");
            Debug.Log($"  Same Building: {isSameBuilding}");

            if (isSameBuilding)
            {
                Debug.Log($"[PathfindingController] ✅ CASE 1: Indoor navigation (same building)");

                var singleNodeRoute = CreateSameBuildingRoute(fromNode.node_id, fromNode, toNode);

                PlayerPrefs.SetString("ARNavigation_OriginalFromNodeId", fromNodeId);
                PlayerPrefs.SetString("ARNavigation_OriginalToNodeId", toNodeId);
                PlayerPrefs.SetInt("ARNavigation_FromIsIndoor", 0);
                PlayerPrefs.SetInt("ARNavigation_ToIsIndoor", 1);
                PlayerPrefs.SetString("ARNavigation_SameBuilding", "true");
                PlayerPrefs.Save();

                currentRoutes = new List<RouteData> { singleNodeRoute };

                if (findPathButton != null)
                {
                    findPathButton.interactable = true;
                }

                DisplayAllRoutes();
                yield break;
            }
        }

        // ========== CASE 2: OUTDOOR TO INDOOR (DIFFERENT BUILDING) ==========
        // Navigate to the building entrance if destination is in a different building
        string pathStartNodeId = fromNodeId;
        string pathEndNodeId = toNodeId;

        if (toIsIndoor)
        {
            Debug.Log($"[PathfindingController] ✅ CASE 2: Outdoor to indoor (different building)");

            // Map indoor destination to building entrance
            string toInfraId = GetInfraIdFromIndoorNode(toNode);
            if (!string.IsNullOrEmpty(toInfraId))
            {
                // Find the entrance node for this building
                Node entranceNode = allNodes.Values.FirstOrDefault(n =>
                    n.is_active &&
                    (n.type == "infrastructure") &&
                    n.related_infra_id == toInfraId);

                if (entranceNode != null)
                {
                    pathEndNodeId = entranceNode.node_id;
                    Debug.Log($"[PathfindingController] Mapped indoor destination to entrance: {entranceNode.node_id} ({entranceNode.name})");
                }
                else
                {
                    ShowConfirmationError($"Cannot find an active entrance for '{toNode.name}'");
                    if (findPathButton != null) findPathButton.interactable = true;
                    yield break;
                }
            }
        }

        // ========== CASE 3: OUTDOOR TO OUTDOOR ==========
        Debug.Log($"[PathfindingController] ✅ CASE 3: Standard outdoor pathfinding");
        Debug.Log($"  Pathfinding from: {pathStartNodeId} to {pathEndNodeId}");

        yield return StartCoroutine(pathfinding.FindMultiplePaths(pathStartNodeId, pathEndNodeId, 3));

        if (findPathButton != null)
        {
            findPathButton.interactable = true;
        }

        var routes = pathfinding.GetAllRoutes();

        if (routes == null || routes.Count == 0)
        {
            ShowConfirmationError("No path found between these locations");
            yield break;
        }

        // ========== SAVE NAVIGATION DATA ==========
        PlayerPrefs.SetString("ARNavigation_OriginalFromNodeId", fromNodeId);
        PlayerPrefs.SetString("ARNavigation_OriginalToNodeId", toNodeId);
        PlayerPrefs.SetInt("ARNavigation_FromIsIndoor", fromIsIndoor ? 1 : 0);
        PlayerPrefs.SetInt("ARNavigation_ToIsIndoor", toIsIndoor ? 1 : 0);
        PlayerPrefs.SetString("ARNavigation_SameBuilding", "false");
        PlayerPrefs.Save();

        currentRoutes = routes;
        DisplayAllRoutes();
    }
    private RouteData CreateSameBuildingRoute(string buildingNodeId, Node fromNode, Node toNode)
    {
        Node buildingNode = allNodes[buildingNodeId];

        var routeData = new RouteData
        {
            startNode = buildingNode,
            endNode = buildingNode,
            path = new List<PathNode>
            {
                new PathNode
                {
                    node = buildingNode,
                    worldPosition = Vector3.zero,
                    isStart = true,
                    isEnd = true,
                    distanceToNext = 0f
                }
            },
            totalDistance = 0f,
            formattedDistance = "N/A",
            walkingTime = "< 1 minute",
            viaMode = "Indoor Navigation",
            isRecommended = true
        };

        return routeData;
    }

    private void DisplayAllRoutes()
    {
        if (resultPanel != null && destinationPanel != null)
        {
            resultPanel.SetActive(true);
            resultPanel.transform.localScale = Vector3.zero;

            resultPanel.transform.DOScale(resultPanelOriginalScale, resultPanelAnimDuration)
                .SetEase(resultPanelEaseType)
                .SetUpdate(true);
            destinationPanel.SetActive(true);
        }

        ClearRouteItems();

        if (currentRoutes.Count == 0)
        {
            return;
        }

        var firstRoute = currentRoutes[0];

        string originalFromId = PlayerPrefs.GetString("ARNavigation_OriginalFromNodeId", "");
        string originalToId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");

        Node displayFromNode = firstRoute.startNode;
        Node displayToNode = firstRoute.endNode;

        if (!string.IsNullOrEmpty(originalFromId) && allNodes.ContainsKey(originalFromId))
        {
            displayFromNode = allNodes[originalFromId];
        }

        if (!string.IsNullOrEmpty(originalToId) && allNodes.ContainsKey(originalToId))
        {
            displayToNode = allNodes[originalToId];
        }

        if (fromText != null)
        {
            fromText.text = $"<b>From:</b> {displayFromNode.name}";
        }

        if (toText != null)
        {
            string toDisplay = displayToNode.name;

            if (displayToNode.type == "indoorinfra")
            {
                string infraId = GetInfraIdFromIndoorNode(displayToNode);
                string buildingName = GetBuildingNameFromInfraId(infraId);
                toDisplay = $"{buildingName} ({displayToNode.name})";

                Debug.Log($"[DisplayAllRoutes] Indoor destination: {displayToNode.name}");
                Debug.Log($"  Related room ID: {displayToNode.related_room_id}");
                Debug.Log($"  Looked up infra ID: {infraId}");
                Debug.Log($"  Building name: {buildingName}");
            }

            toText.text = $"<b>To:</b> {toDisplay}";
        }

        for (int i = 0; i < currentRoutes.Count; i++)
        {
            CreateRouteItem(i, currentRoutes[i]);
        }

        if (currentRoutes.Count > 0)
        {
            OnRouteSelected(0);
        }
        else
        {
            if (confirmRouteButton != null)
            {
                confirmRouteButton.gameObject.SetActive(false);
            }
        }

        if (routeScrollView != null)
        {
            Canvas.ForceUpdateCanvases();
            routeScrollView.verticalNormalizedPosition = 1f;
        }
    }

    private string GetBuildingNameFromInfraId(string infraId)
    {
        foreach (var node in allNodes.Values)
        {
            if (node.type == "infrastructure" && node.related_infra_id == infraId)
            {
                return node.name;
            }
        }
        return "Building";
    }

    private void CreateRouteItem(int index, RouteData routeData)
    {
        if (routeItemPrefab == null || routeListContainer == null)
        {
            return;
        }

        GameObject itemObj = Instantiate(routeItemPrefab, routeListContainer);
        RouteItem routeItem = itemObj.GetComponent<RouteItem>();

        if (routeItem != null)
        {
            routeItem.Initialize(index, routeData, OnRouteSelected);
            routeItemInstances.Add(routeItem);
        }
    }

    private void ClearRouteItems()
    {
        if (routeListContainer == null)
        {
            return;
        }

        routeItemInstances.Clear();

        foreach (Transform child in routeListContainer)
        {
            Destroy(child.gameObject);
        }
    }

    private void OnRouteSelected(int routeIndex)
    {
        if (routeIndex < 0 || routeIndex >= currentRoutes.Count)
        {
            return;
        }

        selectedRouteIndex = routeIndex;

        for (int i = 0; i < routeItemInstances.Count; i++)
        {
            routeItemInstances[i].SetSelected(i == routeIndex);
        }

        if (confirmRouteButton != null)
        {
            confirmRouteButton.gameObject.SetActive(true);
        }

        if (pathfinding != null)
        {
            pathfinding.SetActiveRoute(routeIndex);
        }
    }

    private void OnConfirmRouteClicked()
    {
        if (selectedRouteIndex < 0 || selectedRouteIndex >= currentRoutes.Count)
        {
            return;
        }

        RouteData selectedRoute = currentRoutes[selectedRouteIndex];

        DirectionGenerator directionGen = GetComponent<DirectionGenerator>();
        if (directionGen == null)
        {
            directionGen = gameObject.AddComponent<DirectionGenerator>();
        }

        StartCoroutine(GenerateAndSaveDirections(directionGen, selectedRoute));
    }

    private IEnumerator GenerateAndSaveDirections(DirectionGenerator directionGen, RouteData selectedRoute)
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (!directionGen.IsDataLoaded() && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (!directionGen.IsDataLoaded())
        {
            yield break;
        }

        List<NavigationDirection> directions = directionGen.GenerateDirections(selectedRoute);

        if (directions == null || directions.Count == 0)
        {
            yield break;
        }

        SaveRouteDataForAR(selectedRoute, directions);

        isLocationLocked = false;
        lockedNode = null;

        ARManagerCleanup arCleanup = FindObjectOfType<ARManagerCleanup>();
        if (arCleanup != null)
        {
            arCleanup.LoadARNavigationWithScene("ARScene");
        }
    }

    private void SaveRouteDataForAR(RouteData route, List<NavigationDirection> directions)
    {
        Debug.Log("=============== SAVE ROUTE DATA START ===============");

        int oldDirectionCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0);

        for (int i = 0; i < oldDirectionCount; i++)
        {
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Instruction");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Turn");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_Distance");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNodeId");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_DestNode");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_IsIndoorGrouped");
            PlayerPrefs.DeleteKey($"ARNavigation_Direction_{i}_IsIndoorDirection");
        }

        PlayerPrefs.SetString("ARNavigation_StartNodeId", route.startNode.node_id);
        PlayerPrefs.SetString("ARNavigation_EndNodeId", route.endNode.node_id);
        PlayerPrefs.SetString("ARNavigation_StartNodeName", route.startNode.name);
        PlayerPrefs.SetString("ARNavigation_EndNodeName", route.endNode.name);
        PlayerPrefs.SetFloat("ARNavigation_TotalDistance", route.totalDistance);
        PlayerPrefs.SetString("ARNavigation_FormattedDistance", route.formattedDistance);
        PlayerPrefs.SetString("ARNavigation_WalkingTime", route.walkingTime);
        PlayerPrefs.SetString("ARNavigation_ViaMode", route.viaMode);

        Debug.Log($"✅ Saved route metadata:");
        Debug.Log($"  Start: {route.startNode.node_id} ({route.startNode.name})");
        Debug.Log($"  End: {route.endNode.node_id} ({route.endNode.name})");
        Debug.Log($"  Distance: {route.formattedDistance}");

        PlayerPrefs.SetInt("ARNavigation_PathNodeCount", route.path.Count);

        for (int i = 0; i < route.path.Count; i++)
        {
            PlayerPrefs.SetString($"ARNavigation_PathNode_{i}", route.path[i].node.node_id);
        }

        Debug.Log($"✅ Saved {route.path.Count} path nodes");

        int edgeCount = route.path.Count - 1;
        PlayerPrefs.SetInt("ARNavigation_EdgeCount", edgeCount);

        for (int i = 0; i < edgeCount; i++)
        {
            string fromNode = route.path[i].node.node_id;
            string toNode = route.path[i + 1].node.node_id;

            PlayerPrefs.SetString($"ARNavigation_Edge_{i}_From", fromNode);
            PlayerPrefs.SetString($"ARNavigation_Edge_{i}_To", toNode);
        }

        Debug.Log($"✅ Saved {edgeCount} edges");

        PlayerPrefs.SetInt("ARNavigation_DirectionCount", directions.Count);

        for (int i = 0; i < directions.Count; i++)
        {
            var dir = directions[i];

            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Instruction", dir.instruction);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Turn", dir.turn.ToString());
            PlayerPrefs.SetFloat($"ARNavigation_Direction_{i}_Distance", dir.distanceInMeters);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNodeId", dir.destinationNode?.node_id ?? "");
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNode", dir.destinationNode?.name ?? "Unknown");

            PlayerPrefs.SetInt($"ARNavigation_Direction_{i}_IsIndoorGrouped", dir.isIndoorGrouped ? 1 : 0);
            PlayerPrefs.SetInt($"ARNavigation_Direction_{i}_IsIndoorDirection", dir.isIndoorDirection ? 1 : 0);
        }

        Debug.Log($"Saved {directions.Count} directions");

        ARModeHelper.SetARMode(true);
    }
    public void HideResults()
    {
        if (resultPanel != null)
        {
            resultPanel.transform.DOScale(Vector3.zero, resultPanelAnimDuration)
            .SetEase(Ease.InBack)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                resultPanel.SetActive(false);
                if (destinationPanel != null)
                {
                    destinationPanel.SetActive(false);
                }

                if (BGPanel != null)
                {
                    BGPanel.SetActive(false);
                }
            });
        }

        ClearRouteItems();
    }

    public void ClearCurrentPath()
    {
        selectedToNodeId = null;
        currentRoutes.Clear();
        routeItemInstances.Clear();
        selectedRouteIndex = -1;

        if (confirmRouteButton != null)
        {
            confirmRouteButton.gameObject.SetActive(false);
        }

        if (pathfinding != null)
        {
            pathfinding.ClearCurrentPath();
        }

        HideResults();
    }

    public void RefreshGPSLocation()
    {
        if (useGPSForFromLocation && !useStaticTesting && nodesLoaded && !isLocationLocked)
        {
            UpdateFromLocationByGPS();
        }
    }

    public void SetFromLocationByQR(string nodeId)
    {
        if (allNodes.ContainsKey(nodeId))
        {
            selectedFromNodeId = nodeId;
            currentNearestNode = allNodes[nodeId];
        }
    }

    public void ToggleGPSMode(bool useGPS)
    {
        useGPSForFromLocation = useGPS;

        if (useGPS && nodesLoaded && !isLocationLocked)
        {
            UpdateFromLocationByGPS();
        }
    }

    public RouteData GetSelectedRoute()
    {
        if (selectedRouteIndex >= 0 && selectedRouteIndex < currentRoutes.Count)
        {
            return currentRoutes[selectedRouteIndex];
        }
        return null;
    }

    public List<RouteData> GetAllRoutes()
    {
        return new List<RouteData>(currentRoutes);
    }

    public bool IsLocationLocked()
    {
        return isLocationLocked;
    }

    public string GetCurrentFromLocationName()
    {
        if (isLocationLocked && lockedNode != null)
        {
            return lockedNode.name;
        }
        else if (currentNearestNode != null)
        {
            return currentNearestNode.name;
        }
        return "Unknown";
    }

    public bool IsReadyForPathfinding()
    {
        return nodesLoaded &&
               !string.IsNullOrEmpty(selectedFromNodeId) &&
               !string.IsNullOrEmpty(selectedToNodeId);
    }
}