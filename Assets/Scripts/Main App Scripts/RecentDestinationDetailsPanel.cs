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

    [Header("Directions List")]
    public ScrollRect directionsScrollView;
    public GameObject directionItemPrefab;  

    [Header("Buttons")]
    public Button closeButton;

    [Header("Background Settings")]
    public string backgroundPanelName = "BackgroundForExplorePanel";

    private SavedNavigation navigationData;
    private Transform backgroundTransform;

    void Awake()
    {
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
            routeText.text = $"From:{navigationData.startNodeName} \nTo:{navigationData.endNodeName}";

        if (timestampText != null)
            timestampText.text = FormatTimestamp(navigationData.timestamp);

        if (distanceText != null)
            distanceText.text = $"Distance: {navigationData.formattedDistance}";

        if (walkingTimeText != null)
            walkingTimeText.text = $"Walking Time: {navigationData.walkingTime}";

        PopulateDirections();
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

    void Close()
    {
        Destroy(gameObject);
        if (backgroundTransform != null)
        {
            backgroundTransform.gameObject.SetActive(false);
        }
    }
}