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
    public GameObject expandArrow;
    public GameObject roomsContainer;
    public Transform roomsContent;

    [Header("Prefab")]
    public GameObject roomItemPrefab;

    private Infrastructure infrastructure;
    private List<IndoorInfrastructure> rooms;
    private SearchableDropdown parentDropdown;
    private bool isExpanded = false;

    private Dictionary<string, GameObject> roomItems = new Dictionary<string, GameObject>();

    public string InfrastructureName => infrastructure?.name ?? "";
    public bool HasRooms => rooms != null && rooms.Count > 0;
    public List<string> RoomNames => rooms?.Select(r => r.name).ToList() ?? new List<string>();

    public void Initialize(Infrastructure infra, List<IndoorInfrastructure> indoorRooms, SearchableDropdown dropdown)
    {
        infrastructure = infra;
        rooms = indoorRooms;
        parentDropdown = dropdown;

        if (infraNameText != null)
        {
            infraNameText.text = infra.name;
        }

        if (HasRooms)
        {
            if (expandArrow != null)
            {
                expandArrow.SetActive(true);
            }

            if (infraButton != null)
            {
                infraButton.onClick.AddListener(ToggleRooms);
            }

            SetupRoomItems();
        }
        else
        {
            if (expandArrow != null)
            {
                expandArrow.SetActive(false);
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

        if (expandArrow != null)
        {
            TextMeshProUGUI arrowText = expandArrow.GetComponent<TextMeshProUGUI>();
            if (arrowText != null)
            {
                arrowText.text = "▼";
            }
        }

        if (!silent)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
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

        if (expandArrow != null)
        {
            TextMeshProUGUI arrowText = expandArrow.GetComponent<TextMeshProUGUI>();
            if (arrowText != null)
            {
                arrowText.text = "▶";
            }
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent as RectTransform);
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
    }

    private void OnInfrastructureSelected()
    {
        if (parentDropdown != null)
        {
            parentDropdown.SelectDestination(infrastructure.infra_id, "infrastructure", infrastructure.name);
        }
    }

    void OnDestroy()
    {
        if (infraButton != null)
        {
            infraButton.onClick.RemoveAllListeners();
        }
    }
}