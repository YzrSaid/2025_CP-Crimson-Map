using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ARRerouteUIManager : MonoBehaviour
{
    [Header("Reroute Panel")]
    public GameObject reroutePanel;
    public GameObject rerouteBGPanel;

    [Header("From Location - Searchable Dropdown")]
    public SearchableDropdown fromSearchableDropdown;

    [Header("To/Destination (Read-Only)")]
    public TextMeshProUGUI toDestinationText;

    [Header("Optional Exemption")]
    public TMP_Dropdown exemptionTypeDropdown; 
    public GameObject exemptionItemDropdownContainer;
    public TMP_Dropdown exemptionItemDropdown;

    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    [Header("Confirmation Panel")]
    public GameObject confirmationPanel;
    public GameObject confirmationBGPanel;
    public Button confirmYesButton;
    public Button confirmNoButton;

    [Header("References")]
    public InfrastructurePopulator infrastructurePopulator;
    public ARRerouteAffectedItemPopulator affectedItemPopulator;
    public ARPathfindingController arPathfindingController;

    private string selectedFromNodeId;
    private string selectedFromType;
    private string originalToNodeId;
    private string originalToNodeName;
    private ExemptionType selectedExemptionType = ExemptionType.None;
    private string exemptedItemId;

    void Start()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmationYes);

        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmationNo);

        if (exemptionTypeDropdown != null)
            exemptionTypeDropdown.onValueChanged.AddListener(OnExemptionTypeChanged);

        if (reroutePanel != null)
            reroutePanel.SetActive(false);

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (exemptionItemDropdownContainer != null)
            exemptionItemDropdownContainer.SetActive(false);

        LoadOriginalDestination();
        InitializeExemptionTypeDropdown();
    }

    void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);

        if (confirmYesButton != null)
            confirmYesButton.onClick.RemoveListener(OnConfirmationYes);

        if (confirmNoButton != null)
            confirmNoButton.onClick.RemoveListener(OnConfirmationNo);

        if (exemptionTypeDropdown != null)
            exemptionTypeDropdown.onValueChanged.RemoveListener(OnExemptionTypeChanged);

        if (fromSearchableDropdown != null)
        {
            fromSearchableDropdown.OnDestinationSelected -= OnFromLocationSelected;
        }
    }

    private void LoadOriginalDestination()
    {
        originalToNodeId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");
        originalToNodeName = PlayerPrefs.GetString("ARNavigation_EndNodeName", "Unknown");

        if (toDestinationText != null)
        {
            toDestinationText.text = originalToNodeName;
        }
    }

    private void InitializeExemptionTypeDropdown()
    {
        if (exemptionTypeDropdown == null)
            return;

        exemptionTypeDropdown.ClearOptions();
        
        List<string> options = new List<string>
        {
            "None",
            "Buildings/Nodes",
            "Paths/Walkways"
        };

        exemptionTypeDropdown.AddOptions(options);
        exemptionTypeDropdown.value = 0;
    }

    public void ShowReroutePanel()
    {
        if (reroutePanel != null)
        {
            reroutePanel.SetActive(true);
        }

        if (rerouteBGPanel != null)
        {
            rerouteBGPanel.SetActive(true);
        }

        if (infrastructurePopulator != null && fromSearchableDropdown != null)
        {
            if (infrastructurePopulator.infrastructureList != null)
            {
                fromSearchableDropdown.Initialize(
                    infrastructurePopulator.infrastructureList,
                    infrastructurePopulator.infraToRoomsMap
                );

                fromSearchableDropdown.OnDestinationSelected -= OnFromLocationSelected;
                fromSearchableDropdown.OnDestinationSelected += OnFromLocationSelected;

                Debug.Log("[ARRerouteUI] SearchableDropdown initialized");
            }
            else
            {
                Debug.LogWarning("[ARRerouteUI] InfrastructurePopulator data not ready!");
            }
        }
    }

    private void OnFromLocationSelected(string id, string type, string displayName)
    {
        selectedFromNodeId = id;
        selectedFromType = type;
        
        Debug.Log($"[ARRerouteUI] FROM selected: {displayName} (ID: {id}, Type: {type})");
    }

    public void HideReroutePanel()
    {
        if (reroutePanel != null)
        {
            reroutePanel.SetActive(false);
        }

        if (rerouteBGPanel != null)
        {
            rerouteBGPanel.SetActive(false);
        }

        ResetForm();
    }

    private void ResetForm()
    {
        // Reset SearchableDropdown
        if (fromSearchableDropdown != null)
        {
            fromSearchableDropdown.SelectDestination(null, null, "");
        }

        if (exemptionTypeDropdown != null)
            exemptionTypeDropdown.value = 0;

        if (exemptionItemDropdownContainer != null)
            exemptionItemDropdownContainer.SetActive(false);

        selectedFromNodeId = null;
        selectedFromType = null;
        selectedExemptionType = ExemptionType.None;
        exemptedItemId = null;
    }

    private void OnExemptionTypeChanged(int index)
    {
        selectedExemptionType = (ExemptionType)index;

        if (exemptionItemDropdownContainer != null)
        {
            exemptionItemDropdownContainer.SetActive(selectedExemptionType != ExemptionType.None);
        }

        if (affectedItemPopulator != null && selectedExemptionType != ExemptionType.None)
        {
            bool isNode = selectedExemptionType == ExemptionType.BuildingsNodes;
            affectedItemPopulator.PopulateAffectedItems(isNode);
        }
    }

    private void OnConfirmClicked()
    {
        if (string.IsNullOrEmpty(selectedFromNodeId))
        {
            Debug.LogWarning("[ARRerouteUI] Please select a FROM location");
            return;
        }

        if (selectedFromNodeId == originalToNodeId)
        {
            Debug.LogWarning("[ARRerouteUI] FROM cannot be the same as destination");
            return;
        }

        // Get exempted item if selected
        if (selectedExemptionType != ExemptionType.None && exemptionItemDropdown != null)
        {
            int selectedIndex = exemptionItemDropdown.value;
            exemptedItemId = affectedItemPopulator.GetSelectedAffectedId(selectedIndex);
        }

        ShowConfirmationPanel();
    }

    private void OnCancelClicked()
    {
        HideReroutePanel();
    }

    private void ShowConfirmationPanel()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);
        }

        if (confirmationBGPanel != null)
        {
            confirmationBGPanel.SetActive(true);
        }
    }

    private void HideConfirmationPanel()
    {
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(false);
        }

        if (confirmationBGPanel != null)
        {
            confirmationBGPanel.SetActive(false);
        }
    }

    private void OnConfirmationYes()
    {
        HideConfirmationPanel();
        HideReroutePanel();

        Debug.Log($"[ARRerouteUI] Starting reroute: FROM={selectedFromNodeId}, TO={originalToNodeId}, Exemption={selectedExemptionType}, Blocked={exemptedItemId}");

        if (arPathfindingController != null)
        {
            arPathfindingController.StartReroute(
                selectedFromNodeId,
                originalToNodeId,
                selectedExemptionType,
                exemptedItemId
            );
        }
        else
        {
            Debug.LogError("[ARRerouteUI] ARPathfindingController not found!");
        }
    }

    private void OnConfirmationNo()
    {
        HideConfirmationPanel();
    }

    public enum ExemptionType
    {
        None = 0,
        BuildingsNodes = 1,
        PathsWalkways = 2
    }
}