using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InfrastructurePopulator : MonoBehaviour
{
    [Header("UI References - New Searchable Dropdown")]
    public SearchableDropdown searchableDropdownTo;

    [Header("UI References - Legacy (Optional)")]
    public TMP_Dropdown dropdownTo;
    public ScrollRect destinationScrollView;
    public Transform destinationListContent;

    [Header("Data")]
    public InfrastructureList infrastructureList;
    public IndoorInfrastructureList indoorList;
    public NodeList nodeList;

    [Header("Settings")]
    public bool useSearchableDropdown = true;
    public bool useAccordionUI = false;
    public float maxWaitTime = 30f;

    public Dictionary<string, List<IndoorInfrastructure>> infraToRoomsMap = new Dictionary<string, List<IndoorInfrastructure>>();
    private Dictionary<string, GameObject> accordionInstances = new Dictionary<string, GameObject>();
    private string selectedDestinationId = null;
    private string selectedDestinationType = null;

    private MapManager mapManager;
    private ARMapManager arMapManager;
    private bool isARMode = false;

    private string currentMapId;
    private List<string> currentCampusIds = new List<string>();
    private HashSet<string> validInfraIds = new HashSet<string>();

    void Start()
    {
        isARMode = ARModeHelper.IsARMode();
        Debug.Log($"[InfraPopulator] Starting in {(isARMode ? "AR" : "Normal")} mode");

        if (isARMode && arMapManager != null)
        {
            arMapManager = ARMapManager.Instance;
            ARMapManager.OnSpawningComplete += OnARSpawningComplete;
        }
        else
        {
            mapManager = MapManager.Instance;
            if (mapManager != null) mapManager.OnMapChanged += OnMapChangedHandler;
        }

        StartCoroutine(WaitForDataInitializationThenLoad());
    }

    private void OnARSpawningComplete()
    {
        Debug.Log("[InfraPopulator] AR Spawning complete, refreshing data");
        RefreshForNewMap();
    }

    private void OnMapChangedHandler(MapInfo newMap)
    {
        RefreshForNewMap();
    }

    private IEnumerator WaitForDataInitializationThenLoad()
    {
        float waitTime = 0f;

        if (isARMode)
        {
            while (arMapManager == null && waitTime < maxWaitTime)
            {
                arMapManager = ARMapManager.Instance;
                waitTime += Time.deltaTime;
                yield return new WaitForSeconds(0.1f);
            }

            if (arMapManager != null)
            {
                while (!arMapManager.IsSpawningComplete() && waitTime < maxWaitTime)
                {
                    waitTime += Time.deltaTime;
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
        else
        {
            while (mapManager == null && waitTime < maxWaitTime)
            {
                mapManager = MapManager.Instance;
                waitTime += Time.deltaTime;
                yield return new WaitForSeconds(0.1f);
            }

            if (mapManager != null)
            {
                while (!mapManager.IsReady() && waitTime < maxWaitTime)
                {
                    waitTime += Time.deltaTime;
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }

        waitTime = 0f;
        while (waitTime < maxWaitTime)
        {
            if (GlobalManager.Instance != null && IsDataInitializationComplete())
            {
                yield return StartCoroutine(LoadAllData());
                yield break;
            }
            waitTime += Time.deltaTime;
            yield return new WaitForSeconds(0.1f);
        }

        yield return StartCoroutine(LoadAllData());
    }

    private bool IsDataInitializationComplete()
    {
        string infraPath = GetJsonFilePath("infrastructure.json");
        if (!File.Exists(infraPath)) return false;

        try
        {
            string infraContent = File.ReadAllText(infraPath);
            if (string.IsNullOrEmpty(infraContent) || infraContent.Length < 10) return false;
        }
        catch { return false; }

        return true;
    }

    private string GetJsonFilePath(string fileName)
    {
#if UNITY_EDITOR
        string streamingPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(streamingPath)) return streamingPath;
#endif
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    private IEnumerator LoadAllData()
    {
        if (isARMode)
        {
            currentMapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
            string campusIdsStr = PlayerPrefs.GetString("ARScene_CampusIds", "");
            currentCampusIds = string.IsNullOrEmpty(campusIdsStr)
                ? new List<string>()
                : new List<string>(campusIdsStr.Split(','));
            Debug.Log($"[InfraPopulator] AR Mode - Loading data for Map: {currentMapId}, Campuses: {string.Join(", ", currentCampusIds)}");
        }
        else
        {
            if (mapManager != null && mapManager.GetCurrentMap() != null)
            {
                currentMapId = mapManager.GetCurrentMap().map_id;
                currentCampusIds = mapManager.GetCurrentCampusIds();
                Debug.Log($"[InfraPopulator] Normal Mode - Loading data for Map: {currentMapId}, Campuses: {string.Join(", ", currentCampusIds)}");
            }
            else
            {
                Debug.LogWarning("[InfraPopulator] MapManager not ready or no map loaded. Loading all data without filtering.");
                currentMapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
            }
        }

        bool infraLoaded = false;
        bool indoorLoaded = false;
        bool nodesLoaded = false;

        string nodesFileName = $"nodes_{currentMapId}.json";
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            nodesFileName,
            (jsonContent) =>
            {
                OnNodesDataLoaded(jsonContent);
                nodesLoaded = true;
            },
            (error) =>
            {
                Debug.LogWarning($"[InfraPopulator] Failed to load nodes: {error}");
                nodesLoaded = true;
            }
        ));

        yield return new WaitUntil(() => nodesLoaded);

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            (jsonContent) =>
            {
                OnInfrastructureDataLoaded(jsonContent);
                infraLoaded = true;
            },
            (error) =>
            {
                Debug.LogError($"[InfraPopulator] Failed to load infrastructure: {error}");
                infraLoaded = true;
            }
        ));

        yield return new WaitUntil(() => infraLoaded);

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor.json",
            (jsonContent) =>
            {
                OnIndoorDataLoaded(jsonContent);
                indoorLoaded = true;
            },
            (error) =>
            {
                Debug.LogWarning($"[InfraPopulator] Failed to load indoor: {error}");
                indoorLoaded = true;
            }
        ));

        yield return new WaitUntil(() => indoorLoaded);

        BuildInfraToRoomsMapping();

        if (useSearchableDropdown && searchableDropdownTo != null) InitializeSearchableDropdown();
        else if (useAccordionUI && destinationScrollView != null && destinationListContent != null) PopulateAccordionUI();
        else if (dropdownTo != null) PopulateDropdown(dropdownTo);

        Debug.Log($"[InfraPopulator] Loaded {infrastructureList?.infrastructures?.Length ?? 0} infrastructures, {indoorList?.indoors?.Length ?? 0} indoor items");
    }

    private void OnNodesDataLoaded(string jsonContent)
    {
        try
        {
            Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
            nodeList = new NodeList { nodes = nodes.ToList() };
            validInfraIds.Clear();

            foreach (var node in nodes)
            {
                if (node.is_active && !string.IsNullOrEmpty(node.related_infra_id))
                {
                    if (currentCampusIds.Count == 0 || currentCampusIds.Contains(node.campus_id))
                        validInfraIds.Add(node.related_infra_id);
                }
            }
            Debug.Log($"[InfraPopulator] Found {validInfraIds.Count} valid infrastructure IDs for current map");
        }
        catch (Exception ex) { Debug.LogError($"[InfraPopulator] Error parsing nodes: {ex.Message}"); }
    }

    private void OnInfrastructureDataLoaded(string jsonContent)
    {
        try
        {
            string wrappedJson = "{\"infrastructures\":" + jsonContent + "}";
            InfrastructureList fullList = JsonUtility.FromJson<InfrastructureList>(wrappedJson);
            if (validInfraIds.Count > 0)
            {
                var filteredInfras = fullList.infrastructures.Where(infra => !infra.is_deleted && validInfraIds.Contains(infra.infra_id)).ToArray();
                infrastructureList = new InfrastructureList { infrastructures = filteredInfras };
                Debug.Log($"[InfraPopulator] Filtered to {filteredInfras.Length} infrastructures for map {currentMapId}");
            }
            else
            {
                infrastructureList = new InfrastructureList { infrastructures = fullList.infrastructures.Where(i => !i.is_deleted).ToArray() };
                Debug.LogWarning("[InfraPopulator] No valid infra IDs found, loading all infrastructures");
            }
        }
        catch (Exception ex) { Debug.LogError($"[InfraPopulator] Error parsing infrastructures: {ex.Message}"); }
    }

    private void OnIndoorDataLoaded(string jsonContent)
    {
        try
        {
            IndoorInfrastructure[] indoorArray = JsonHelper.FromJson<IndoorInfrastructure>(jsonContent);
            if (validInfraIds.Count > 0)
            {
                var filteredIndoors = indoorArray.Where(indoor => !indoor.is_deleted && validInfraIds.Contains(indoor.infra_id)).ToArray();
                indoorList = new IndoorInfrastructureList { indoors = filteredIndoors };
                Debug.Log($"[InfraPopulator] Filtered to {filteredIndoors.Length} indoor items for map {currentMapId}");
            }
            else
            {
                indoorList = new IndoorInfrastructureList { indoors = indoorArray.Where(i => !i.is_deleted).ToArray() };
                Debug.LogWarning("[InfraPopulator] No valid infra IDs found, loading all indoor items");
            }
        }
        catch (Exception ex) { Debug.LogError($"[InfraPopulator] Error parsing indoor infrastructures: {ex.Message}"); }
    }

    private void BuildInfraToRoomsMapping()
    {
        infraToRoomsMap.Clear();
        if (indoorList == null || indoorList.indoors == null) return;

        foreach (var indoor in indoorList.indoors)
        {
            if (indoor.is_deleted) continue;
            string indoorType = indoor.indoor_type?.ToLower();
            if (indoorType != "room" && indoorType != "fire_exit") continue;
            if (validInfraIds.Count > 0 && !validInfraIds.Contains(indoor.infra_id)) continue;

            if (!infraToRoomsMap.ContainsKey(indoor.infra_id)) infraToRoomsMap[indoor.infra_id] = new List<IndoorInfrastructure>();
            infraToRoomsMap[indoor.infra_id].Add(indoor);
        }
        Debug.Log($"[InfraPopulator] Built mapping for {infraToRoomsMap.Count} infrastructures with rooms");
    }

    void OnDestroy()
    {
        if (isARMode) ARMapManager.OnSpawningComplete -= OnARSpawningComplete;
        else if (mapManager != null) mapManager.OnMapChanged -= OnMapChangedHandler;
    }

    private void InitializeSearchableDropdown()
    {
        if (searchableDropdownTo == null) return;
        searchableDropdownTo.Initialize(infrastructureList, infraToRoomsMap);
        searchableDropdownTo.OnDestinationSelected += (id, type, displayName) =>
        {
            selectedDestinationId = id;
            selectedDestinationType = type;
            PathfindingController pathfinding = FindObjectOfType<PathfindingController>();
            if (pathfinding != null) pathfinding.SetDestination(id, type);
        };
    }

    private void PopulateAccordionUI()
    {
        if (destinationListContent == null) return;

        foreach (Transform child in destinationListContent) Destroy(child.gameObject);
        accordionInstances.Clear();

        if (infrastructureList == null || infrastructureList.infrastructures.Length == 0)
        {
            Debug.LogWarning("[InfraPopulator] No infrastructures to populate in accordion UI");
            return;
        }

        foreach (var infra in infrastructureList.infrastructures)
        {
            bool hasRooms = infraToRoomsMap.ContainsKey(infra.infra_id) && infraToRoomsMap[infra.infra_id].Count > 0;
            GameObject infraButton = CreateInfrastructureButton(infra.name, hasRooms);
            infraButton.transform.SetParent(destinationListContent, false);

            Button btn = infraButton.GetComponent<Button>();
            GameObject arrowIcon = infraButton.transform.Find("Arrow")?.gameObject;

            if (hasRooms)
            {
                GameObject roomsContainer = new GameObject("Rooms_" + infra.infra_id);
                roomsContainer.transform.SetParent(destinationListContent, false);

                RectTransform containerRect = roomsContainer.AddComponent<RectTransform>();
                containerRect.anchorMin = new Vector2(0, 1);
                containerRect.anchorMax = new Vector2(1, 1);
                containerRect.pivot = new Vector2(0.5f, 1);
                containerRect.sizeDelta = new Vector2(0, 0);

                VerticalLayoutGroup layout = roomsContainer.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.spacing = 2f;
                layout.padding = new RectOffset(30, 0, 0, 0);

                ContentSizeFitter fitter = roomsContainer.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                roomsContainer.SetActive(false);

                foreach (var room in infraToRoomsMap[infra.infra_id])
                {
                    GameObject roomButton = CreateRoomButton(room.name);
                    roomButton.transform.SetParent(roomsContainer.transform, false);
                    Button roomBtn = roomButton.GetComponent<Button>();
                    string roomId = room.room_id;
                    roomBtn.onClick.AddListener(() => OnDestinationSelected(roomId, "indoorinfra", room.name));
                }

                accordionInstances[infra.infra_id] = roomsContainer;
                string infraId = infra.infra_id;
                btn.onClick.AddListener(() => ToggleAccordion(infraId, arrowIcon));
            }
            else
            {
                string infraId = infra.infra_id;
                btn.onClick.AddListener(() => OnDestinationSelected(infraId, "infrastructure", infra.name));
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private GameObject CreateInfrastructureButton(string text, bool hasArrow)
    {
        GameObject buttonObj = new GameObject("Infra_" + text);
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(0, 50);

        Image bgImage = buttonObj.AddComponent<Image>();
        bgImage.color = new Color(1f, 1f, 1f, 1f);

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = bgImage;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;

        HorizontalLayoutGroup layout = buttonObj.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.spacing = 10;
        layout.childControlWidth = false;
        layout.childControlHeight = true;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
        textComp.text = text;
        textComp.fontSize = 16;
        textComp.color = Color.black;
        textComp.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(300, 30);

        LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1;
        textLayout.preferredHeight = 30;

        if (hasArrow)
        {
            GameObject arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(buttonObj.transform, false);
            TextMeshProUGUI arrowText = arrowObj.AddComponent<TextMeshProUGUI>();
            arrowText.text = "▶";
            arrowText.fontSize = 14;
            arrowText.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            arrowText.alignment = TextAlignmentOptions.Center;

            RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.sizeDelta = new Vector2(20, 20);

            LayoutElement arrowLayout = arrowObj.AddComponent<LayoutElement>();
            arrowLayout.minWidth = 20;
            arrowLayout.preferredWidth = 20;
            arrowLayout.preferredHeight = 20;
        }

        return buttonObj;
    }

    private GameObject CreateRoomButton(string text)
    {
        GameObject buttonObj = new GameObject("Room_" + text);
        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.sizeDelta = new Vector2(0, 45);

        Image bgImage = buttonObj.AddComponent<Image>();
        bgImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = bgImage;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        button.colors = colors;

        HorizontalLayoutGroup layout = buttonObj.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.padding = new RectOffset(15, 15, 10, 10);
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
        textComp.text = text;
        textComp.fontSize = 14;
        textComp.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        textComp.alignment = TextAlignmentOptions.MidlineLeft;

        LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1;
        textLayout.preferredHeight = 25;

        return buttonObj;
    }

    private void ToggleAccordion(string infraId, GameObject arrowIcon)
    {
        if (!accordionInstances.ContainsKey(infraId)) return;

        GameObject roomsContainer = accordionInstances[infraId];
        bool isOpen = roomsContainer.activeSelf;

        foreach (var kvp in accordionInstances) kvp.Value.SetActive(false);
        roomsContainer.SetActive(!isOpen);

        if (arrowIcon != null)
        {
            TextMeshProUGUI arrowText = arrowIcon.GetComponent<TextMeshProUGUI>();
            if (arrowText != null) arrowText.text = roomsContainer.activeSelf ? "▼" : "▶";
        }

        Canvas.ForceUpdateCanvases();
        if (destinationScrollView != null) LayoutRebuilder.ForceRebuildLayoutImmediate(destinationListContent as RectTransform);
    }

    private void OnDestinationSelected(string id, string type, string displayName)
    {
        selectedDestinationId = id;
        selectedDestinationType = type;

        PathfindingController pathfinding = FindObjectOfType<PathfindingController>();
        if (pathfinding != null) pathfinding.SetDestination(id, type);
    }

    private void PopulateDropdown(TMP_Dropdown dropdown)
    {
        dropdown.ClearOptions();
        if (infrastructureList == null || infrastructureList.infrastructures.Length == 0) return;

        List<string> options = new List<string>();
        foreach (var infra in infrastructureList.infrastructures)
        {
            options.Add(infra.name);
            if (infraToRoomsMap.ContainsKey(infra.infra_id))
                foreach (var room in infraToRoomsMap[infra.infra_id])
                    options.Add("    " + room.name);
        }
        dropdown.AddOptions(options);
    }

    public Infrastructure GetSelectedInfrastructure(TMP_Dropdown dropdown)
    {
        int index = dropdown.value;
        if (index >= 0 && index < infrastructureList.infrastructures.Length) return infrastructureList.infrastructures[index];
        return null;
    }

    public (string id, string type) GetSelectedDestinationFromDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null || infrastructureList == null) return (null, null);

        int selectedIndex = dropdown.value;
        string selectedText = dropdown.options[selectedIndex].text;

        if (selectedText.StartsWith("    "))
        {
            string roomName = selectedText.Trim();
            if (indoorList != null && indoorList.indoors != null)
                foreach (var indoor in indoorList.indoors)
                    if (!indoor.is_deleted && indoor.name == roomName)
                        return (indoor.room_id, "indoorinfra");
            return (null, null);
        }
        else
        {
            foreach (var infra in infrastructureList.infrastructures)
                if (infra.name == selectedText)
                    return (infra.infra_id, "infrastructure");
            return (null, null);
        }
    }

    public (string id, string type) GetSelectedDestination()
    {
        if (useSearchableDropdown && searchableDropdownTo != null)
        {
            var selection = searchableDropdownTo.GetSelectedDestination();
            return (selection.id, selection.type);
        }
        return (selectedDestinationId, selectedDestinationType);
    }

    public void RefreshForNewMap()
    {
        Debug.Log($"[InfraPopulator] Refreshing for new map in {(isARMode ? "AR" : "Normal")} mode...");

        infraToRoomsMap.Clear();
        accordionInstances.Clear();
        validInfraIds.Clear();
        selectedDestinationId = null;
        selectedDestinationType = null;

        if (useSearchableDropdown && searchableDropdownTo != null)
        {
            searchableDropdownTo.ClearDropdown();
            searchableDropdownTo.ResetSelection();
        }

        StopAllCoroutines();
        StartCoroutine(LoadAllData());
    }
}