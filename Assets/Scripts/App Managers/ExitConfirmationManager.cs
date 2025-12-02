using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;

public class ExitConfirmationManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject confirmExitPanel;
    public GameObject backgroundPanel;
    
    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;
    
    [Header("Animation Settings")]
    public float animationDuration = 0.3f;
    public Ease openEaseType = Ease.OutBack;
    public Ease closeEaseType = Ease.InBack;
    
    private Vector3 originalPanelScale;
    private bool isPanelOpen = false;
    
    private InputAction backButtonAction;
    
    void Awake()
    {
        if (confirmExitPanel != null)
        {
            originalPanelScale = confirmExitPanel.transform.localScale;
            confirmExitPanel.SetActive(false);
        }
        
        if (backgroundPanel != null)
        {
            backgroundPanel.SetActive(false);
        }
        
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmExit);
        }
        
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelExit);
        }
        
        backButtonAction = new InputAction(binding: "<Keyboard>/p"); 
        
        #if UNITY_ANDROID
        backButtonAction.AddBinding("<Keyboard>/escape");
        #endif
        
        backButtonAction.Enable();
    }
    
    void Update()
    {
        if (backButtonAction.triggered && !isPanelOpen)
        {
            ShowExitConfirmation();
        }
    }
    
    public void ShowExitConfirmation()
    {
        if (isPanelOpen) return;
        
        isPanelOpen = true;
        
        if (backgroundPanel != null)
        {
            backgroundPanel.SetActive(true);
        }
        
        if (confirmExitPanel != null)
        {
            confirmExitPanel.SetActive(true);
            confirmExitPanel.transform.localScale = Vector3.zero;
            
            confirmExitPanel.transform.DOScale(originalPanelScale, animationDuration)
                .SetEase(openEaseType)
                .SetUpdate(true);
        }
    }
    
    private void OnConfirmExit()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
    
    private void OnCancelExit()
    {
        if (!isPanelOpen) return;
        if (confirmExitPanel != null)
        {
            confirmExitPanel.transform.DOScale(Vector3.zero, animationDuration)
                .SetEase(closeEaseType)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    confirmExitPanel.SetActive(false);
                    
                    if (backgroundPanel != null)
                    {
                        backgroundPanel.SetActive(false);
                    }
                    
                    isPanelOpen = false;
                });
        }
    }
    
    void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmExit);
        }
        
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelExit);
        }
        backButtonAction?.Disable();
    }
}