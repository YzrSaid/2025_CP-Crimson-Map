using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;
using Mapbox.Unity.Map;

public class MapDropdown : MonoBehaviour
{
    [Header("UI References")]
    public Button dropdownButton;
    public GameObject panel;
    public GameObject panelForBG;
    public GameObject mapButtonPrefab;
    public Transform buttonContainer;

    [Header("Loading Manager")]
    public HomePageLoadingManager loadingManager;

    private List<MapInfo> availableMaps = new List<MapInfo>();
    private bool isDataLoaded = false;

    void Start()
    {
        if (loadingManager == null)
        {
            loadingManager = FindObjectOfType<HomePageLoadingManager>();
        }

        dropdownButton.onClick.AddListener(TogglePanel);
        StartCoroutine(WaitForMapManagerData());
        panel.SetActive(false);
        panelForBG.SetActive(false);
    }

    IEnumerator WaitForMapManagerData()
    {
        while (MapManager.Instance == null || !MapManager.Instance.IsReady())
            yield return new WaitForSeconds(0.1f);

        availableMaps = MapManager.Instance.GetAvailableMaps();
        isDataLoaded = true;
        PopulatePanel();
    }

    void TogglePanel()
    {
        if (!isDataLoaded) return;

        bool isActive = !panel.activeSelf;
        panel.SetActive(isActive);
        panelForBG.SetActive(isActive);
    }

    void PopulatePanel()
    {
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        foreach (var map in availableMaps)
        {
            GameObject btnObj = Instantiate(mapButtonPrefab, buttonContainer);

            var tmpText = btnObj.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmpText != null) tmpText.text = map.map_name;

            var regularText = btnObj.GetComponentInChildren<Text>();
            if (regularText != null) regularText.text = map.map_name;

            Button button = btnObj.GetComponent<Button>();
            if (button != null)
            {
                MapInfo selectedMap = map;
                button.onClick.AddListener(() => SelectMap(selectedMap));
            }
        }
    }

    void SelectMap(MapInfo map)
    {
        panel.SetActive(false);
        panelForBG.SetActive(false);

        if (MapManager.Instance != null && MapManager.Instance.IsReady())
        {
            if (loadingManager != null)
                loadingManager.TriggerMapChangeLoading();

            MapManager.Instance.LoadMap(map);
            MapManager.Instance.SnapToCurrentMapCenter(); 
        }
    }

    public MapInfo GetCurrentlySelectedMap()
    {
        return MapManager.Instance?.GetCurrentMap();
    }

    public void RefreshMapList()
    {
        if (MapManager.Instance != null && MapManager.Instance.IsReady())
        {
            availableMaps = MapManager.Instance.GetAvailableMaps();
            PopulatePanel();
        }
    }

    public void SelectMapById(string mapId)
    {
        MapInfo targetMap = availableMaps.Find(m => m.map_id == mapId);
        if (targetMap != null)
            SelectMap(targetMap);
    }

    public void SelectDefaultMap()
    {
        if (availableMaps.Count > 0)
            SelectMap(availableMaps[0]);
    }

    void OnDestroy()
    {
        dropdownButton?.onClick.RemoveAllListeners();
    }
}
