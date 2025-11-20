using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DropdownRoomItem : MonoBehaviour
{
    [Header("UI References")]
    public Button roomButton;
    public TextMeshProUGUI roomNameText;

    private IndoorInfrastructure room;
    private SearchableDropdown parentDropdown;

    public string RoomName => room?.name ?? "";

    public void Initialize(IndoorInfrastructure indoorRoom, SearchableDropdown dropdown)
    {
        room = indoorRoom;
        parentDropdown = dropdown;

        if (roomNameText != null)
        {
            roomNameText.text = room.name;
        }

        if (roomButton != null)
        {
            roomButton.onClick.AddListener(OnRoomSelected);
        }
    }

    private void OnRoomSelected()
    {
        if (parentDropdown != null && room != null)
        {
            parentDropdown.SelectDestination(room.room_id, "indoorinfra", room.name);
        }
    }

    void OnDestroy()
    {
        if (roomButton != null)
        {
            roomButton.onClick.RemoveListener(OnRoomSelected);
        }
    }
}