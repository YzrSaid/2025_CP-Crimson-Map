using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ReportAffectedItemPopulator : MonoBehaviour
{
    public TMP_Dropdown whichOneDropdown;

    private List<string> currentNodeIds = new List<string>();
    private List<string> currentEdgeIds = new List<string>();
    private List<string> currentDisplayNames = new List<string>();
    private Dictionary<string, string> nodeIdToName = new Dictionary<string, string>();

    void Start()
    {
        Debug.Log("[ReportAffectedItemPopulator] Start - Loading navigation data...");
        LoadNavigationData();
    }

    private void LoadNavigationData()
    {
        int nodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        Debug.Log($"[ReportAffectedItemPopulator] Node count from PlayerPrefs: {nodeCount}");

        currentNodeIds.Clear();
        nodeIdToName.Clear();

        for (int i = 0; i < nodeCount; i++)
        {
            string nodeId = PlayerPrefs.GetString($"ARNavigation_PathNode_{i}", "");
            if (!string.IsNullOrEmpty(nodeId))
            {
                currentNodeIds.Add(nodeId);
                Debug.Log($"[ReportAffectedItemPopulator] Added node ID: {nodeId}");
            }
        }

        Debug.Log($"[ReportAffectedItemPopulator] Total nodes loaded: {currentNodeIds.Count}");

        int edgeCount = PlayerPrefs.GetInt("ARNavigation_EdgeCount", 0);
        Debug.Log($"[ReportAffectedItemPopulator] Edge count from PlayerPrefs: {edgeCount}");

        currentEdgeIds.Clear();

        for (int i = 0; i < edgeCount; i++)
        {
            string fromNode = PlayerPrefs.GetString($"ARNavigation_Edge_{i}_From", "");
            string toNode = PlayerPrefs.GetString($"ARNavigation_Edge_{i}_To", "");

            if (!string.IsNullOrEmpty(fromNode) && !string.IsNullOrEmpty(toNode))
            {
                string edgeId = $"{fromNode}-{toNode}";
                currentEdgeIds.Add(edgeId);
                Debug.Log($"[ReportAffectedItemPopulator] Added edge: {edgeId}");
            }
        }

        Debug.Log($"[ReportAffectedItemPopulator] Total edges loaded: {currentEdgeIds.Count}");

        StartCoroutine(LoadNodeNamesFromJSON());
    }

    private IEnumerator LoadNodeNamesFromJSON()
    {
        string mapId = PlayerPrefs.GetString("ARScene_MapId", "");

        if (string.IsNullOrEmpty(mapId))
        {
            Debug.LogError("[ReportAffectedItemPopulator] ARScene_MapId not found in PlayerPrefs!");
            yield break;
        }

        Debug.Log($"[ReportAffectedItemPopulator] Loading nodes from map: {mapId}");

        string fileName = $"nodes_{mapId}.json";

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Debug.Log($"[ReportAffectedItemPopulator] Successfully loaded {fileName}");
                    Node[] nodesArray = JsonHelper.FromJson<Node>(jsonContent);
                    Debug.Log($"[ReportAffectedItemPopulator] Parsed {nodesArray.Length} nodes from JSON");

                    foreach (Node node in nodesArray)
                    {
                        nodeIdToName[node.node_id] = node.name;
                    }

                    Debug.Log($"[ReportAffectedItemPopulator] Node name dictionary has {nodeIdToName.Count} entries");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[ReportAffectedItemPopulator] Error loading nodes: {e.Message}");
                }
            },
            (error) =>
            {
                Debug.LogError($"[ReportAffectedItemPopulator] Failed to load {fileName}: {error}");
            }
        ));
    }

    public void PopulateAffectedItems(bool isNode)
    {
        Debug.Log($"[ReportAffectedItemPopulator] PopulateAffectedItems called - isNode: {isNode}");

        if (whichOneDropdown == null)
        {
            Debug.LogError("[ReportAffectedItemPopulator] whichOneDropdown is NULL!");
            return;
        }

        whichOneDropdown.ClearOptions();
        currentDisplayNames.Clear();

        if (isNode)
        {
            PopulateNodes();
        }
        else
        {
            PopulateEdges();
        }

        Debug.Log($"[ReportAffectedItemPopulator] Dropdown now has {whichOneDropdown.options.Count} options");
    }

    private void PopulateNodes()
    {
        Debug.Log($"[ReportAffectedItemPopulator] PopulateNodes - currentNodeIds count: {currentNodeIds.Count}");
        Debug.Log($"[ReportAffectedItemPopulator] PopulateNodes - nodeIdToName count: {nodeIdToName.Count}");

        List<string> options = new List<string> { "Select location..." };

        foreach (string nodeId in currentNodeIds)
        {
            if (nodeIdToName.ContainsKey(nodeId))
            {
                string nodeName = nodeIdToName[nodeId];
                options.Add(nodeName);
                currentDisplayNames.Add(nodeId);
                Debug.Log($"[ReportAffectedItemPopulator] Added node option: {nodeName} (ID: {nodeId})");
            }
            else
            {
                Debug.LogWarning($"[ReportAffectedItemPopulator] Node ID {nodeId} not found in nodeIdToName dictionary!");
            }
        }

        Debug.Log($"[ReportAffectedItemPopulator] Total node options: {options.Count}");
        whichOneDropdown.AddOptions(options);
    }

    private void PopulateEdges()
    {
        Debug.Log($"[ReportAffectedItemPopulator] PopulateEdges - currentEdgeIds count: {currentEdgeIds.Count}");
        Debug.Log($"[ReportAffectedItemPopulator] PopulateEdges - nodeIdToName count: {nodeIdToName.Count}");

        List<string> options = new List<string> { "Select pathway..." };

        foreach (string edgeId in currentEdgeIds)
        {
            Debug.Log($"[ReportAffectedItemPopulator] Processing edge: {edgeId}");

            // Split only on the MIDDLE hyphen (find the pattern: NodeID-NodeID)
            int middleIndex = edgeId.IndexOf('-', edgeId.IndexOf('-') + 1);

            if (middleIndex == -1)
            {
                Debug.LogError($"[ReportAffectedItemPopulator] Invalid edge format: {edgeId}");
                continue;
            }

            string fromNodeId = edgeId.Substring(0, middleIndex);
            string toNodeId = edgeId.Substring(middleIndex + 1);

            Debug.Log($"[ReportAffectedItemPopulator] From: {fromNodeId}, To: {toNodeId}");

            bool hasFromNode = nodeIdToName.ContainsKey(fromNodeId);
            bool hasToNode = nodeIdToName.ContainsKey(toNodeId);

            Debug.Log($"[ReportAffectedItemPopulator] Has fromNode: {hasFromNode}, Has toNode: {hasToNode}");

            if (hasFromNode && hasToNode)
            {
                string fromName = nodeIdToName[fromNodeId];
                string toName = nodeIdToName[toNodeId];
                string displayName = $"{fromName} → {toName}";
                options.Add(displayName);
                currentDisplayNames.Add(edgeId);
                Debug.Log($"[ReportAffectedItemPopulator] Added edge option: {displayName} (ID: {edgeId})");
            }
            else
            {
                Debug.LogWarning($"[ReportAffectedItemPopulator] Edge nodes not found - From: {fromNodeId} ({hasFromNode}), To: {toNodeId} ({hasToNode})");
            }
        }

        Debug.Log($"[ReportAffectedItemPopulator] Total edge options: {options.Count}");
        whichOneDropdown.AddOptions(options);
    }

    public string GetSelectedAffectedId(int index)
    {
        Debug.Log($"[ReportAffectedItemPopulator] GetSelectedAffectedId called - index: {index}");
        Debug.Log($"[ReportAffectedItemPopulator] currentDisplayNames count: {currentDisplayNames.Count}");

        if (index <= 0 || index > currentDisplayNames.Count)
        {
            Debug.LogWarning($"[ReportAffectedItemPopulator] Invalid index: {index}");
            return "";
        }

        string selectedId = currentDisplayNames[index - 1];
        Debug.Log($"[ReportAffectedItemPopulator] Selected ID: {selectedId}");
        return selectedId;
    }
}