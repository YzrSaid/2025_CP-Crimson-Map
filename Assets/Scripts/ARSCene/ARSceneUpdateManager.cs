using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ARSceneUpdateManager : MonoBehaviour
{
    [Header("AR Scene Components")]
    public DirectionDisplayManager directionDisplayManager;
    public ARMapManager arMapManager;
    public UnifiedARNavigationMarkerSpawner navigationMarkerSpawner;
    public ReportAffectedItemPopulator reportAffectedItemPopulator;
    public ARRerouteAffectedItemPopulator rerouteAffectedItemPopulator;

    [Header("Loading")]
    public ARLoadingManager arLoadingManager;

    private string currentMapId;
    private List<string> currentCampusIds;

    void Start()
    {
        FindReferences();
        LoadMapData();
    }

    private void FindReferences()
    {
        if (directionDisplayManager == null)
            directionDisplayManager = FindObjectOfType<DirectionDisplayManager>();

        if (arMapManager == null)
            arMapManager = FindObjectOfType<ARMapManager>();

        if (navigationMarkerSpawner == null)
            navigationMarkerSpawner = FindObjectOfType<UnifiedARNavigationMarkerSpawner>();

        if (reportAffectedItemPopulator == null)
            reportAffectedItemPopulator = FindObjectOfType<ReportAffectedItemPopulator>();

        if (rerouteAffectedItemPopulator == null)
            rerouteAffectedItemPopulator = FindObjectOfType<ARRerouteAffectedItemPopulator>();

        if (arLoadingManager == null)
            arLoadingManager = FindObjectOfType<ARLoadingManager>();
    }

    private void LoadMapData()
    {
        currentMapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        string campusIdsStr = PlayerPrefs.GetString("ARScene_CampusIds", "");
        currentCampusIds = string.IsNullOrEmpty(campusIdsStr)
            ? new List<string>()
            : new List<string>(campusIdsStr.Split(','));
    }

    public IEnumerator ApplyReroute()
    {
        Debug.Log("[ARSceneUpdateManager] ========== STARTING REROUTE APPLICATION ==========");

        // STEP 1: Update Directions
        if (arLoadingManager != null)
        {
            arLoadingManager.ShowLoadingPanel("Updating directions...");
        }
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(UpdateDirectionDisplay());

        // STEP 2: Update AR Map Highlighting
        if (arLoadingManager != null)
        {
            arLoadingManager.ShowLoadingPanel("Updating map highlights...");
        }
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(UpdateARMapHighlighting());

        // STEP 3: Update Navigation Markers
        if (arLoadingManager != null)
        {
            arLoadingManager.ShowLoadingPanel("Updating navigation markers...");
        }
        yield return new WaitForSeconds(0.3f);
        yield return StartCoroutine(UpdateNavigationMarkers());

        // STEP 4: Refresh Affected Item Populators
        if (arLoadingManager != null)
        {
            arLoadingManager.ShowLoadingPanel("Refreshing route data...");
        }
        yield return new WaitForSeconds(0.3f);
        UpdateAffectedItemPopulators();

        yield return new WaitForSeconds(0.5f);

        Debug.Log("[ARSceneUpdateManager] ========== REROUTE APPLICATION COMPLETE ==========");
    }

    private IEnumerator UpdateDirectionDisplay()
    {
        if (directionDisplayManager == null)
        {
            Debug.LogWarning("[ARSceneUpdateManager] DirectionDisplayManager not found!");
            yield break;
        }

        Debug.Log("[ARSceneUpdateManager] → Reloading directions...");

        directionDisplayManager.ResetNavigation();
        yield return new WaitForSeconds(0.2f);

        directionDisplayManager.ReloadDirections();
        yield return new WaitForSeconds(0.5f);

        Debug.Log("[ARSceneUpdateManager] ✅ Directions updated!");
    }

    private IEnumerator UpdateARMapHighlighting()
    {
        if (arMapManager == null)
        {
            Debug.LogWarning("[ARSceneUpdateManager] ARMapManager not found!");
            yield break;
        }

        Debug.Log("[ARSceneUpdateManager] → Clearing old highlights...");
        arMapManager.ClearNavigationHighlights();
        yield return new WaitForSeconds(0.2f);

        Debug.Log("[ARSceneUpdateManager] → Reconstructing route from PlayerPrefs...");
        yield return StartCoroutine(ReconstructAndApplyRoute());

        Debug.Log("[ARSceneUpdateManager] ✅ AR map highlighting updated!");
    }

    private IEnumerator ReconstructAndApplyRoute()
    {
        RouteData newRoute = null;
        yield return StartCoroutine(BuildRouteFromPlayerPrefs((route) => newRoute = route));

        if (newRoute != null && arMapManager != null)
        {
            arMapManager.InitializeARNavigation(currentMapId, currentCampusIds, newRoute);
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            Debug.LogWarning("[ARSceneUpdateManager] Failed to reconstruct route!");
        }
    }

    private IEnumerator BuildRouteFromPlayerPrefs(System.Action<RouteData> callback)
    {
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        if (pathNodeCount == 0)
        {
            Debug.LogWarning("[ARSceneUpdateManager] No path nodes in PlayerPrefs!");
            callback?.Invoke(null);
            yield break;
        }

        string startNodeId = PlayerPrefs.GetString("ARNavigation_StartNodeId", "");
        string endNodeId = PlayerPrefs.GetString("ARNavigation_EndNodeId", "");

        List<string> pathNodeIds = new List<string>();
        for (int i = 0; i < pathNodeCount; i++)
        {
            string nodeId = PlayerPrefs.GetString($"ARNavigation_PathNode_{i}", "");
            if (!string.IsNullOrEmpty(nodeId))
                pathNodeIds.Add(nodeId);
        }

        string fileName = $"nodes_{currentMapId}.json";
        Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    foreach (var node in nodes)
                    {
                        if (node != null && node.is_active)
                            allNodes[node.node_id] = node;
                    }
                    loadComplete = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ARSceneUpdateManager] Error loading nodes: {e.Message}");
                    loadComplete = true;
                }
            },
            (error) =>
            {
                Debug.LogError($"[ARSceneUpdateManager] Failed to load nodes: {error}");
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);

        if (!allNodes.ContainsKey(startNodeId) || !allNodes.ContainsKey(endNodeId))
        {
            callback?.Invoke(null);
            yield break;
        }

        List<PathNode> pathNodes = new List<PathNode>();
        foreach (string nodeId in pathNodeIds)
        {
            if (allNodes.TryGetValue(nodeId, out Node node))
            {
                pathNodes.Add(new PathNode
                {
                    node = node,
                    worldPosition = Vector3.zero,
                    isStart = nodeId == startNodeId,
                    isEnd = nodeId == endNodeId,
                    distanceToNext = 0f
                });
            }
        }

        RouteData route = new RouteData
        {
            path = pathNodes,
            startNode = allNodes[startNodeId],
            endNode = allNodes[endNodeId],
            totalDistance = PlayerPrefs.GetFloat("ARNavigation_TotalDistance", 0f),
            formattedDistance = PlayerPrefs.GetString("ARNavigation_FormattedDistance", ""),
            walkingTime = PlayerPrefs.GetString("ARNavigation_WalkingTime", ""),
            viaMode = PlayerPrefs.GetString("ARNavigation_ViaMode", "")
        };

        callback?.Invoke(route);
    }

    private IEnumerator UpdateNavigationMarkers()
    {
        if (navigationMarkerSpawner == null)
        {
            Debug.LogWarning("[ARSceneUpdateManager] NavigationMarkerSpawner not found!");
            yield break;
        }

        Debug.Log("[ARSceneUpdateManager] → Reloading navigation markers...");

        navigationMarkerSpawner.ReloadPathNodes();
        yield return new WaitForSeconds(0.5f);

        Debug.Log("[ARSceneUpdateManager] ✅ Navigation markers updated!");
    }

    private void UpdateAffectedItemPopulators()
    {
        Debug.Log("[ARSceneUpdateManager] → Refreshing affected item populators...");

        if (reportAffectedItemPopulator != null)
        {
            reportAffectedItemPopulator.ReloadNavigationData();
        }

        if (rerouteAffectedItemPopulator != null)
        {
            rerouteAffectedItemPopulator.enabled = false;
            rerouteAffectedItemPopulator.enabled = true;
        }

        Debug.Log("[ARSceneUpdateManager] ✅ Affected item populators refreshed!");
    }
}