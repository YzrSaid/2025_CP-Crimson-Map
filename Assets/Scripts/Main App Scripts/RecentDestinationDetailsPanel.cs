using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class RecentDestinationDetailsPanel : MonoBehaviour
{
    [Header("Header Info")]
    public TextMeshProUGUI titleText;          
    public TextMeshProUGUI routeText;          
    public TextMeshProUGUI timestampText;      

    [Header("Route Summary")]
    public TextMeshProUGUI distanceText;       
    public TextMeshProUGUI walkingTimeText;    
    public TextMeshProUGUI viaModeText;       

    [Header("Directions List")]
    public ScrollRect directionsScrollView;
    public GameObject directionItemPrefab;

    [Header("Path Visualization")]
    public TextMeshProUGUI pathNodesText;      

    [Header("Buttons")]
    public Button navigateButton;
    public Button closeButton;

    [Header("Background Settings")]
    public string backgroundPanelName = "BackgroundForExplorePanel";

    private SavedNavigation navigationData;
    private Transform backgroundTransform;

    void Awake()
    {
        if (navigateButton != null)
            navigateButton.onClick.AddListener(OnNavigateClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        SetupBackground();
    }

    private void SetupBackground()
    {
        backgroundTransform = transform.root.Find(backgroundPanelName);
        if (backgroundTransform == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                backgroundTransform = SearchAllChildren(canvas.transform, backgroundPanelName);
            }
        }
        if (backgroundTransform != null)
        {
            backgroundTransform.gameObject.SetActive(true);
        }
    }

    private Transform SearchAllChildren(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }
        return null;
    }

    public void SetData(SavedNavigation data)
    {
        navigationData = data;
        PopulateUI();
    }

    void PopulateUI()
    {
        if (navigationData == null) return;

        if (titleText != null)
            titleText.text = "Route Details";

        if (routeText != null)
            routeText.text = $"{navigationData.startNodeName} → {navigationData.endNodeName}";

        if (timestampText != null)
            timestampText.text = FormatTimestamp(navigationData.timestamp);

        if (distanceText != null)
            distanceText.text = $"Distance: {navigationData.formattedDistance}";

        if (walkingTimeText != null)
            walkingTimeText.text = $"Walking Time: {navigationData.walkingTime}";

        if (viaModeText != null)
            viaModeText.text = navigationData.viaMode;

        PopulateDirections();

        if (pathNodesText != null)
        {
            pathNodesText.text = "Path: " + string.Join(" → ", navigationData.pathNodes);
        }
    }

    private void PopulateDirections()
    {
        if (directionsScrollView == null || directionItemPrefab == null)
            return;

        ClearDirections();

        Transform content = directionsScrollView.content;

        for (int i = 0; i < navigationData.directions.Count; i++)
        {
            SavedDirection dir = navigationData.directions[i];
            GameObject itemObj = Instantiate(directionItemPrefab, content);

            TextMeshProUGUI instructionText = itemObj.GetComponentInChildren<TextMeshProUGUI>();
            if (instructionText != null)
            {
                instructionText.text = $"{i + 1}. {dir.instruction}";
            }
        }
    }

    private void ClearDirections()
    {
        if (directionsScrollView == null || directionsScrollView.content == null)
            return;

        foreach (Transform child in directionsScrollView.content)
        {
            Destroy(child.gameObject);
        }
    }

    private string FormatTimestamp(string timestamp)
    {
        try
        {
            DateTime dt = DateTime.ParseExact(timestamp, "yyyy-MM-dd HH:mm:ss", null);
            return dt.ToString("MMM dd, yyyy hh:mm tt");
        }
        catch
        {
            return timestamp;
        }
    }

    void OnNavigateClicked()
    {
        if (navigationData == null) return;

        RestoreNavigationData();

        Close();

        ARManagerCleanup arCleanup = FindObjectOfType<ARManagerCleanup>();
        if (arCleanup != null)
        {
            arCleanup.LoadARNavigationWithScene("ARScene");
        }
    }

    private void RestoreNavigationData()
    {
        PlayerPrefs.SetString("ARNavigation_StartNodeId", navigationData.startNodeId);
        PlayerPrefs.SetString("ARNavigation_EndNodeId", navigationData.endNodeId);
        PlayerPrefs.SetString("ARNavigation_StartNodeName", navigationData.startNodeName);
        PlayerPrefs.SetString("ARNavigation_EndNodeName", navigationData.endNodeName);
        PlayerPrefs.SetFloat("ARNavigation_TotalDistance", navigationData.totalDistance);
        PlayerPrefs.SetString("ARNavigation_FormattedDistance", navigationData.formattedDistance);
        PlayerPrefs.SetString("ARNavigation_WalkingTime", navigationData.walkingTime);
        PlayerPrefs.SetString("ARNavigation_ViaMode", navigationData.viaMode);

        PlayerPrefs.SetInt("ARNavigation_PathNodeCount", navigationData.pathNodes.Count);
        for (int i = 0; i < navigationData.pathNodes.Count; i++)
        {
            PlayerPrefs.SetString($"ARNavigation_PathNode_{i}", navigationData.pathNodes[i]);
        }

        PlayerPrefs.SetInt("ARNavigation_EdgeCount", navigationData.edges.Count);
        for (int i = 0; i < navigationData.edges.Count; i++)
        {
            PlayerPrefs.SetString($"ARNavigation_Edge_{i}_From", navigationData.edges[i].fromNode);
            PlayerPrefs.SetString($"ARNavigation_Edge_{i}_To", navigationData.edges[i].toNode);
        }

        PlayerPrefs.SetInt("ARNavigation_DirectionCount", navigationData.directions.Count);
        for (int i = 0; i < navigationData.directions.Count; i++)
        {
            var dir = navigationData.directions[i];
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Instruction", dir.instruction);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_Turn", dir.turn);
            PlayerPrefs.SetFloat($"ARNavigation_Direction_{i}_Distance", dir.distance);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNodeId", dir.destNodeId);
            PlayerPrefs.SetString($"ARNavigation_Direction_{i}_DestNode", dir.destNode);
            PlayerPrefs.SetInt($"ARNavigation_Direction_{i}_IsIndoorGrouped", dir.isIndoorGrouped ? 1 : 0);
            PlayerPrefs.SetInt($"ARNavigation_Direction_{i}_IsIndoorDirection", dir.isIndoorDirection ? 1 : 0);
        }

        PlayerPrefs.Save();
    }

    void Close()
    {
        Destroy(gameObject);
        if (backgroundTransform != null)
        {
            backgroundTransform.gameObject.SetActive(false);
        }
    }
}