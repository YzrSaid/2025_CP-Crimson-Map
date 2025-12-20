using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class DirectionDisplayManager : MonoBehaviour
{
    [Header("Direction Panel UI")]
    public GameObject directionPanel;
    public TextMeshProUGUI directionText;

    [Header("Turn Icons")]
    public GameObject turnRightImage;
    public GameObject turnLeftImage;
    public GameObject walkStraightImage;
    public GameObject enterImage;
    public GameObject turnIconsContainer;

    [Header("Compass Arrow")]
    public CompassNavigationArrow compassArrow;

    [Header("All Directions UI")]
    public Transform directionsScrollContent;
    public GameObject directionItemPrefab;

    [Header("Success Panel UI")]
    public GameObject successPanel;
    public TextMeshProUGUI successBodyText;
    public Button successCloseButton;
    public GameObject successPanelBackground;
    public float successAnimationDuration = 0.3f;
    public Ease successEaseType = Ease.OutBack;

    [Header("Lookahead, Off Route, and Overshoot Algorithm Settings")]
    public float lookaheadDistanceThreshold = 5f;
    public float offRouteDistanceThreshold = 25f;
    public float destinationOvershootThreshold = 10f;
    public int maxLookaheadCount = 3;

    public GameObject offRoutePanel;
    public TextMeshProUGUI offRouteTitle;
    public TextMeshProUGUI offRouteBody;
    public Button offRouteContinueButton;
    public GameObject offRouteBackground;

    [Header("Voice & Sound Effects")]
    [Tooltip("AudioSource for playing sound effects")]
    public AudioSource audioSource;

    [Tooltip("Sound when reaching waypoint/checkpoint")]
    public AudioClip checkpointSound;

    [Tooltip("Sound when reaching final destination")]
    public AudioClip destinationSound;

    [Tooltip("Enable voice instructions")]
    public bool enableVoiceInstructions = true;

    [Tooltip("Delay before speaking (seconds)")]
    public float voiceDelay = 0.5f;

    private AndroidJavaObject tts;

    private bool isOffRoutePanelActive = false;
    private bool isOffRouteConditionActive = false;

    private bool hasPassedDestination = false;
    private Vector2 destinationGPS;
    private bool isDestinationNode = false;
    private Node currentDestinationNode;

    public bool enableKeyboardTesting = true;
    public float autoProgressDistance = 10f;

    public float distanceUpdateInterval = 0.5f;
    private float lastDistanceUpdateTime = 0f;

    private List<NavigationDirection> allDirections = new List<NavigationDirection>();
    private List<DirectionItemUI> directionItemInstances = new List<DirectionItemUI>();
    private int currentDirectionIndex = 0;
    private bool isNavigationActive = false;

    private Vector2 userLocation;
    private Node currentTargetNode;
    private float distanceToTarget = 0f;
    private bool hasAutoProgressed = false;

    private UnifiedARManager arManager;
    private Vector3 successPanelOriginalScale;

    private UnifiedARNavigationMarkerSpawner markerSpawner;

    void Start()
    {
        arManager = FindObjectOfType<UnifiedARManager>();
        markerSpawner = FindObjectOfType<UnifiedARNavigationMarkerSpawner>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        InitializeAndroidTTS();

        if (directionPanel != null)
            directionPanel.SetActive(false);

        HideAllTurnIcons();

        if (compassArrow == null)
            compassArrow = FindObjectOfType<CompassNavigationArrow>();

        if (successPanel != null)
        {
            successPanelOriginalScale = successPanel.transform.localScale;
            successPanel.SetActive(false);
        }

        if (successPanelBackground != null)
            successPanelBackground.SetActive(false);

        if (successCloseButton != null)
            successCloseButton.onClick.AddListener(OnSuccessCloseClicked);

        if (offRoutePanel != null)
        {
            offRoutePanel.SetActive(false);
        }

        if (offRouteBackground != null)
        {
            offRouteBackground.SetActive(false);
        }

        LoadDirectionsFromPlayerPrefs();

        if (allDirections.Count > 0)
        {
            StartNavigation();
        }
    }

    private void InitializeAndroidTTS()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, null);

                Debug.Log("[TTS] Android TTS initialized successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TTS] Failed to initialize Android TTS: {e.Message}");
            }
        }
        else
        {
            Debug.Log("[TTS] Not on Android platform - TTS disabled");
        }
    }

    private void Speak(string message)
    {
        if (!enableVoiceInstructions) return;

        if (Application.platform == RuntimePlatform.Android && tts != null)
        {
            try
            {
                tts.Call<int>("speak", message, 0, null, null);
                Debug.Log($"[TTS] Speaking: {message}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TTS] Error speaking: {e.Message}");
            }
        }
        else
        {
            Debug.Log($"[TTS] Would speak: {message}");
        }
    }

    private void PlayCheckpointReached(string instruction)
    {
        if (checkpointSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(checkpointSound);
        }

        if (enableVoiceInstructions)
        {
            StartCoroutine(SpeakAfterDelay(instruction, voiceDelay));
        }
    }

    private void PlayDestinationReached(string destinationName)
    {
        if (destinationSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(destinationSound);
        }

        if (enableVoiceInstructions)
        {
            string message = $"You have reached your destination. Welcome to {destinationName}!";
            StartCoroutine(SpeakAfterDelay(message, voiceDelay));
        }
    }

    private IEnumerator SpeakAfterDelay(string message, float delay)
    {
        yield return new WaitForSeconds(delay);
        Speak(message);
    }

    void Update()
    {
        if (!isNavigationActive)
            return;

        if (enableKeyboardTesting && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            MoveToNextDirection();
        }

        UpdateUserLocation();

        if (currentDirectionIndex < allDirections.Count - 1)
        {
            CheckIfPassedAnyUpcomingNodes();

            CheckIfOffRoute();
        }
        else if (currentDirectionIndex == allDirections.Count - 1)
        {
            CheckDestinationOvershoot();
        }

        UpdateDistanceToTarget();
        CheckAutoProgress();

        if (Time.time - lastDistanceUpdateTime >= distanceUpdateInterval)
        {
            UpdateDirectionTextWithDistance();
            lastDistanceUpdateTime = Time.time;
        }
    }

    private void CheckIfPassedAnyUpcomingNodes()
    {
        if (currentDirectionIndex >= allDirections.Count - 1)
            return;

        int lookaheadCount = Mathf.Min(maxLookaheadCount, allDirections.Count - currentDirectionIndex - 1);

        for (int i = 1; i <= lookaheadCount; i++)
        {
            int checkIndex = currentDirectionIndex + i;
            NavigationDirection checkDir = allDirections[checkIndex];

            if (checkDir.destinationNode == null || checkDir.isIndoorGrouped)
                continue;

            Vector2 targetGPS = new Vector2(checkDir.destinationNode.latitude, checkDir.destinationNode.longitude);
            float distanceToNode = CalculateDistanceGPS(userLocation, targetGPS);

            Debug.Log($"[Lookahead] Distance to upcoming node {checkDir.destinationNode.name}: {distanceToNode:F1}m (threshold: {lookaheadDistanceThreshold}m)");

            if (distanceToNode <= lookaheadDistanceThreshold)
            {
                Debug.Log($"[Lookahead] ✅ User passed upcoming node {checkDir.destinationNode.name} at index {checkIndex}");

                int oldIndex = currentDirectionIndex;
                currentDirectionIndex = checkIndex;

                HideSkippedMarkers(oldIndex, checkIndex);

                DisplayCurrentDirection();
                return;
            }
        }
    }

    private void CheckIfOffRoute()
    {
        if (isOffRouteConditionActive) return;

        if (isOffRoutePanelActive) return;

        float minDistance = float.MaxValue;
        Node closestNode = null;

        for (int i = currentDirectionIndex; i < allDirections.Count; i++)
        {
            NavigationDirection dir = allDirections[i];
            if (dir.destinationNode == null || dir.isIndoorGrouped)
                continue;

            Vector2 nodeGPS = new Vector2(dir.destinationNode.latitude, dir.destinationNode.longitude);
            float distance = CalculateDistanceGPS(userLocation, nodeGPS);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestNode = dir.destinationNode;
            }
        }

        if (minDistance > offRouteDistanceThreshold)
        {
            isOffRouteConditionActive = true;
            ShowOffRoutePanel(closestNode, minDistance);
        }
    }

    private void CheckDestinationOvershoot()
    {
        if (hasPassedDestination || currentDestinationNode == null)
            return;

        if (!isDestinationNode)
        {
            NavigationDirection lastDir = allDirections[allDirections.Count - 1];
            if (lastDir.destinationNode != null)
            {
                currentDestinationNode = lastDir.destinationNode;
                destinationGPS = new Vector2(currentDestinationNode.latitude, currentDestinationNode.longitude);
                isDestinationNode = true;
            }
            return;
        }

        float distanceFromDestination = CalculateDistanceGPS(userLocation, destinationGPS);

        Debug.Log($"[Destination] Distance from destination {currentDestinationNode.name}: {distanceFromDestination:F1}m (threshold: {destinationOvershootThreshold}m)");

        if (distanceFromDestination > destinationOvershootThreshold)
        {
            Debug.Log($"[Destination] ⚠️ User passed destination by {distanceFromDestination:F1}m!");
            hasPassedDestination = true;
            ShowPassedDestinationPanel(distanceFromDestination);
        }
    }

    private void HideSkippedMarkers(int fromIndex, int toIndex)
    {
        if (markerSpawner == null) return;

        Debug.Log($"[DirectionManager] Hiding markers for skipped nodes {fromIndex} to {toIndex}");

        for (int i = fromIndex; i < toIndex; i++)
        {
            if (i < allDirections.Count)
            {
                NavigationDirection dir = allDirections[i];
                if (dir.destinationNode != null && !dir.isIndoorGrouped)
                {

                }
            }
        }
    }

    private void ShowOffRoutePanel(Node nearestNode, float distance)
    {
        if (offRoutePanel == null)
        {
            CreateSimpleOffRoutePanel(nearestNode, distance);
            return;
        }

        if (offRouteTitle != null)
            offRouteTitle.text = "You're Off Route";

        if (offRouteBody != null)
        {
            string nodeName = nearestNode != null ? nearestNode.name : "the nearest point";
            offRouteBody.text = $"You are {Mathf.RoundToInt(distance)} meters away from {nodeName}.\n\nClick OK to continue navigation.";
        }

        if (offRouteBackground != null)
            offRouteBackground.SetActive(true);

        offRoutePanel.SetActive(true);
        isOffRoutePanelActive = true;

        if (offRouteContinueButton != null)
            offRouteContinueButton.onClick.RemoveAllListeners();

        if (offRouteContinueButton != null)
            offRouteContinueButton.onClick.AddListener(HideOffRoutePanel);
    }

    private void ShowPassedDestinationPanel(float distancePassed)
    {
        if (offRoutePanel == null) return;

        if (offRouteTitle != null)
            offRouteTitle.text = "Passed Destination";

        if (offRouteBody != null)
        {
            offRouteBody.text = $"You have passed your destination by {Mathf.RoundToInt(distancePassed)} meters.\n\nYou are now beyond your intended stopping point.";
        }

        if (offRouteBackground != null)
            offRouteBackground.SetActive(true);

        offRoutePanel.SetActive(true);
        isOffRoutePanelActive = true;
    }

    private void HideOffRoutePanel()
    {
        if (offRoutePanel != null)
            offRoutePanel.SetActive(false);
        if (offRouteBackground != null)
            offRouteBackground.SetActive(false);

        isOffRoutePanelActive = false;

        Debug.Log("[OffRoute] Panel hidden - starting 40 second cooldown");
        StartCoroutine(OffRouteCooldown());
    }

    private IEnumerator OffRouteCooldown()
    {
        float cooldownTime = 40f;
        float timer = 0f;

        while (timer < cooldownTime)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        isOffRouteConditionActive = false;
    }

    private void CreateSimpleOffRoutePanel(Node nearestNode, float distance)
    {
        Debug.LogWarning($"[DirectionManager] Off-route panel not assigned! User is {distance:F1}m from {nearestNode?.name}");
    }

    private void UpdateUserLocation()
    {
        if (GPSManager.Instance == null)
        {
            return;
        }

        if (GPSManager.Instance.IsUsingQROverride())
        {
            userLocation = GPSManager.Instance.GetCoordinates();
        }
        else
        {
            Vector2 rawGPS = GPSManager.Instance.GetRawSmoothedGPSCoordinates();

            if (rawGPS.magnitude > 0.0001f)
            {
                userLocation = rawGPS;
            }
        }
    }

    private void LoadDirectionsFromPlayerPrefs()
    {
        int directionCount = PlayerPrefs.GetInt("ARNavigation_DirectionCount", 0);

        if (directionCount == 0)
        {
            return;
        }

        allDirections.Clear();

        string mapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        StartCoroutine(LoadNodesAndDirections(mapId, directionCount));
    }

    private IEnumerator LoadNodesAndDirections(string mapId, int directionCount)
    {
        string fileName = $"nodes_{mapId}.json";
        Node[] allNodes = null;
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    allNodes = JsonHelper.FromJson<Node>(jsonContent);
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

        for (int i = 0; i < directionCount; i++)
        {
            NavigationDirection dir = new NavigationDirection
            {
                instruction = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Instruction", ""),
                turn = (TurnDirection)System.Enum.Parse(typeof(TurnDirection),
                       PlayerPrefs.GetString($"ARNavigation_Direction_{i}_Turn", "Straight")),
                distanceInMeters = PlayerPrefs.GetFloat($"ARNavigation_Direction_{i}_Distance", 0f),
                isIndoorGrouped = PlayerPrefs.GetInt($"ARNavigation_Direction_{i}_IsIndoorGrouped", 0) == 1,
                isIndoorDirection = PlayerPrefs.GetInt($"ARNavigation_Direction_{i}_IsIndoorDirection", 0) == 1
            };

            string destNodeId = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNodeId", "");

            if (allNodes != null && !string.IsNullOrEmpty(destNodeId))
            {
                dir.destinationNode = System.Array.Find(allNodes, n => n.node_id == destNodeId);

                if (dir.destinationNode == null)
                {
                    dir.destinationNode = new Node
                    {
                        name = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNode", "Unknown"),
                        node_id = destNodeId
                    };
                }
            }
            else
            {
                dir.destinationNode = new Node
                {
                    name = PlayerPrefs.GetString($"ARNavigation_Direction_{i}_DestNode", "Unknown")
                };
            }

            allDirections.Add(dir);
        }

        PopulateDirectionItems();

        if (allDirections.Count > 0)
        {
            StartNavigation();
        }
    }

    private void PopulateDirectionItems()
    {
        ClearDirectionItems();

        if (directionItemPrefab == null || directionsScrollContent == null)
        {
            return;
        }

        for (int i = 0; i < allDirections.Count; i++)
        {
            GameObject itemObj = Instantiate(directionItemPrefab, directionsScrollContent);
            DirectionItemUI itemUI = itemObj.GetComponent<DirectionItemUI>();

            if (itemUI != null)
            {
                itemUI.Initialize(i, allDirections[i]);
                directionItemInstances.Add(itemUI);
            }
        }
    }

    private void ClearDirectionItems()
    {
        directionItemInstances.Clear();

        if (directionsScrollContent != null)
        {
            foreach (Transform child in directionsScrollContent)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void StartNavigation()
    {
        if (allDirections.Count == 0)
        {
            return;
        }

        isNavigationActive = true;
        currentDirectionIndex = 0;

        if (directionPanel != null)
            directionPanel.SetActive(true);

        DisplayCurrentDirection();
    }

    private void DisplayCurrentDirection()
    {
        if (currentDirectionIndex >= allDirections.Count)
        {
            CompleteNavigation();
            return;
        }

        NavigationDirection currentDir = allDirections[currentDirectionIndex];

        if (currentDir.isIndoorGrouped)
        {
            string groupedInstructions = "";
            List<Node> groupedTargets = new List<Node>();

            int startIndex = currentDirectionIndex;
            while (currentDirectionIndex < allDirections.Count &&
                   allDirections[currentDirectionIndex].isIndoorGrouped)
            {
                var indoorDir = allDirections[currentDirectionIndex];
                groupedInstructions += indoorDir.instruction + "\n\n";
                groupedTargets.Add(indoorDir.destinationNode);
                currentDirectionIndex++;
            }

            if (directionText != null)
            {
                directionText.text = groupedInstructions.Trim();
            }

            HideAllTurnIcons();

            if (groupedTargets.Count > 0)
            {
                currentTargetNode = groupedTargets[groupedTargets.Count - 1];

                if (compassArrow != null)
                {
                    compassArrow.SetTargetNode(currentTargetNode);
                    compassArrow.SetActive(true);
                }
            }

            hasAutoProgressed = false;

            UpdateDirectionItemsStatus();

            PlayCheckpointReached(groupedInstructions.Trim());

            return;
        }

        currentTargetNode = currentDir.destinationNode;

        UpdateDirectionTextWithDistance();

        ShowTurnIcon(currentDir.turn);

        if (compassArrow != null)
        {
            compassArrow.SetTargetNode(currentTargetNode);
            compassArrow.SetActive(true);
        }

        hasAutoProgressed = false;
        UpdateDirectionItemsStatus();

        PlayCheckpointReached(currentDir.instruction);
    }

    private void UpdateDirectionTextWithDistance()
    {
        if (directionText == null || currentDirectionIndex >= allDirections.Count)
            return;

        NavigationDirection currentDir = allDirections[currentDirectionIndex];

        if (currentDir.isIndoorGrouped)
            return;

        string baseInstruction = currentDir.instruction;

        int lastOpenParen = baseInstruction.LastIndexOf('(');
        if (lastOpenParen != -1)
        {
            baseInstruction = baseInstruction.Substring(0, lastOpenParen).TrimEnd();
        }

        float realTimeDistance = distanceToTarget;

        string updatedInstruction = $"{baseInstruction} ({FormatDistance(realTimeDistance)})";

        directionText.text = updatedInstruction;
    }

    private string FormatDistance(float distanceInMeters)
    {
        if (distanceInMeters < 1000f)
        {
            return $"{Mathf.RoundToInt(distanceInMeters)}m";
        }
        else
        {
            return $"{(distanceInMeters / 1000f):F1}km";
        }
    }

    public void ShowTurnIconContainer()
    {
        if (turnIconsContainer != null)
        {
            turnIconsContainer.SetActive(true);
        }
    }

    private void HideAllTurnIcons()
    {
        if (turnRightImage != null) turnRightImage.SetActive(false);
        if (turnLeftImage != null) turnLeftImage.SetActive(false);
        if (walkStraightImage != null) walkStraightImage.SetActive(false);
        if (enterImage != null) enterImage.SetActive(false);

        if (turnIconsContainer != null) turnIconsContainer.SetActive(false);
    }

    private void ShowTurnIcon(TurnDirection turn)
    {
        HideAllTurnIcons();
        ShowTurnIconContainer();

        switch (turn)
        {
            case TurnDirection.Right:
            case TurnDirection.SlightRight:
                if (turnRightImage != null)
                {
                    turnRightImage.SetActive(true);
                }
                break;

            case TurnDirection.Left:
            case TurnDirection.SlightLeft:
                if (turnLeftImage != null)
                {
                    turnLeftImage.SetActive(true);
                }
                break;

            case TurnDirection.Straight:
                if (walkStraightImage != null)
                {
                    walkStraightImage.SetActive(true);
                }
                break;

            case TurnDirection.Enter:
            case TurnDirection.Arrive:
                if (enterImage != null)
                {
                    enterImage.SetActive(true);
                }
                break;

            default:
                if (walkStraightImage != null)
                {
                    walkStraightImage.SetActive(true);
                }
                break;
        }
    }

    private void MoveToNextDirection()
    {
        if (!isNavigationActive)
            return;

        if (currentDirectionIndex < allDirections.Count)
        {
            NavigationDirection reachedDir = allDirections[currentDirectionIndex];
            if (reachedDir.destinationNode != null && !reachedDir.isIndoorGrouped)
            {
                string nodeId = reachedDir.destinationNode.node_id;

                if (markerSpawner != null)
                {
                    markerSpawner.MarkJourneyNodeAsPassed(nodeId);
                }
            }
        }

        if (currentDirectionIndex >= allDirections.Count - 1)
        {
            CompleteNavigation();
            return;
        }

        currentDirectionIndex++;
        DisplayCurrentDirection();
    }

    private void UpdateDistanceToTarget()
    {
        if (currentTargetNode == null)
            return;

        Vector2 targetGPS = new Vector2(currentTargetNode.latitude, currentTargetNode.longitude);
        distanceToTarget = CalculateDistanceGPS(userLocation, targetGPS);
    }

    private void CheckAutoProgress()
    {
        if (hasAutoProgressed)
            return;

        if (currentDirectionIndex < allDirections.Count)
        {
            var currentDir = allDirections[currentDirectionIndex];
            if (currentDir.isIndoorGrouped)
            {
                return;
            }
        }

        if (distanceToTarget <= autoProgressDistance && distanceToTarget > 0)
        {
            hasAutoProgressed = true;
            MoveToNextDirection();
        }
    }

    public void OnNodeReached(string nodeId)
    {
        Debug.Log($"[DirectionManager] QR Recalibration triggered for node: {nodeId}");

        int targetIndex = FindDirectionIndexByNodeId(nodeId);

        if (targetIndex == -1)
        {
            Debug.Log($"[DirectionManager] Node {nodeId} not in route. Finding closest route node...");

            Node scannedNode = LoadNodeById(nodeId);
            if (scannedNode == null)
            {
                Debug.LogWarning($"[DirectionManager] Could not load node {nodeId}");
                return;
            }

            FindClosestUpcomingNode(scannedNode);
            return;
        }

        Debug.Log($"[DirectionManager] Found node at index {targetIndex}, current is {currentDirectionIndex}");

        if (targetIndex < currentDirectionIndex)
        {
            Debug.Log($"[DirectionManager] Node already passed, ignoring");
            return;
        }

        if (targetIndex > currentDirectionIndex)
        {
            Debug.Log($"[DirectionManager] Jumping ahead to direction index {targetIndex}");
            int oldIndex = currentDirectionIndex;
            currentDirectionIndex = targetIndex;

            HideSkippedMarkers(oldIndex, targetIndex);

            DisplayCurrentDirection();
        }
        else
        {
            Debug.Log($"[DirectionManager] Moving to next direction");
            MoveToNextDirection();
        }
    }

    private void FindClosestUpcomingNode(Node scannedNode)
    {
        Vector2 scannedGPS = new Vector2(scannedNode.latitude, scannedNode.longitude);

        float minDistance = float.MaxValue;
        int closestIndex = -1;
        Node closestNode = null;

        for (int i = currentDirectionIndex; i < allDirections.Count; i++)
        {
            NavigationDirection dir = allDirections[i];
            if (dir.destinationNode == null || dir.isIndoorGrouped)
                continue;

            Vector2 nodeGPS = new Vector2(dir.destinationNode.latitude, dir.destinationNode.longitude);
            float distance = CalculateDistanceGPS(scannedGPS, nodeGPS);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
                closestNode = dir.destinationNode;
            }
        }

        Debug.Log($"[DirectionManager] Scanned node is {minDistance:F1}m from route node {closestNode?.name} at index {closestIndex}");

        if (closestIndex != -1)
        {
            if (minDistance <= lookaheadDistanceThreshold)
            {
                Debug.Log($"[DirectionManager] ✅ Close enough ({minDistance:F1}m), jumping to index {closestIndex}");
                int oldIndex = currentDirectionIndex;
                currentDirectionIndex = closestIndex;
                HideSkippedMarkers(oldIndex, closestIndex);
                DisplayCurrentDirection();
            }
            else if (minDistance > offRouteDistanceThreshold)
            {
                Debug.Log($"[DirectionManager] ⚠️ Too far ({minDistance:F1}m), showing off-route panel");
            }
            else
            {
                Debug.Log($"[DirectionManager] Somewhat close ({minDistance:F1}m), staying at current direction");
            }
        }
        else
        {
            Debug.Log($"[DirectionManager] No route nodes found!");
        }
    }

    private Node LoadNodeById(string nodeId)
    {
        foreach (var dir in allDirections)
        {
            if (dir.destinationNode != null && dir.destinationNode.node_id == nodeId)
                return dir.destinationNode;
        }

        Debug.LogWarning($"[DirectionManager] Could not load node {nodeId} from directions");
        return null;
    }

    private int FindDirectionIndexByNodeId(string nodeId)
    {
        for (int i = 0; i < allDirections.Count; i++)
        {
            if (allDirections[i].destinationNode != null &&
                allDirections[i].destinationNode.node_id == nodeId)
            {
                return i;
            }
        }
        return -1;
    }

    private void UpdateDirectionItemsStatus()
    {
        for (int i = 0; i < directionItemInstances.Count; i++)
        {
            bool isCompleted = i < currentDirectionIndex || !isNavigationActive;
            directionItemInstances[i].SetCompleted(isCompleted);
        }
    }

    private void CompleteNavigation()
    {
        isNavigationActive = false;

        if (directionText != null)
            directionText.text = "You have arrived at your destination!";

        HideAllTurnIcons();
        if (enterImage != null)
            enterImage.SetActive(true);

        if (compassArrow != null)
            compassArrow.SetActive(false);

        UpdateDirectionItemsStatus();

        string destinationName = "your destination";

        if (allDirections.Count > 0)
        {
            NavigationDirection lastDir = allDirections[allDirections.Count - 1];
            if (lastDir.destinationNode != null && !string.IsNullOrEmpty(lastDir.destinationNode.name))
            {
                destinationName = lastDir.destinationNode.name;
            }
        }

        PlayDestinationReached(destinationName);
        ShowSuccessPanel();
    }

    private void ShowSuccessPanel()
    {
        if (successPanel == null)
            return;
        bool toIsIndoor = PlayerPrefs.GetInt("ARNavigation_ToIsIndoor", 0) == 1;

        bool isSameBuilding = PlayerPrefs.GetInt("ARNavigation_SameBuilding", 0) == 1;

        if (successBodyText != null)
        {
            // CASE #1 (Outdoor to Indoor - Same Building)
            if (toIsIndoor && isSameBuilding)
            {
                successBodyText.text =
                    $"<b>You're already in the building!</b> " +
                    $"Check the directions panel to see which floor the room is on, and use the indoor map to navigate inside the building.";
            }
            // CASE #2 (Outdoor to Indoor - Different Building)
            else if (toIsIndoor && !isSameBuilding)
            {
                successBodyText.text =
                    $"<b>Congratulations!</b> You've arrived at the building. " +
                    $"Check the directions panel to see which floor the room is on, and use the indoor map to navigate inside the building.";
            }
            // CASE #3 (Outdoor to Outdoor)
            else
            {
                successBodyText.text =
                    $"<b>Congratulations!</b> You've successfully reached your destination! " +
                    $"You can now stop AR navigation or explore other locations on campus.";
            }
        }

        if (successPanelBackground != null)
        {
            successPanelBackground.SetActive(true);
        }

        successPanel.SetActive(true);
        successPanel.transform.localScale = Vector3.zero;
        successPanel.transform.DOScale(successPanelOriginalScale, successAnimationDuration)
            .SetEase(successEaseType)
            .SetUpdate(true);
    }

    private void OnSuccessCloseClicked()
    {
        if (successPanel != null)
        {
            successPanel.transform.DOScale(Vector3.zero, successAnimationDuration)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    successPanel.SetActive(false);

                    if (successPanelBackground != null)
                    {
                        successPanelBackground.SetActive(false);
                    }
                });
        }
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

    public void ResetNavigation()
    {
        currentDirectionIndex = 0;
        isNavigationActive = false;
        hasPassedDestination = false;
        isDestinationNode = false;

        isOffRoutePanelActive = false;
        isOffRouteConditionActive = false;

        if (directionPanel != null)
            directionPanel.SetActive(false);

        HideAllTurnIcons();

        if (compassArrow != null)
            compassArrow.SetActive(false);

        if (successPanel != null)
            successPanel.SetActive(false);

        if (successPanelBackground != null)
            successPanelBackground.SetActive(false);

        if (offRoutePanel != null)
            offRoutePanel.SetActive(false);

        if (offRouteBackground != null)
            offRouteBackground.SetActive(false);

        UpdateDirectionItemsStatus();
    }

    public void ReloadDirections()
    {
        allDirections.Clear();
        ClearDirectionItems();
        currentDirectionIndex = 0;
        isNavigationActive = false;

        LoadDirectionsFromPlayerPrefs();
    }

    public int GetCurrentDirectionIndex()
    {
        return currentDirectionIndex;
    }

    public NavigationDirection GetCurrentDirection()
    {
        if (currentDirectionIndex >= 0 && currentDirectionIndex < allDirections.Count)
            return allDirections[currentDirectionIndex];
        return null;
    }

    public List<NavigationDirection> GetAllDirections()
    {
        return new List<NavigationDirection>(allDirections);
    }

    void OnDestroy()
    {
        if (Application.platform == RuntimePlatform.Android && tts != null)
        {
            try
            {
                tts.Call("shutdown");
                Debug.Log("[TTS] Android TTS shutdown");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TTS] Error shutting down: {e.Message}");
            }
        }
    }
}