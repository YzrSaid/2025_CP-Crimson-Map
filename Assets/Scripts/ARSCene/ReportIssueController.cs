using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using Firebase.Firestore;
using Firebase.Extensions;

public class ReportIssueController : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown issueTypeDropdown;
    public TMP_Dropdown whereIsIssueDropdown;
    public TMP_Dropdown whichOneDropdown;
    public Button submitButton;
    public Button cancelButton;

    [Header("Other Issue Container")]
    public GameObject otherIssueContainer;
    public TMP_InputField otherIssueInputField;

    [Header("Panels")]
    public GameObject reportFormPanel;
    public GameObject loadingPanel;
    public GameObject successPanel;
    public GameObject errorPanel;
    public TextMeshProUGUI errorMessageText;
    public Button retryButton;
    public Button errorCancelButton;

    [Header("Affected Item Populator")]
    public ReportAffectedItemPopulator affectedItemPopulator;

    private FirebaseFirestore db;
    private string selectedIssueType;
    private string selectedWhereType;
    private string selectedAffectedId;
    private bool isOtherIssue = false;

    private static readonly Dictionary<string, string> whereTypeToDatabase = new Dictionary<string, string>
    {
        { "Building/Location", "Node" },
        { "Walkway/Path", "Edge" }
    };

    void Start()
    {
        db = FirebaseFirestore.DefaultInstance;

        SetupIssueTypeDropdown();
        SetupWhereIsIssueDropdown();

        if (issueTypeDropdown != null)
        {
            issueTypeDropdown.onValueChanged.AddListener(OnIssueTypeChanged);
        }

        if (whereIsIssueDropdown != null)
        {
            whereIsIssueDropdown.onValueChanged.AddListener(OnWhereIsIssueChanged);
        }

        if (whichOneDropdown != null)
        {
            whichOneDropdown.onValueChanged.AddListener(OnWhichOneChanged);
        }

        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnSubmitClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        if (errorCancelButton != null)
        {
            errorCancelButton.onClick.AddListener(OnErrorCancelClicked);
        }

        if (otherIssueContainer != null)
        {
            otherIssueContainer.SetActive(false);
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        if (successPanel != null)
        {
            successPanel.SetActive(false);
        }

        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }
    }

    private void SetupIssueTypeDropdown()
    {
        if (issueTypeDropdown == null) return;

        issueTypeDropdown.ClearOptions();

        List<string> issueOptions = new List<string>
        {
            "Select issue type...",
            "Path blocked or closed",
            "Under construction/maintenance",
            "Flooded or damaged area",
            "Confusing or missing signs",
            "Incorrect route shown",
            "Safety concern",
            "Other"
        };

        issueTypeDropdown.AddOptions(issueOptions);
        issueTypeDropdown.value = 0;
    }

    private void SetupWhereIsIssueDropdown()
    {
        if (whereIsIssueDropdown == null) return;

        whereIsIssueDropdown.ClearOptions();

        List<string> whereOptions = new List<string>
        {
            "Select location type...",
            "Building/Location",
            "Walkway/Path"
        };

        whereIsIssueDropdown.AddOptions(whereOptions);
        whereIsIssueDropdown.value = 0;
    }

    private void OnIssueTypeChanged(int index)
    {
        if (issueTypeDropdown == null) return;

        string selectedOption = issueTypeDropdown.options[index].text;

        isOtherIssue = selectedOption == "Other";

        if (otherIssueContainer != null)
        {
            otherIssueContainer.SetActive(isOtherIssue);
        }

        if (isOtherIssue)
        {
            selectedIssueType = "";
        }
        else if (selectedOption != "Select issue type...")
        {
            selectedIssueType = selectedOption;
        }
        else
        {
            selectedIssueType = "";
        }
    }

    private void OnWhereIsIssueChanged(int index)
    {
        if (whereIsIssueDropdown == null) return;

        string selectedOption = whereIsIssueDropdown.options[index].text;

        if (selectedOption == "Select location type...")
        {
            selectedWhereType = "";
            if (whichOneDropdown != null)
            {
                whichOneDropdown.ClearOptions();
                whichOneDropdown.AddOptions(new List<string> { "Select location..." });
            }
            return;
        }

        selectedWhereType = selectedOption;

        if (affectedItemPopulator != null)
        {
            bool isNode = selectedOption == "Building/Location";
            affectedItemPopulator.PopulateAffectedItems(isNode);
        }
    }

    private void OnWhichOneChanged(int index)
    {
        if (whichOneDropdown == null) return;

        if (affectedItemPopulator != null)
        {
            selectedAffectedId = affectedItemPopulator.GetSelectedAffectedId(index);
        }
    }

    private void OnSubmitClicked()
    {
        if (!ValidateForm())
        {
            return;
        }

        string finalIssue = isOtherIssue ? otherIssueInputField.text.Trim() : selectedIssueType;

        if (reportFormPanel != null)
        {
            reportFormPanel.SetActive(false);
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        StartCoroutine(SubmitReportToFirebase(finalIssue));
    }

    private bool ValidateForm()
    {
        if (string.IsNullOrEmpty(selectedIssueType) && !isOtherIssue)
        {
            ShowError("Please select an issue type");
            return false;
        }

        if (isOtherIssue && string.IsNullOrWhiteSpace(otherIssueInputField.text))
        {
            ShowError("Please describe the issue");
            return false;
        }

        if (string.IsNullOrEmpty(selectedWhereType))
        {
            ShowError("Please select where the issue is located");
            return false;
        }

        if (string.IsNullOrEmpty(selectedAffectedId))
        {
            ShowError("Please select which location is affected");
            return false;
        }

        return true;
    }

    private IEnumerator SubmitReportToFirebase(string issue)
    {
        bool operationComplete = false;
        bool operationSuccess = false;
        string operationError = "";

        string typeValue = whereTypeToDatabase.ContainsKey(selectedWhereType)
            ? whereTypeToDatabase[selectedWhereType]
            : selectedWhereType;

        string description = "";
        if (isOtherIssue && otherIssueInputField != null)
        {
            description = otherIssueInputField.text.Trim();
        }

        DocumentReference docRef = db.Collection("UserReports").Document();

        Dictionary<string, object> reportData = new Dictionary<string, object>
    {
        { "affected", selectedAffectedId },
        { "createdAt", FieldValue.ServerTimestamp },
        { "date", Timestamp.GetCurrentTimestamp() },
        { "description", description },
        { "issue", issue },
        { "status", "Pending" },
        { "type", typeValue }
    };

        docRef.SetAsync(reportData).ContinueWithOnMainThread(task =>
        {
            operationComplete = true;

            if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
            {
                operationSuccess = true;
            }
            else if (task.IsFaulted)
            {
                operationError = task.Exception?.GetBaseException().Message ?? "Unknown error. Try Again.";
            }
            else if (task.IsCanceled)
            {
                operationError = "Operation was canceled";
            }
        });

        yield return new WaitUntil(() => operationComplete);

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        if (operationSuccess)
        {
            if (successPanel != null)
            {
                successPanel.SetActive(true);
            }
            ResetForm();
        }
        else
        {
            ShowError($"Failed to submit report: {operationError}");
        }
    }

    private void ShowError(string message)
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(true);
        }

        if (errorMessageText != null)
        {
            errorMessageText.text = message;
        }

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    private void OnRetryClicked()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }

        if (reportFormPanel != null)
        {
            reportFormPanel.SetActive(true);
        }
    }

    private void OnErrorCancelClicked()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }

        if (reportFormPanel != null)
        {
            reportFormPanel.SetActive(true);
        }

        ResetForm();
    }

    private void OnCancelClicked()
    {
        ResetForm();

        if (reportFormPanel != null)
        {
            reportFormPanel.SetActive(false);
        }
    }

    private void ResetForm()
    {
        if (issueTypeDropdown != null)
        {
            issueTypeDropdown.value = 0;
        }

        if (whereIsIssueDropdown != null)
        {
            whereIsIssueDropdown.value = 0;
        }

        if (whichOneDropdown != null)
        {
            whichOneDropdown.ClearOptions();
            whichOneDropdown.AddOptions(new List<string> { "Select location..." });
        }

        if (otherIssueInputField != null)
        {
            otherIssueInputField.text = "";
        }

        if (otherIssueContainer != null)
        {
            otherIssueContainer.SetActive(false);
        }

        selectedIssueType = "";
        selectedWhereType = "";
        selectedAffectedId = "";
        isOtherIssue = false;
    }

    void OnDestroy()
    {
        if (issueTypeDropdown != null)
            issueTypeDropdown.onValueChanged.RemoveListener(OnIssueTypeChanged);
        if (whereIsIssueDropdown != null)
            whereIsIssueDropdown.onValueChanged.RemoveListener(OnWhereIsIssueChanged);
        if (whichOneDropdown != null)
            whichOneDropdown.onValueChanged.RemoveListener(OnWhichOneChanged);
        if (submitButton != null)
            submitButton.onClick.RemoveListener(OnSubmitClicked);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        if (retryButton != null)
            retryButton.onClick.RemoveListener(OnRetryClicked);
        if (errorCancelButton != null)
            errorCancelButton.onClick.RemoveListener(OnErrorCancelClicked);
    }
}