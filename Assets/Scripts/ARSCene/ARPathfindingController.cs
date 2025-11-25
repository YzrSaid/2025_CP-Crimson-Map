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

        if (routeSelectionPanel != null)
        {
            routeSelectionPanel.SetActive(false);
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
    }

    public void StartReroute(string fromId, string toId, ARRerouteUIManager.ExemptionType exemption, string exemptedId)
    {
        rerouteFromNodeId = ConvertToNodeId(fromId);
        rerouteToNodeId = ConvertToNodeId(toId);

        if (string.IsNullOrEmpty(rerouteFromNodeId))
        {
            return;
        }

        if (string.IsNullOrEmpty(rerouteToNodeId))
        {
            return;
        }

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
            }
        }
        else if (exemptionType == ARRerouteUIManager.ExemptionType.PathsWalkways && !string.IsNullOrEmpty(exemptedId))
        {
            blockedEdges.Add(exemptedId);
        }

        StartCoroutine(FindAndDisplayRoutes());
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

        return null;
    }

    private IEnumerator FindAndDisplayRoutes()
    {
        if (pathfinding == null)
        {
            yield break;
        }

        if (!allNodes.ContainsKey(rerouteFromNodeId))
        {
            yield break;
        }

        if (!allNodes.ContainsKey(rerouteToNodeId))
        {
            yield break;
        }

        bool fromIsIndoor = IsIndoorNode(rerouteFromNodeId);
        bool toIsIndoor = IsIndoorNode(rerouteToNodeId);

        string pathStartNodeId = rerouteFromNodeId;
        string pathEndNodeId = rerouteToNodeId;

        Node fromNode = allNodes[rerouteFromNodeId];
        Node toNode = allNodes[rerouteToNodeId];

        if (fromIsIndoor && allNodes.TryGetValue(rerouteFromNodeId, out Node fromIndoorNode))
        {
            Node entranceNode = GetBuildingEntranceNode(fromIndoorNode);
            if (entranceNode != null)
            {
                pathStartNodeId = entranceNode.node_id;
            }
        }

        if (toIsIndoor && allNodes.TryGetValue(rerouteToNodeId, out Node toIndoorNode))
        {
            Node entranceNode = GetBuildingEntranceNode(toIndoorNode);
            if (entranceNode != null)
            {
                pathEndNodeId = entranceNode.node_id;
            }
        }

        bool isSameBuilding = pathStartNodeId == pathEndNodeId;

        if (isSameBuilding)
        {
            var singleNodeRoute = CreateSameBuildingRoute(pathStartNodeId, fromNode, toNode);

            PlayerPrefs.SetString("ARNavigation_OriginalFromNodeId", rerouteFromNodeId);
            PlayerPrefs.SetString("ARNavigation_OriginalToNodeId", rerouteToNodeId);
            PlayerPrefs.SetInt("ARNavigation_FromIsIndoor", fromIsIndoor ? 1 : 0);
            PlayerPrefs.SetInt("ARNavigation_ToIsIndoor", toIsIndoor ? 1 : 0);
            PlayerPrefs.SetString("ARNavigation_SameBuilding", "true");
            PlayerPrefs.Save();

            currentRoutes = new List<RouteData> { singleNodeRoute };
            DisplayAllRoutes();
            yield break;
        }

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

    private Node GetBuildingEntranceNode(Node indoorNode)
    {
        if (indoorNode.type != "indoorinfra" || string.IsNullOrEmpty(indoorNode.related_infra_id))
        {
            return null;
        }

        foreach (var node in allNodes.Values)
        {
            if (node.type == "infrastructure" && node.related_infra_id == indoorNode.related_infra_id)
            {
                return node;
            }
        }

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
            formattedDistance = "Already at building",
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
                string buildingName = GetBuildingNameFromInfraId(displayToNode.related_infra_id);
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
            yield break;
        }

        List<NavigationDirection> directions = directionGen.GenerateDirections(selectedRoute);

        if (directions == null || directions.Count == 0)
        {
            yield break;
        }

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
        if (updateManager != null)
        {
            yield return StartCoroutine(updateManager.ApplyReroute());
        }

        if (arLoadingManager != null)
        {
            yield return new WaitForSeconds(1f);
            arLoadingManager.gameObject.SetActive(false);
        }
    }

    private void SaveRouteDataForAR(RouteData route, List<NavigationDirection> directions)
    {
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

        PlayerPrefs.SetInt("ARNavigation_PathNodeCount", route.path.Count);

        for (int i = 0; i < route.path.Count; i++)
        {
            PlayerPrefs.SetString($"ARNavigation_PathNode_{i}", route.path[i].node.node_id);
        }

        int edgeCount = route.path.Count - 1;
        PlayerPrefs.SetInt("ARNavigation_EdgeCount", edgeCount);

        for (int i = 0; i < edgeCount; i++)
        {
            string fromNode = route.path[i].node.node_id;
            string toNode = route.path[i + 1].node.node_id;

            PlayerPrefs.SetString($"ARNavigation_Edge_{i}_From", fromNode);
            PlayerPrefs.SetString($"ARNavigation_Edge_{i}_To", toNode);
        }

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

        PlayerPrefs.SetString("ARMode", "Navigation");
        PlayerPrefs.Save();
    }
}