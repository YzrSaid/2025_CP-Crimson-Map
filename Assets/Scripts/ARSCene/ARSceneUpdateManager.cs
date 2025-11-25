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
        currentMapId = PlayerPrefs.GetString("ARScene_MapId");
        string campusIdsStr = PlayerPrefs.GetString("ARScene_CampusIds", "");
        currentCampusIds = string.IsNullOrEmpty(campusIdsStr)
            ? new List<string>()
            : new List<string>(campusIdsStr.Split(','));
    }

    public IEnumerator ApplyReroute()
    {
        yield return StartCoroutine(UpdateDirectionDisplay());

        yield return StartCoroutine(UpdateARMapHighlighting());

        yield return StartCoroutine(UpdateNavigationMarkers());

        UpdateAffectedItemPopulators();

        yield return new WaitForSeconds(0.3f);

        if (arLoadingManager != null)
            arLoadingManager.HideLoadingPanel();
    }

    private IEnumerator UpdateDirectionDisplay()
    {
        if (directionDisplayManager == null)
        {
            yield break;
        }

        directionDisplayManager.ReloadDirections();

        float timeout = 5f;
        float elapsed = 0f;
        int previousCount = 0;

        while (elapsed < timeout)
        {
            int currentCount = directionDisplayManager.GetAllDirections().Count;

            if (currentCount > 0 && currentCount != previousCount)
            {
                break;
            }

            previousCount = currentCount;
            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator UpdateARMapHighlighting()
    {
        if (arMapManager == null)
        {
            yield break;
        }

        arMapManager.ClearNavigationHighlights();
        yield return new WaitForSeconds(0.2f);

        yield return StartCoroutine(ReconstructAndApplyRoute());
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
    }

    private IEnumerator BuildRouteFromPlayerPrefs(System.Action<RouteData> callback)
    {
        int pathNodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        if (pathNodeCount == 0)
        {
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
            yield break;
        }

        navigationMarkerSpawner.ReloadPathNodes();
        yield return new WaitForSeconds(0.5f);
    }

    private void UpdateAffectedItemPopulators()
    {
        if (reportAffectedItemPopulator != null)
        {
            reportAffectedItemPopulator.ReloadNavigationData();
        }

        if (rerouteAffectedItemPopulator != null)
        {
            rerouteAffectedItemPopulator.enabled = false;
            rerouteAffectedItemPopulator.enabled = true;
        }
    }
}