using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SearchableDropdown : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField searchField;
    public Button dropdownButton;
    public GameObject arrowDownButton;
    public GameObject arrowUpButton;
    public GameObject dropdownPanel;
    public ScrollRect scrollView;
    public Transform contentContainer;

    [Header("Prefabs")]
    public GameObject infraItemPrefab;
    public GameObject roomItemPrefab;

    [Header("Settings")]
    public string searchPlaceholder = "Search destination...";
    public float itemHeight = 50f;
    public float roomItemHeight = 45f;

    private Dictionary<string, DropdownInfraItem> infraItems = new Dictionary<string, DropdownInfraItem>();
    private List<GameObject> allInstantiatedItems = new List<GameObject>();
    private bool isOpen = false;
    private bool isManualOpen = false;
    private bool isARMode = false;

    private string selectedId = null;
    private string selectedType = null;
    private string selectedDisplayName = null;

    public event Action<string, string, string> OnDestinationSelected;

    void Awake()
    {
        isARMode = ARModeHelper.IsARMode();
        Debug.Log($"[SearchableDropdown] Initialized in {(isARMode ? "AR" : "Normal")} mode");

        if (dropdownButton != null)
        {
            dropdownButton.onClick.AddListener(ToggleDropdown);
        }

        if (searchField != null)
        {
            searchField.onValueChanged.AddListener(OnSearchTextChanged);
            if (searchField.placeholder != null)
            {
                TextMeshProUGUI placeholder = searchField.placeholder.GetComponent<TextMeshProUGUI>();
                if (placeholder != null)
                {
                    placeholder.text = searchPlaceholder;
                }
            }
        }

        if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(false);
        }

        UpdateArrowVisibility(false);
    }

    public void ResetSelection()
    {
        selectedId = null;
        selectedType = null;
        selectedDisplayName = null;

        if (searchField != null)
        {
            searchField.text = "";
        }
    }

    public void Initialize(InfrastructureList infrastructureList, Dictionary<string, List<IndoorInfrastructure>> infraToRoomsMap)
    {
        ClearDropdown();

        if (infrastructureList == null || infrastructureList.infrastructures == null)
        {
            Debug.LogWarning("[SearchableDropdown] No infrastructures to initialize");
            return;
        }

        foreach (var infra in infrastructureList.infrastructures)
        {
            bool hasRooms = !isARMode && 
                           infraToRoomsMap.ContainsKey(infra.infra_id) &&
                           infraToRoomsMap[infra.infra_id].Count > 0;

            GameObject infraItemObj = Instantiate(infraItemPrefab, contentContainer);
            DropdownInfraItem infraItem = infraItemObj.GetComponent<DropdownInfraItem>();

            if (infraItem != null)
            {
                List<IndoorInfrastructure> rooms = hasRooms ? infraToRoomsMap[infra.infra_id] : null;
                infraItem.Initialize(infra, rooms, this);
                infraItems[infra.infra_id] = infraItem;
            }

            allInstantiatedItems.Add(infraItemObj);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);

        Debug.Log($"[SearchableDropdown] Initialized with {infraItems.Count} infrastructures (AR Mode: {isARMode})");
    }

    public void SelectDestination(string id, string type, string displayName)
    {
        selectedId = id;
        selectedType = type;
        selectedDisplayName = displayName;

        if (searchField != null)
        {
            searchField.text = displayName;
        }

        CloseDropdown();

        OnDestinationSelected?.Invoke(id, type, displayName);
    }

    public (string id, string type, string displayName) GetSelectedDestination()
    {
        return (selectedId, selectedType, selectedDisplayName);
    }

    private void ToggleDropdown()
    {
        if (isOpen)
        {
            CloseDropdown();
        }
        else
        {
            isManualOpen = true;
            OpenDropdown();
        }
    }

    private void OpenDropdown()
    {
        if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(true);
            isOpen = true;

            UpdateArrowVisibility(true);

            if (isManualOpen)
            {
                ShowAllItems();
                isManualOpen = false;
            }
            else
            {
                string currentSearch = searchField != null ? searchField.text : "";
                if (string.IsNullOrWhiteSpace(currentSearch))
                {
                    ShowAllItems();
                }
                else
                {
                    OnSearchTextChanged(currentSearch);
                }
            }
        }
    }

    private void CloseDropdown()
    {
        if (dropdownPanel != null)
        {
            dropdownPanel.SetActive(false);
            isOpen = false;
            isManualOpen = false;

            UpdateArrowVisibility(false);

            CollapseAllAccordions();
        }
    }

    private void UpdateArrowVisibility(bool isDropdownOpen)
    {
        if (arrowDownButton != null)
        {
            arrowDownButton.SetActive(!isDropdownOpen);
        }

        if (arrowUpButton != null)
        {
            arrowUpButton.SetActive(isDropdownOpen);
        }
    }

    private void OnSearchTextChanged(string searchText)
    {
        if (!isOpen && !string.IsNullOrWhiteSpace(searchText))
        {
            OpenDropdown();
            return;
        }

        if (!isOpen)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            ShowAllItems();
            return;
        }

        searchText = searchText.ToLower();

        foreach (var kvp in infraItems)
        {
            DropdownInfraItem item = kvp.Value;
            bool infraMatches = item.InfrastructureName.ToLower().Contains(searchText);
            
            bool anyRoomMatches = !isARMode && 
                                 item.HasRooms && 
                                 item.RoomNames.Any(roomName => roomName.ToLower().Contains(searchText));

            if (infraMatches || anyRoomMatches)
            {
                item.gameObject.SetActive(true);

                if (anyRoomMatches && !infraMatches)
                {
                    item.ExpandRooms(true);
                    item.FilterRooms(searchText);
                }
                else if (infraMatches)
                {
                    item.ShowAllRooms();
                }
            }
            else
            {
                item.gameObject.SetActive(false);
            }
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);
    }

    private void ShowAllItems()
    {
        foreach (var kvp in infraItems)
        {
            kvp.Value.gameObject.SetActive(true);
            kvp.Value.ShowAllRooms();
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);
    }

    private void CollapseAllAccordions()
    {
        foreach (var kvp in infraItems)
        {
            kvp.Value.CollapseRooms();
        }
    }
    
    public void ClearDropdown()
    {
        foreach (var item in allInstantiatedItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }

        allInstantiatedItems.Clear();
        infraItems.Clear();

        selectedId = null;
        selectedType = null;
        selectedDisplayName = null;

        if (searchField != null)
        {
            searchField.text = "";
        }
    }

    void OnDestroy()
    {
        if (dropdownButton != null)
        {
            dropdownButton.onClick.RemoveListener(ToggleDropdown);
        }

        if (searchField != null)
        {
            searchField.onValueChanged.RemoveListener(OnSearchTextChanged);
        }
    }
}