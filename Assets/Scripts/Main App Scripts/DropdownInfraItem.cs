using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DropdownInfraItem : MonoBehaviour
{
    [Header("UI References")]
    public Button infraButton;
    public TextMeshProUGUI infraNameText;
    public Button expandArrowButton;
    public GameObject arrowDownImage;
    public GameObject arrowUpImage;
    public GameObject roomsContainer;
    public Transform roomsContent;

    [Header("Prefab")]
    public GameObject roomItemPrefab;

    [Header("Height Settings")]
    public float collapsedHeight = 50f;
    public float roomItemHeight = 45f;
    public float roomSpacing = 2f;

    private Infrastructure infrastructure;
    private List<IndoorInfrastructure> rooms;
    private SearchableDropdown parentDropdown;
    private bool isExpanded = false;

    private Dictionary<string, GameObject> roomItems = new Dictionary<string, GameObject>();
    private RectTransform rectTransform;

    public string InfrastructureName => infrastructure?.name ?? "";
    public bool HasRooms => rooms != null && rooms.Count > 0;
    public List<string> RoomNames => rooms?.Select(r => r.name).ToList() ?? new List<string>();

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Initialize(Infrastructure infra, List<IndoorInfrastructure> indoorRooms, SearchableDropdown dropdown)
    {
        infrastructure = infra;
        rooms = indoorRooms;
        parentDropdown = dropdown;

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (infraNameText != null)
        {
            infraNameText.text = infra.name;
        }

        if (HasRooms)
        {
            if (expandArrowButton != null)
            {
                expandArrowButton.gameObject.SetActive(true);
                expandArrowButton.onClick.AddListener(ToggleRooms);
                UpdateArrowVisibility(false);
            }

            if (infraButton != null)
            {
                infraButton.onClick.AddListener(() => OnInfrastructureSelected());
            }

            SetupRoomItems();
        }
        else
        {
            if (expandArrowButton != null)
            {
                expandArrowButton.gameObject.SetActive(false);
            }

            if (infraButton != null)
            {
                infraButton.onClick.AddListener(() => OnInfrastructureSelected());
            }

            if (roomsContainer != null)
            {
                roomsContainer.SetActive(false);
            }
        }

        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, collapsedHeight);
    }

    private void SetupRoomItems()
    {
        if (roomsContainer == null || roomsContent == null || roomItemPrefab == null)
        {
            return;
        }

        foreach (var room in rooms)
        {
            GameObject roomItemObj = Instantiate(roomItemPrefab, roomsContent);
            DropdownRoomItem roomItem = roomItemObj.GetComponent<DropdownRoomItem>();

            if (roomItem != null)
            {
                roomItem.Initialize(room, parentDropdown);
            }

            roomItems[room.room_id] = roomItemObj;
        }

        roomsContainer.SetActive(false);
    }

    private void ToggleRooms()
    {
        if (isExpanded)
        {
            CollapseRooms();
        }
        else
        {
            ExpandRooms(false);
        }
    }

    public void ExpandRooms(bool silent)
    {
        if (!HasRooms || roomsContainer == null)
        {
            return;
        }

        isExpanded = true;
        roomsContainer.SetActive(true);

        UpdateArrowVisibility(true);

        int visibleRoomCount = GetVisibleRoomCount();
        float totalRoomHeight = (roomItemHeight * visibleRoomCount) + (roomSpacing * (visibleRoomCount - 1));
        float targetHeight = collapsedHeight + totalRoomHeight;

        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);

        if (!silent)
        {
            ForceLayoutUpdate();
        }
    }

    public void CollapseRooms()
    {
        if (!HasRooms || roomsContainer == null)
        {
            return;
        }

        isExpanded = false;
        roomsContainer.SetActive(false);

        UpdateArrowVisibility(false);

        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, collapsedHeight);

        ForceLayoutUpdate();
    }

    private void UpdateArrowVisibility(bool isExpanded)
    {
        if (arrowDownImage != null)
        {
            arrowDownImage.SetActive(!isExpanded);
        }

        if (arrowUpImage != null)
        {
            arrowUpImage.SetActive(isExpanded);
        }
    }

    private int GetVisibleRoomCount()
    {
        int count = 0;
        foreach (var kvp in roomItems)
        {
            if (kvp.Value.activeSelf)
            {
                count++;
            }
        }
        return count;
    }

    public void FilterRooms(string searchText)
    {
        if (!HasRooms)
        {
            return;
        }

        foreach (var kvp in roomItems)
        {
            DropdownRoomItem roomItem = kvp.Value.GetComponent<DropdownRoomItem>();
            if (roomItem != null)
            {
                bool matches = roomItem.RoomName.ToLower().Contains(searchText.ToLower());
                kvp.Value.SetActive(matches);
            }
        }

        if (isExpanded)
        {
            int visibleCount = GetVisibleRoomCount();
            float totalRoomHeight = (roomItemHeight * visibleCount) + (roomSpacing * (visibleCount - 1));
            float targetHeight = collapsedHeight + totalRoomHeight;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            ForceLayoutUpdate();
        }
    }

    public void ShowAllRooms()
    {
        if (!HasRooms)
        {
            return;
        }

        foreach (var kvp in roomItems)
        {
            kvp.Value.SetActive(true);
        }

        if (isExpanded)
        {
            int visibleCount = GetVisibleRoomCount();
            float totalRoomHeight = (roomItemHeight * visibleCount) + (roomSpacing * (visibleCount - 1));
            float targetHeight = collapsedHeight + totalRoomHeight;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            ForceLayoutUpdate();
        }
    }

    private void OnInfrastructureSelected()
    {
        if (parentDropdown != null)
        {
            parentDropdown.SelectDestination(infrastructure.infra_id, "infrastructure", infrastructure.name);
        }
    }

    private void ForceLayoutUpdate()
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        if (transform.parent != null)
        {
            RectTransform parentRect = transform.parent.GetComponent<RectTransform>();
            if (parentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }
    }

    void OnDestroy()
    {
        if (infraButton != null)
        {
            infraButton.onClick.RemoveAllListeners();
        }

        if (expandArrowButton != null)
        {
            expandArrowButton.onClick.RemoveAllListeners();
        }
    }
}