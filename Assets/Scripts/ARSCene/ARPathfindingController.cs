using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ARPathfindingController : MonoBehaviour
{
    [Header("References")]
    public AStarPathfinding pathfinding;

    [Header("Route Selection UI")]
    public GameObject routeSelectionPanel;
    public GameObject routeSelectionBGPanel;
    public Transform routeListContainer;
    public GameObject routeItemPrefab;
    public ScrollRect routeScrollView;
    public Button confirmRouteButton;
    public Button cancelRouteButton;

    [Header("Route Display")]
    public TextMeshProUGUI fromText;
    public TextMeshProUGUI toText;

    [Header("Error Panel")]
    public GameObject errorPanel;
    public TextMeshProUGUI errorMessageText;
    public Button errorOkButton;

    [Header("Loading")]
    public ARLoadingManager arLoadingManager;

    private string currentMapId;
    private List<string> currentCampusIds;
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, IndoorInfrastructure> indoorInfrastructures = new Dictionary<string, IndoorInfrastructure>();

    private List<RouteData> currentRoutes = new List<RouteData>();
    private List<RouteItem> routeItemInstances = new List<RouteItem>();
    private int selectedRouteIndex = -1;

    private string rerouteFromNodeId;
    private string rerouteToNodeId;
    private ARRerouteUIManager.ExemptionType exemptionType;
    private string exemptedItemId;

    private HashSet<string> blockedNodes = new HashSet<string>();
    private HashSet<string> blockedEdges = new HashSet<string>();

    void Start()
    {
        if (confirmRouteButton != null)
        {
            confirmRouteButton.onClick.AddListener(OnConfirmRouteClicked);
            confirmRouteButton.gameObject.SetActive(false);
        }

        if (cancelRouteButton != null)
        {
            cancelRouteButton.onClick.AddListener(OnCancelRouteClicked);
        }

        if (errorOkButton != null)
        {
            errorOkButton.onClick.AddListener(OnErrorOkClicked);
        }

        if (routeSelectionPanel != null)
        {
            routeSelectionPanel.SetActive(false);
        }

        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }

        currentMapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        string campusIdsStr = PlayerPrefs.GetString("ARScene_CampusIds", "");
        currentCampusIds = string.IsNullOrEmpty(campusIdsStr)
            ? new List<string>()
            : new List<string>(campusIdsStr.Split(','));

        StartCoroutine(InitializePathfinding());
    }

    void OnDestroy()
    {
        if (confirmRouteButton != null)
            confirmRouteButton.onClick.RemoveListener(OnConfirmRouteClicked);

        if (cancelRouteButton != null)
            cancelRouteButton.onClick.RemoveListener(OnCancelRouteClicked);

        if (errorOkButton != null)
            errorOkButton.onClick.RemoveListener(OnErrorOkClicked);
    }

    private IEnumerator InitializePathfinding()
    {
        yield return StartCoroutine(LoadNodesFromJSON(currentMapId));
        yield return StartCoroutine(LoadIndoorData());

        if (pathfinding != null)
        {
            yield return StartCoroutine(pathfinding.LoadGraphDataForMap(currentMapId, currentCampusIds));
        }
    }

    private IEnumerator LoadNodesFromJSON(string mapId)
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadComplete = false;

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

                    Debug.Log($"[ARPathfinding] Loaded {allNodes.Count} nodes");
                    loadComplete = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ARPathfinding] Error loading nodes: {ex.Message}");
                    loadComplete = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"[ARPathfinding] Failed to load nodes: {error}");
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
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

                    Debug.Log($"[ARPathfinding] Loaded {indoorInfrastructures.Count} indoor infrastructures");
                    loadComplete = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ARPathfinding] Error loading indoor data: {ex.Message}");
                    loadComplete = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"[ARPathfinding] Failed to load indoor data: {error}");
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
    }

    public void StartReroute(string fromId, string toId, ARRerouteUIManager.ExemptionType exemption, string exemptedId)
    {
        Debug.Log($"[ARPathfinding] StartReroute called with fromId={fromId}, toId={toId}");

        rerouteFromNodeId = ConvertToNodeId(fromId);
        rerouteToNodeId = ConvertToNodeId(toId);

        Debug.Log($"[ARPathfinding] Converted: fromNodeId={rerouteFromNodeId}, toNodeId={rerouteToNodeId}");

        // VALIDATION 1: Check if FROM node exists
        if (string.IsNullOrEmpty(rerouteFromNodeId))
        {
            ShowError("Starting location not found.");
            return;
        }

        // VALIDATION 2: Check if TO node exists
        if (string.IsNullOrEmpty(rerouteToNodeId))
        {
            ShowError("Destination not found.");
            return;
        }

        // VALIDATION 3: Get the actual node objects
        if (!allNodes.TryGetValue(rerouteFromNodeId, out Node fromNode))
        {
            ShowError("Starting location node data not found.");
            return;
        }

        if (!allNodes.TryGetValue(rerouteToNodeId, out Node toNode))
        {
            ShowError("Destination node data not found.");
            return;
        }

        // VALIDATION 4: Check if FROM is an indoorinfra (not allowed as starting point)
        if (fromNode.type == "indoorinfra")
        {
            ShowError("You cannot start navigation from an indoor room. Please start from a building entrance.");
            return;
        }

        // VALIDATION 5: Check if FROM and TO are the same location
        if (rerouteFromNodeId == rerouteToNodeId)
        {
            ShowError("You are already at this location!");
            return;
        }

        // VALIDATION 6: Check if nodes are active
        if (!fromNode.is_active)
        {
            ShowError($"Starting location '{fromNode.name}' is currently not available.");
            return;
        }

        if (!toNode.is_active)
        {
            ShowError($"Destination '{toNode.name}' is currently not available.");
            return;
        }

        Debug.Log($"[ARPathfinding] ✅ All validations passed");
        Debug.Log($"  FROM: {fromNode.name} (Type: {fromNode.type})");
        Debug.Log($"  TO: {toNode.name} (Type: {toNode.type})");

        exemptionType = exemption;
        exemptedItemId = exemptedId;

        blockedNodes.Clear();
        blockedEdges.Clear();
        if (exemptionType == ARRerouteUIManager.ExemptionType.BuildingsNodes && !string.IsNullOrEmpty(exemptedId))
        {
            string exemptedNodeId = ConvertToNodeId(exemptedId);
            if (!string.IsNullOrEmpty(exemptedNodeId))
            {
                blockedNodes.Add(exemptedNodeId);
                Debug.Log($"[ARPathfinding] Blocking node: {exemptedNodeId}");
            }
        }
        else if (exemptionType == ARRerouteUIManager.ExemptionType.PathsWalkways && !string.IsNullOrEmpty(exemptedId))
        {
            blockedEdges.Add(exemptedId);
            Debug.Log($"[ARPathfinding] Blocking edge: {exemptedId}");
        }

        StartCoroutine(FindAndDisplayRoutes());
    }

    private void ShowError(string message)
    {
        Debug.LogWarning($"[ARPathfinding] Error: {message}");

        if (errorPanel != null && errorMessageText != null)
        {
            errorMessageText.text = message;
            errorPanel.SetActive(true);

            if (routeSelectionBGPanel != null)
            {
                routeSelectionBGPanel.SetActive(true);
            }
        }
    }

    private void OnErrorOkClicked()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }

        if (routeSelectionBGPanel != null)
        {
            routeSelectionBGPanel.SetActive(false);
        }
    }

    private string ConvertToNodeId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (allNodes.ContainsKey(id))
        {
            return id;
        }

        foreach (var node in allNodes.Values)
        {
            if (node.type == "infrastructure" && node.related_infra_id == id)
            {
                return node.node_id;
            }
        }

        foreach (var node in allNodes.Values)
        {
            if (node.type == "indoorinfra" && node.HasRelatedRoomId && node.related_room_id == id)
            {
                return node.node_id;
            }
        }

        Debug.LogWarning($"[ARPathfinding] Could not convert ID to node: {id}");
        return null;
    }

    private IEnumerator FindAndDisplayRoutes()
    {
        if (pathfinding == null)
        {
            Debug.LogError("[ARPathfinding] Pathfinding reference is null!");
            yield break;
        }

        if (!allNodes.ContainsKey(rerouteFromNodeId))
        {
            ShowError("Starting location not found in map data.");
            yield break;
        }

        if (!allNodes.ContainsKey(rerouteToNodeId))
        {
            ShowError("Destination not found in map data.");
            yield break;
        }

        Node fromNode = allNodes[rerouteFromNodeId];
        Node toNode = allNodes[rerouteToNodeId];

        bool fromIsIndoor = IsIndoorNode(rerouteFromNodeId);
        bool toIsIndoor = IsIndoorNode(rerouteToNodeId);

        Debug.Log($"[ARPathfinding] Node Types:");
        Debug.Log($"  FROM: {fromNode.name} (Indoor: {fromIsIndoor})");
        Debug.Log($"  TO: {toNode.name} (Indoor: {toIsIndoor})");

        string pathStartNodeId = rerouteFromNodeId;
        string pathEndNodeId = rerouteToNodeId;

        // ========== CASE 1: OUTDOOR TO INDOOR (SAME BUILDING) ==========
        if (!fromIsIndoor && toIsIndoor)
        {
            string toInfraId = GetInfraIdFromIndoorNode(toNode);
            string fromInfraId = null;

            if (fromNode.type == "infrastructure" && !string.IsNullOrEmpty(fromNode.related_infra_id))
            {
                fromInfraId = fromNode.related_infra_id;
            }

            bool isSameBuilding = !string.IsNullOrEmpty(fromInfraId) &&
                                !string.IsNullOrEmpty(toInfraId) &&
                                fromInfraId == toInfraId;

            Debug.Log($"[ARPathfinding] Same Building Check:");
            Debug.Log($"  FROM infra_id: {fromInfraId}");
            Debug.Log($"  TO infra_id: {toInfraId}");
            Debug.Log($"  Same Building: {isSameBuilding}");

            if (isSameBuilding)
            {
                Debug.Log($"[ARPathfinding] ✅ CASE 1: Indoor navigation (same building)");

                var singleNodeRoute = CreateSameBuildingRoute(fromNode.node_id, fromNode, toNode);

                PlayerPrefs.SetString("ARNavigation_OriginalFromNodeId", rerouteFromNodeId);
                PlayerPrefs.SetString("ARNavigation_OriginalToNodeId", rerouteToNodeId);
                PlayerPrefs.SetInt("ARNavigation_FromIsIndoor", 0);
                PlayerPrefs.SetInt("ARNavigation_ToIsIndoor", 1);
                PlayerPrefs.SetString("ARNavigation_SameBuilding", "true");
                PlayerPrefs.Save();

                currentRoutes = new List<RouteData> { singleNodeRoute };
                DisplayAllRoutes();
                yield break;
            }
        }

        // ========== CASE 2: OUTDOOR TO INDOOR (DIFFERENT BUILDING) ==========
        if (toIsIndoor)
        {
            Debug.Log($"[ARPathfinding] ✅ CASE 2: Outdoor to indoor (different building)");

            string toInfraId = GetInfraIdFromIndoorNode(toNode);
            if (!string.IsNullOrEmpty(toInfraId))
            {
                Node entranceNode = allNodes.Values.FirstOrDefault(n =>
                    n.is_active &&
                    n.type == "infrastructure" &&
                    n.related_infra_id == toInfraId);

                if (entranceNode != null)
                {
                    pathEndNodeId = entranceNode.node_id;
                    Debug.Log($"[ARPathfinding] Mapped indoor destination to entrance: {entranceNode.node_id} ({entranceNode.name})");
                }
                else
                {
                    ShowError($"Cannot find an active entrance for '{toNode.name}'");
                    yield break;
                }
            }
        }

        // ========== CASE 3: OUTDOOR TO OUTDOOR ==========
        Debug.Log($"[ARPathfinding] ✅ CASE 3: Standard outdoor pathfinding");
        Debug.Log($"  Pathfinding from: {pathStartNodeId} to {pathEndNodeId}");

        yield return StartCoroutine(pathfinding.FindMultiplePathsWithBlocking(
            pathStartNodeId,
            pathEndNodeId,
            3,
            blockedNodes,
            blockedEdges
        ));

        var routes = pathfinding.GetAllRoutes();

        if (routes == null || routes.Count == 0)
        {
            ShowError("No alternative path found. The selected route may be the only available option.");
            yield break;
        }

        PlayerPrefs.SetString("ARNavigation_OriginalFromNodeId", rerouteFromNodeId);
        PlayerPrefs.SetString("ARNavigation_OriginalToNodeId", rerouteToNodeId);
        PlayerPrefs.SetInt("ARNavigation_FromIsIndoor", fromIsIndoor ? 1 : 0);
        PlayerPrefs.SetInt("ARNavigation_ToIsIndoor", toIsIndoor ? 1 : 0);
        PlayerPrefs.SetString("ARNavigation_SameBuilding", "false");
        PlayerPrefs.Save();

        currentRoutes = routes;
        DisplayAllRoutes();
    }

    private bool IsIndoorNode(string nodeId)
    {
        if (allNodes.TryGetValue(nodeId, out Node node))
        {
            return node.type == "indoorinfra";
        }
        return false;
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

        Debug.LogWarning($"[ARPathfinding] Room not found in indoor data: {indoorNode.related_room_id}");
        return null;
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
        if (routeSelectionPanel != null)
        {
            routeSelectionPanel.SetActive(true);
        }

        if (routeSelectionBGPanel != null)
        {
            routeSelectionBGPanel.SetActive(true);
        }

        ClearRouteItems();

        if (currentRoutes.Count == 0)
        {
            Debug.LogWarning("[ARPathfinding] No routes to display");
            return;
        }

        var firstRoute = currentRoutes[0];

        Node displayFromNode = firstRoute.startNode;
        Node displayToNode = firstRoute.endNode;
        if (!string.IsNullOrEmpty(rerouteFromNodeId) && allNodes.ContainsKey(rerouteFromNodeId))
        {
            displayFromNode = allNodes[rerouteFromNodeId];
        }

        if (!string.IsNullOrEmpty(rerouteToNodeId) && allNodes.ContainsKey(rerouteToNodeId))
        {
            displayToNode = allNodes[rerouteToNodeId];
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

        Debug.Log($"[ARPathfinding] Displayed {currentRoutes.Count} routes");
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

        StartCoroutine(GenerateAndApplyReroute(directionGen, selectedRoute));
    }

    private void OnCancelRouteClicked()
    {
        if (routeSelectionPanel != null)
        {
            routeSelectionPanel.SetActive(false);
        }

        if (routeSelectionBGPanel != null)
        {
            routeSelectionBGPanel.SetActive(false);
        }

        ClearRouteItems();
    }

    private IEnumerator GenerateAndApplyReroute(DirectionGenerator directionGen, RouteData selectedRoute)
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
            Debug.LogError("[ARPathfinding] Direction generator failed to load data");
            yield break;
        }

        List<NavigationDirection> directions = directionGen.GenerateDirections(selectedRoute);

        if (directions == null || directions.Count == 0)
        {
            Debug.LogError("[ARPathfinding] Failed to generate directions");
            yield break;
        }

        Debug.Log($"[ARPathfinding] Generated {directions.Count} directions");

        SaveRouteDataForAR(selectedRoute, directions);

        if (routeSelectionPanel != null)
        {
            routeSelectionPanel.SetActive(false);
        }

        if (routeSelectionBGPanel != null)
        {
            routeSelectionBGPanel.SetActive(false);
        }

        if (arLoadingManager != null)
        {
            arLoadingManager.ShowLoadingPanel("Updating route...");
        }

        yield return new WaitForSeconds(0.5f);
        ARSceneUpdateManager updateManager = FindObjectOfType<ARSceneUpdateManager>();
        if (updateManager != null && updateManager.gameObject.activeInHierarchy)
        {
            updateManager.StartRerouteUpdate();
        }
        else
        {
            Debug.LogError("[ARPathfinding] ARSceneUpdateManager not found or inactive!");
            if (arLoadingManager != null)
            {
                arLoadingManager.HideLoadingPanel();
            }
        }
    }

    private void SaveRouteDataForAR(RouteData route, List<NavigationDirection> directions)
    {
        Debug.Log("=============== SAVE REROUTE DATA START ===============");

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

        Debug.Log($"✅ Saved {directions.Count} directions");

        ARModeHelper.EnableARMode();
        PlayerPrefs.Save();

        Debug.Log("=============== SAVE REROUTE DATA COMPLETE ===============");
    }
}