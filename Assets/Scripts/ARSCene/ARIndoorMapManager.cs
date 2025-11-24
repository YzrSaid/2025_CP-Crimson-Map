using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ARIndoorMapManager : MonoBehaviour
{
    [Header("Indoor Map UI")]
    public RectTransform indoorMapContainer;
    public RawImage indoorMapBackground;
    public Transform markersContainer;

    [Header("Marker Prefabs")]
    public GameObject roomMarkerPrefab;
    public GameObject stairsMarkerPrefab;
    public GameObject fireExitMarkerPrefab;
    public GameObject entranceMarkerPrefab;
    public GameObject destinationRoomMarkerPrefab;

    [Header("Edge Prefabs")]
    public GameObject indoorEdgePrefab;
    public GameObject highlightedEdgePrefab;

    [Header("Map Settings")]
    public float mapWidth = 1000f;
    public float mapHeight = 1000f;
    public float pixelsPerMeter = 20f;
    public Color backgroundColor = Color.white;

    [Header("Edge Settings")]
    public float edgeWidth = 2f;
    public Color normalEdgeColor = Color.gray;
    public Color highlightEdgeColor = new Color(0.74f, 0.06f, 0.18f, 1f);

    [Header("Pan & Zoom Settings")]
    public float minZoom = 0.5f;
    public float maxZoom = 3f;
    public float zoomSpeed = 0.1f;

    private string currentInfraId;
    private Node currentInfraNode;
    private string destinationNodeId;
    private int currentFloor = 1;
    private List<int> availableFloors = new List<int>();

    private Dictionary<string, GameObject> spawnedMarkers = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> spawnedEdges = new Dictionary<string, GameObject>();
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, IndoorInfrastructure> indoorInfrastructures = new Dictionary<string, IndoorInfrastructure>();
    private Dictionary<string, IndoorEdge> indoorEdges = new Dictionary<string, IndoorEdge>();
    private HashSet<string> highlightedEdgeIds = new HashSet<string>();

    private Vector2 dragStartPos;
    private Vector2 mapStartPos;
    private bool isDragging = false;

    void Start()
    {
        if (indoorMapBackground != null)
        {
            Texture2D whiteTex = new Texture2D(1, 1);
            whiteTex.SetPixel(0, 0, backgroundColor);
            whiteTex.Apply();
            indoorMapBackground.texture = whiteTex;
        }

        destinationNodeId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");
    }

    public void LoadIndoorMap(string infraId, Node infraNode)
    {
        currentInfraId = infraId;
        currentInfraNode = infraNode;

        ClearAllMarkers();
        ClearAllEdges();

        StartCoroutine(LoadIndoorMapData());
    }

    private IEnumerator LoadIndoorMapData()
    {
        string mapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        yield return StartCoroutine(LoadNodes(mapId));

        yield return StartCoroutine(LoadIndoorData());
        yield return StartCoroutine(LoadIndoorEdges());

        DetermineAvailableFloors();

        if (availableFloors.Count > 0)
        {
            currentFloor = availableFloors[0];
        }

        CalculateHighlightedPath();
        SpawnEdgesForCurrentFloor();
        SpawnMarkersForCurrentFloor();

        if (ARMapModeController.Instance != null)
        {
            ARMapModeController.Instance.UpdateFloorIndicator(currentFloor);
        }
    }

    private IEnumerator LoadNodes(string mapId)
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
                    allNodes.Clear();

                    foreach (var node in nodes)
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

    private IEnumerator LoadIndoorEdges()
    {
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor_edges.json",
            (jsonContent) =>
            {
                try
                {
                    IndoorEdge[] edgesArray = JsonHelper.FromJson<IndoorEdge>(jsonContent);
                    indoorEdges.Clear();

                    foreach (var edge in edgesArray)
                    {
                        if (!edge.is_deleted && edge.is_active)
                        {
                            indoorEdges[edge.indooredge_id] = edge;
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

    private void CalculateHighlightedPath()
    {
        highlightedEdgeIds.Clear();

        if (string.IsNullOrEmpty(destinationNodeId) || !allNodes.ContainsKey(destinationNodeId))
            return;

        Node destNode = allNodes[destinationNodeId];
        if (destNode.type != "indoorinfra" || !destNode.HasRelatedRoomId)
            return;

        string destRoomId = destNode.related_room_id;
        if (!indoorInfrastructures.ContainsKey(destRoomId))
            return;

        int destFloor = 1;
        if (destNode.indoor != null && !string.IsNullOrEmpty(destNode.indoor.floor))
        {
            int.TryParse(destNode.indoor.floor, out destFloor);
        }

        string startRoomId = GetStartingPoint(destFloor);
        if (string.IsNullOrEmpty(startRoomId))
            return;

        List<string> path = FindPath(startRoomId, destRoomId);

        if (path != null && path.Count > 1)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                string fromRoom = path[i];
                string toRoom = path[i + 1];

                var edge = indoorEdges.Values.FirstOrDefault(e =>
                    e.infra_id == currentInfraId &&
                    ((e.from_indoor == fromRoom && e.to_indoor == toRoom) ||
                     (e.from_indoor == toRoom && e.to_indoor == fromRoom))
                );

                if (edge != null)
                {
                    highlightedEdgeIds.Add(edge.indooredge_id);
                }
            }
        }
    }

    private string GetStartingPoint(int destFloor)
    {
        if (destFloor == 1)
        {
            var entrance = indoorInfrastructures.Values.FirstOrDefault(i =>
                i.infra_id == currentInfraId &&
                i.indoor_type.ToLower() == "entrance"
            );

            if (entrance != null)
                return entrance.room_id;
        }

        var stairs = indoorInfrastructures.Values.FirstOrDefault(i =>
            i.infra_id == currentInfraId &&
            i.indoor_type.ToLower() == "stairs"
        );

        if (stairs != null)
        {
            var stairsNode = allNodes.Values.FirstOrDefault(n =>
                n.type == "indoorinfra" &&
                n.HasRelatedRoomId &&
                n.related_room_id == stairs.room_id &&
                n.indoor != null &&
                n.indoor.floor == destFloor.ToString()
            );

            if (stairsNode != null)
                return stairs.room_id;
        }

        return null;
    }

    private List<string> FindPath(string startRoomId, string destRoomId)
    {
        Dictionary<string, List<string>> graph = BuildGraph();

        if (!graph.ContainsKey(startRoomId) || !graph.ContainsKey(destRoomId))
            return null;

        Queue<string> queue = new Queue<string>();
        Dictionary<string, string> cameFrom = new Dictionary<string, string>();
        HashSet<string> visited = new HashSet<string>();

        queue.Enqueue(startRoomId);
        visited.Add(startRoomId);
        cameFrom[startRoomId] = null;

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();

            if (current == destRoomId)
            {
                return ReconstructPath(cameFrom, current);
            }

            if (graph.ContainsKey(current))
            {
                foreach (string neighbor in graph[current])
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        cameFrom[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return null;
    }

    private Dictionary<string, List<string>> BuildGraph()
    {
        Dictionary<string, List<string>> graph = new Dictionary<string, List<string>>();

        foreach (var edge in indoorEdges.Values)
        {
            if (edge.infra_id != currentInfraId)
                continue;

            if (!graph.ContainsKey(edge.from_indoor))
                graph[edge.from_indoor] = new List<string>();

            if (!graph.ContainsKey(edge.to_indoor))
                graph[edge.to_indoor] = new List<string>();

            graph[edge.from_indoor].Add(edge.to_indoor);
            graph[edge.to_indoor].Add(edge.from_indoor);
        }

        return graph;
    }

    private List<string> ReconstructPath(Dictionary<string, string> cameFrom, string current)
    {
        List<string> path = new List<string>();
        path.Add(current);

        while (cameFrom[current] != null)
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private void DetermineAvailableFloors()
    {
        availableFloors.Clear();

        var indoorNodes = allNodes.Values.Where(n =>
            n.type == "indoorinfra" &&
            n.HasRelatedRoomId &&
            indoorInfrastructures.ContainsKey(n.related_room_id) &&
            indoorInfrastructures[n.related_room_id].infra_id == currentInfraId &&
            n.indoor != null &&
            !string.IsNullOrEmpty(n.indoor.floor)
        );

        foreach (var node in indoorNodes)
        {
            if (int.TryParse(node.indoor.floor, out int floor))
            {
                if (!availableFloors.Contains(floor))
                {
                    availableFloors.Add(floor);
                }
            }
        }

        availableFloors.Sort();
    }

    private void SpawnMarkersForCurrentFloor()
    {
        ClearAllMarkers();

        var indoorNodes = allNodes.Values.Where(n =>
            n.type == "indoorinfra" &&
            n.HasRelatedRoomId &&
            indoorInfrastructures.ContainsKey(n.related_room_id) &&
            indoorInfrastructures[n.related_room_id].infra_id == currentInfraId &&
            indoorInfrastructures[n.related_room_id].indoor_type.ToLower() != "indoorinter" && 
            n.indoor != null &&
            n.indoor.floor == currentFloor.ToString()
        );

        foreach (var node in indoorNodes)
        {
            SpawnMarkerForNode(node);
        }
    }

    private void SpawnEdgesForCurrentFloor()
    {
        ClearAllEdges();

        if (indoorEdgePrefab == null)
            return;

        var currentFloorNodes = allNodes.Values.Where(n =>
            n.type == "indoorinfra" &&
            n.HasRelatedRoomId &&
            indoorInfrastructures.ContainsKey(n.related_room_id) &&
            indoorInfrastructures[n.related_room_id].infra_id == currentInfraId &&
            n.indoor != null &&
            n.indoor.floor == currentFloor.ToString()
        ).ToList();

        Dictionary<string, Node> roomIdToNode = new Dictionary<string, Node>();
        foreach (var node in currentFloorNodes)
        {
            roomIdToNode[node.related_room_id] = node;
        }

        var currentInfraEdges = indoorEdges.Values.Where(e =>
            e.infra_id == currentInfraId &&
            roomIdToNode.ContainsKey(e.from_indoor) &&
            roomIdToNode.ContainsKey(e.to_indoor)
        );

        foreach (var edge in currentInfraEdges)
        {
            bool isHighlighted = highlightedEdgeIds.Contains(edge.indooredge_id);
            GameObject prefabToUse = isHighlighted && highlightedEdgePrefab != null ? highlightedEdgePrefab : indoorEdgePrefab;
            Color colorToUse = isHighlighted ? highlightEdgeColor : normalEdgeColor;

            SpawnEdge(edge, roomIdToNode[edge.from_indoor], roomIdToNode[edge.to_indoor], prefabToUse, colorToUse);
        }
    }

    private void SpawnEdge(IndoorEdge edge, Node fromNode, Node toNode, GameObject prefab, Color color)
    {
        GameObject edgeObj = Instantiate(prefab, markersContainer);
        edgeObj.name = $"Edge_{edge.indooredge_id}";

        RectTransform edgeRect = edgeObj.GetComponent<RectTransform>();
        if (edgeRect != null)
        {
            Vector2 fromPos = WorldToMapPosition(fromNode.indoor.x, fromNode.indoor.y);
            Vector2 toPos = WorldToMapPosition(toNode.indoor.x, toNode.indoor.y);

            Vector2 direction = toPos - fromPos;
            float distance = direction.magnitude;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            Vector2 centerPos = (fromPos + toPos) / 2f;
            edgeRect.anchoredPosition = centerPos;
            edgeRect.sizeDelta = new Vector2(distance, edgeWidth);
            edgeRect.rotation = Quaternion.Euler(0, 0, angle);

            Image edgeImage = edgeObj.GetComponent<Image>();
            if (edgeImage != null)
            {
                edgeImage.color = color;
            }
        }

        spawnedEdges[edge.indooredge_id] = edgeObj;
    }

    private void SpawnMarkerForNode(Node node)
    {
        if (node.indoor == null || !indoorInfrastructures.ContainsKey(node.related_room_id))
            return;

        IndoorInfrastructure indoor = indoorInfrastructures[node.related_room_id];

        bool isDestination = node.node_id == destinationNodeId;

        GameObject markerPrefab = GetMarkerPrefabForType(indoor.indoor_type, isDestination);

        if (markerPrefab == null)
            return;

        GameObject marker = Instantiate(markerPrefab, markersContainer);

        Vector2 mapPos = WorldToMapPosition(node.indoor.x, node.indoor.y);
        marker.GetComponent<RectTransform>().anchoredPosition = mapPos;

        TextMeshProUGUI nameText = marker.GetComponentInChildren<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = node.name;
        }

        spawnedMarkers[node.node_id] = marker;
    }

    private GameObject GetMarkerPrefabForType(string indoorType, bool isDestination)
    {
        if (isDestination && indoorType.ToLower() == "room" && destinationRoomMarkerPrefab != null)
        {
            return destinationRoomMarkerPrefab;
        }

        switch (indoorType.ToLower())
        {
            case "room":
                return roomMarkerPrefab;
            case "stairs":
                return stairsMarkerPrefab;
            case "fire_exit":
                return fireExitMarkerPrefab;
            case "entrance":
                return entranceMarkerPrefab;
            default:
                return roomMarkerPrefab;
        }
    }

    private Vector2 WorldToMapPosition(float x, float y)
    {
        float mapX = x * pixelsPerMeter;
        float mapY = y * pixelsPerMeter;

        return new Vector2(mapX, mapY);
    }

    public void ChangeFloor(int direction)
    {
        if (availableFloors.Count == 0)
            return;

        int currentIndex = availableFloors.IndexOf(currentFloor);

        if (currentIndex == -1)
            return;

        int newIndex = currentIndex + direction;

        if (newIndex < 0 || newIndex >= availableFloors.Count)
            return;

        currentFloor = availableFloors[newIndex];

        SpawnEdgesForCurrentFloor();
        SpawnMarkersForCurrentFloor();

        if (ARMapModeController.Instance != null)
        {
            ARMapModeController.Instance.UpdateFloorIndicator(currentFloor);
        }
    }

    private void ClearAllMarkers()
    {
        foreach (var marker in spawnedMarkers.Values)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        spawnedMarkers.Clear();
    }

    private void ClearAllEdges()
    {
        foreach (var edge in spawnedEdges.Values)
        {
            if (edge != null)
            {
                Destroy(edge);
            }
        }

        spawnedEdges.Clear();
    }

    public int GetCurrentFloor()
    {
        return currentFloor;
    }

    public List<int> GetAvailableFloors()
    {
        return new List<int>(availableFloors);
    }
}