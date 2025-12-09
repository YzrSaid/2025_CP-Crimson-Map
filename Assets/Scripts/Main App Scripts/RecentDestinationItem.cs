using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class RecentDestinationItem : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI routeText;           
    public TextMeshProUGUI timestampText;      
    public TextMeshProUGUI distanceText;    
    public TextMeshProUGUI viaModeText;      

    [Header("Navigation Buttons")]
    public Button navigateButton;
    public Button viewDetailsButton;

    [Header("Details Panel")]
    public GameObject detailsPanelPrefab;
    public string targetCanvasName = "Canvas";

    private SavedNavigation navigationData;

    void Awake()
    {
        if (navigateButton != null)
            navigateButton.onClick.AddListener(OnNavigateClicked);
        if (viewDetailsButton != null)
            viewDetailsButton.onClick.AddListener(OnViewDetailsClicked);
    }

    public void SetNavigationData(SavedNavigation data)
    {
        navigationData = data;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (navigationData == null) return;

        if (routeText != null)
        {
            routeText.text = $"{navigationData.startNodeName} → {navigationData.endNodeName}";
        }

        if (timestampText != null)
        {
            timestampText.text = FormatTimestamp(navigationData.timestamp);
        }

        if (distanceText != null)
        {
            distanceText.text = navigationData.formattedDistance;
        }

        if (viaModeText != null)
        {
            viaModeText.text = navigationData.viaMode;
        }
    }

    private string FormatTimestamp(string timestamp)
    {
        try
        {
            // Parse: "2025-12-02 11:43:17"
            DateTime dt = DateTime.ParseExact(timestamp, "yyyy-MM-dd HH:mm:ss", null);
            // Format: "Dec 02, 2025 11:43 AM"
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

    void OnViewDetailsClicked()
    {
        Canvas targetCanvas = FindCanvasByName(targetCanvasName);

        if (targetCanvas == null)
        {
            return;
        }

        GameObject detailsPanel = Instantiate(detailsPanelPrefab, targetCanvas.transform);
        RecentDestinationDetailsPanel detailsScript = detailsPanel.GetComponent<RecentDestinationDetailsPanel>();

        if (detailsScript != null)
        {
            detailsScript.SetData(navigationData);
        }
    }

    private Canvas FindCanvasByName(string canvasName)
    {
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in allCanvases)
        {
            if (canvas.gameObject.name == canvasName)
            {
                return canvas;
            }
        }
        return null;
    }

    public SavedNavigation GetNavigationData()
    {
        return navigationData;
    }
}