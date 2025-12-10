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
    public UnifiedARManager unifiedARManager;

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

        if (unifiedARManager == null)
            unifiedARManager = FindObjectOfType<UnifiedARManager>();
    }

    private void LoadMapData()
    {
        currentMapId = PlayerPrefs.GetString("ARScene_MapId");
        string campusIdsStr = PlayerPrefs.GetString("ARScene_CampusIds", "");
        currentCampusIds = string.IsNullOrEmpty(campusIdsStr)
            ? new List<string>()
            : new List<string>(campusIdsStr.Split(','));

        Debug.Log($"[ARSceneUpdateManager] Loaded map data: {currentMapId}");
    }

    public void StartRerouteUpdate()
    {
        Debug.Log("[ARSceneUpdateManager] Starting reroute update...");
        
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[ARSceneUpdateManager] GameObject was inactive, activating...");
            gameObject.SetActive(true);
        }

        StopAllCoroutines();
        StartCoroutine(ApplyRerouteCoroutine());
    }

    private IEnumerator ApplyRerouteCoroutine()
    {
        Debug.Log("[ARSceneUpdateManager] ========== REROUTE UPDATE START ==========");

        UpdateTopPanelUI();
        Debug.Log("[ARSceneUpdateManager] ✅ Top panel UI updated");

        yield return StartCoroutine(UpdateDirectionDisplay());
        Debug.Log("[ARSceneUpdateManager] ✅ Direction display updated");

        yield return StartCoroutine(UpdateARMapHighlighting());
        Debug.Log("[ARSceneUpdateManager] ✅ AR map highlighting updated");

        yield return StartCoroutine(UpdateNavigationMarkers());
        Debug.Log("[ARSceneUpdateManager] ✅ Navigation markers updated");

        UpdateAffectedItemPopulators();
        Debug.Log("[ARSceneUpdateManager] ✅ Affected item populators updated");

        yield return new WaitForSeconds(1f);

        if (arLoadingManager != null)
        {
            arLoadingManager.HideLoadingPanel();
            Debug.Log("[ARSceneUpdateManager] ✅ Loading panel hidden");
        }

        Debug.Log("[ARSceneUpdateManager] ========== REROUTE UPDATE COMPLETE ==========");
    }

    private void UpdateTopPanelUI()
    {
        if (unifiedARManager != null)
        {
            Debug.Log("[ARSceneUpdateManager] Reloading navigation data in UnifiedARManager...");
            unifiedARManager.ReloadNavigationData();
        }
        else
        {
            Debug.LogWarning("[ARSceneUpdateManager] UnifiedARManager not found!");
        }
    }

    public IEnumerator ApplyReroute()
    {
        Debug.LogWarning("[ARSceneUpdateManager] ApplyReroute() is deprecated. Use StartRerouteUpdate() instead.");
        StartRerouteUpdate();
        yield return null;
    }

    private IEnumerator UpdateDirectionDisplay()
    {
        if (directionDisplayManager == null)
        {
            Debug.LogWarning("[ARSceneUpdateManager] DirectionDisplayManager not found");
            yield break;
        }

        Debug.Log("[ARSceneUpdateManager] Reloading directions...");
        directionDisplayManager.ReloadDirections();

        float timeout = 5f;
        float elapsed = 0f;
        int previousCount = 0;

        while (elapsed < timeout)
        {
            int currentCount = directionDisplayManager.GetAllDirections().Count;

            if (currentCount > 0 && currentCount != previousCount)
            {
                Debug.Log($"[ARSceneUpdateManager] Directions loaded: {currentCount}");
                break;
            }

            previousCount = currentCount;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
        {
            Debug.LogWarning("[ARSceneUpdateManager] Direction loading timed out");
        }

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator UpdateARMapHighlighting()
    {
        if (arMapManager == null)
        {
            Debug.LogWarning("[ARSceneUpdateManager] ARMapManager not found");
            yield break;
        }

        Debug.Log("[ARSceneUpdateManager] Clearing navigation highlights...");
        arMapManager.ClearNavigationHighlights();
        yield return new WaitForSeconds(0.2f);

        yield return StartCoroutine(ReconstructAndApplyRoute());
    }

    private IEnumerator ReconstructAndApplyRoute()
    {
        Debug.Log("[ARSceneUpdateManager] Reconstructing route from PlayerPrefs...");
        
        RouteData newRoute = null;
        yield return StartCoroutine(BuildRouteFromPlayerPrefs((route) => newRoute = route));

        if (newRoute != null && arMapManager != null)
        {
            Debug.Log($"[ARSceneUpdateManager] Applying new route: {newRoute.startNode.name} → {newRoute.endNode.name}");
            arMapManager.InitializeARNavigation(currentMapId, currentCampusIds, newRoute);
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            Debug.LogWarning("[ARSceneUpdateManager] Failed to reconstruct route or ARMapManager is null");
        }
    }

    private IEnumerator BuildRouteFromPlayerPrefs(System.Action<RouteData> callback)
    {
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        Debug.Log($"[ARSceneUpdateManager] Building route with {pathNodeCount} nodes");

        if (pathNodeCount == 0)
        {
            Debug.LogWarning("[ARSceneUpdateManager] No path nodes found in PlayerPrefs");
            callback?.Invoke(null);
            yield break;
        }

        string startNodeId = PlayerPrefs.GetString("ARNavigation_StartNodeId", "");
        string endNodeId = PlayerPrefs.GetString("ARNavigation_EndNodeId", "");

        Debug.Log($"[ARSceneUpdateManager] Route: {startNodeId} → {endNodeId}");

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
                    Debug.Log($"[ARSceneUpdateManager] Loaded {allNodes.Count} nodes from {fileName}");
                    loadComplete = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ARSceneUpdateManager] Error loading nodes: {ex.Message}");
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
            Debug.LogError($"[ARSceneUpdateManager] Start or end node not found in loaded nodes");
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

        Debug.Log($"[ARSceneUpdateManager] ✅ Route reconstructed with {pathNodes.Count} path nodes");

        callback?.Invoke(route);
    }

    private IEnumerator UpdateNavigationMarkers()
    {
        if (navigationMarkerSpawner == null)
        {
            Debug.LogWarning("[ARSceneUpdateManager] NavigationMarkerSpawner not found");
            yield break;
        }

        Debug.Log("[ARSceneUpdateManager] Reloading navigation markers...");
        navigationMarkerSpawner.ReloadPathNodes();
        yield return new WaitForSeconds(0.5f);
    }

    private void UpdateAffectedItemPopulators()
    {
        if (reportAffectedItemPopulator != null)
        {
            Debug.Log("[ARSceneUpdateManager] Reloading report affected item populator...");
            reportAffectedItemPopulator.ReloadNavigationData();
        }

        if (rerouteAffectedItemPopulator != null)
        {
            Debug.Log("[ARSceneUpdateManager] Refreshing reroute affected item populator...");
            rerouteAffectedItemPopulator.enabled = false;
            rerouteAffectedItemPopulator.enabled = true;
        }
    }
}