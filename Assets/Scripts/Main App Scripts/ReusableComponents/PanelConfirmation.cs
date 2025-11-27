using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using DG.Tweening;

public class PanelConfirmation : MonoBehaviour
{
    [Header("Confirmation Panel References")]
    public GameObject panelContainer;
    public GameObject backgroundPanel;
    public TextMeshProUGUI messageText;
    
    [Header("Panel Buttons")]
    public Button confirmButton;
    public Button cancelButton;
    
    [Header("Confirmation Settings")]
    [TextArea(3, 5)]
    public string confirmationMessage = "Are you sure?";
    public string confirmButtonText = "Yes";
    public string cancelButtonText = "No";
    
    [Header("Animation Settings")]
    public float animationDuration = 0.3f;
    public Ease showEase = Ease.OutBack;
    public Ease hideEase = Ease.InBack;
    
    [Header("Actions")]
    public UnityEvent onConfirmed;
    public UnityEvent onCancelled;
    
    private Vector3 originalScale;
    private Button thisButton;

    void Start()
    {
        thisButton = GetComponent<Button>();
        
        if (thisButton != null)
        {
            thisButton.onClick.AddListener(ShowConfirmation);
        }

        if (panelContainer != null)
        {
            originalScale = panelContainer.transform.localScale;
            panelContainer.SetActive(false);
        }

        if (backgroundPanel != null)
        {
            backgroundPanel.SetActive(false);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    private void ShowConfirmation()
    {
        // Set the message
        if (messageText != null)
        {
            messageText.text = confirmationMessage;
        }

        // Set button texts
        TextMeshProUGUI confirmText = confirmButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (confirmText != null)
        {
            confirmText.text = confirmButtonText;
        }

        TextMeshProUGUI cancelText = cancelButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (cancelText != null)
        {
            cancelText.text = cancelButtonText;
        }

        // Show background
        if (backgroundPanel != null)
        {
            backgroundPanel.SetActive(true);
        }

        // Animate panel
        if (panelContainer != null)
        {
            panelContainer.SetActive(true);
            panelContainer.transform.localScale = Vector3.zero;
            panelContainer.transform.DOScale(originalScale, animationDuration)
                .SetEase(showEase)
                .SetUpdate(true);
        }
    }

    private void OnConfirmClicked()
    {
        HidePanel();
        onConfirmed?.Invoke();
    }

    private void OnCancelClicked()
    {
        HidePanel();
        onCancelled?.Invoke();
    }

    private void HidePanel()
    {
        if (panelContainer != null)
        {
            panelContainer.transform.DOScale(Vector3.zero, animationDuration)
                .SetEase(hideEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    panelContainer.SetActive(false);

                    if (backgroundPanel != null)
                    {
                        backgroundPanel.SetActive(false);
                    }

                    panelContainer.transform.localScale = originalScale;
                });
        }
    }

    void OnDestroy()
    {
        if (thisButton != null)
        {
            thisButton.onClick.RemoveListener(ShowConfirmation);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        }
    }
}