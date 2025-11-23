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
        LoadNavigationData();
    }

    private void LoadNavigationData()
    {
        int nodeCount = PlayerPrefs.GetInt("ARNavigation_PathNodeCount", 0);
        currentNodeIds.Clear();
        nodeIdToName.Clear();

        for (int i = 0; i < nodeCount; i++)
        {
            string nodeId = PlayerPrefs.GetString($"ARNavigation_PathNode_{i}", "");
            if (!string.IsNullOrEmpty(nodeId))
            {
                currentNodeIds.Add(nodeId);
            }
        }

        int edgeCount = PlayerPrefs.GetInt("ARNavigation_EdgeCount", 0);
        currentEdgeIds.Clear();

        for (int i = 0; i < edgeCount; i++)
        {
            string fromNode = PlayerPrefs.GetString($"ARNavigation_Edge_{i}_From", "");
            string toNode = PlayerPrefs.GetString($"ARNavigation_Edge_{i}_To", "");

            if (!string.IsNullOrEmpty(fromNode) && !string.IsNullOrEmpty(toNode))
            {
                string edgeId = $"{fromNode}-{toNode}";
                currentEdgeIds.Add(edgeId);
            }
        }

        StartCoroutine(LoadNodeNamesFromJSON());
    }

    private IEnumerator LoadNodeNamesFromJSON()
    {
        string mapId = PlayerPrefs.GetString("ARScene_MapId", "");

        if (string.IsNullOrEmpty(mapId))
        {
            yield break;
        }

        string fileName = $"nodes_{mapId}.json";

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodesArray = JsonHelper.FromJson<Node>(jsonContent);

                    foreach (Node node in nodesArray)
                    {
                        nodeIdToName[node.node_id] = node.name;
                    }
                }
                catch (System.Exception)
                {
                    // Handle exception as needed
                }
            },
            (error) =>
            {
                // Handle error as needed
            }
        ));
    }

    public void PopulateAffectedItems(bool isNode)
    {
        if (whichOneDropdown == null)
        {
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
    }

    private void PopulateNodes()
    {
        List<string> options = new List<string> { "Select location..." };

        foreach (string nodeId in currentNodeIds)
        {
            if (nodeIdToName.ContainsKey(nodeId))
            {
                string nodeName = nodeIdToName[nodeId];
                options.Add(nodeName);
                currentDisplayNames.Add(nodeId);
            }
        }

        whichOneDropdown.AddOptions(options);
    }

    private void PopulateEdges()
    {
        List<string> options = new List<string> { "Select pathway..." };

        foreach (string edgeId in currentEdgeIds)
        {
            int middleIndex = edgeId.IndexOf('-', edgeId.IndexOf('-') + 1);

            if (middleIndex == -1)
            {
                continue;
            }

            string fromNodeId = edgeId.Substring(0, middleIndex);
            string toNodeId = edgeId.Substring(middleIndex + 1);

            if (nodeIdToName.ContainsKey(fromNodeId) && nodeIdToName.ContainsKey(toNodeId))
            {
                string fromName = nodeIdToName[fromNodeId];
                string toName = nodeIdToName[toNodeId];
                string displayName = $"{fromName} → {toName}";
                options.Add(displayName);
                currentDisplayNames.Add(edgeId);
            }
        }

        whichOneDropdown.AddOptions(options);
    }

    public string GetSelectedAffectedId(int index)
    {
        if (index <= 0 || index > currentDisplayNames.Count)
        {
            return "";
        }

        return currentDisplayNames[index - 1];
    }
}