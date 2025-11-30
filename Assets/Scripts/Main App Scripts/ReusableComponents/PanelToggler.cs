using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using DG.Tweening;

[RequireComponent(typeof(Button))]
public class PanelToggler : MonoBehaviour
{
    [Header("Panels To Toggle")]
    public List<GameObject> panelsToToggle = new List<GameObject>();

    [Header("Category Panel Mode")]
    public bool isCategoryPanelToggler = false;
    public GameObject outdoorPanel;
    public GameObject outdoorPanelContent;
    public GameObject indoorPanel;
    public GameObject scrollviewOutdoor;
    public GameObject viewportOutdoor;

    private Button button;
    private Dictionary<GameObject, Vector3> originalScales = new Dictionary<GameObject, Vector3>();

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(TogglePanels);

        foreach (var panel in panelsToToggle)
        {
            if (panel != null && !originalScales.ContainsKey(panel))
            {
                originalScales.Add(panel, panel.transform.localScale);
            }
        }

        if (isCategoryPanelToggler)
        {
            if (outdoorPanel != null && !originalScales.ContainsKey(outdoorPanel))
            {
                originalScales.Add(outdoorPanel, outdoorPanel.transform.localScale);
            }
            if (scrollviewOutdoor != null && !originalScales.ContainsKey(scrollviewOutdoor))
            {
                originalScales.Add(scrollviewOutdoor, scrollviewOutdoor.transform.localScale);
            }
            if (viewportOutdoor != null && !originalScales.ContainsKey(viewportOutdoor))
            {
                originalScales.Add(viewportOutdoor, viewportOutdoor.transform.localScale);
            }
            if (outdoorPanelContent != null && !originalScales.ContainsKey(outdoorPanelContent))
            {
                originalScales.Add(outdoorPanelContent, outdoorPanelContent.transform.localScale);
            }
            if (indoorPanel != null && !originalScales.ContainsKey(indoorPanel))
            {
                originalScales.Add(indoorPanel, indoorPanel.transform.localScale);
            }
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(TogglePanels);
        }
    }

    private void TogglePanels()
    {
        if (isCategoryPanelToggler)
        {
            ToggleCategoryPanels();
            return;
        }

        foreach (GameObject panel in panelsToToggle)
        {
            if (panel != null)
            {
                if (panel.activeSelf)
                {
                    panel.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                        .OnComplete(() => panel.SetActive(false));
                }
                else
                {
                    panel.SetActive(true);
                    panel.transform.localScale = Vector3.zero;
                    panel.transform.DOScale(originalScales[panel], 0.18f).SetEase(Ease.OutBack);
                }
            }
        }
    }

    private void ToggleCategoryPanels()
    {
        bool isIndoorMode = IsIndoorMode();

        if (isIndoorMode)
        {
            if (indoorPanel != null)
            {
                if (indoorPanel.activeSelf)
                {
                    indoorPanel.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                        .OnComplete(() => indoorPanel.SetActive(false));
                }
                else
                {
                    indoorPanel.SetActive(true);
                    indoorPanel.transform.localScale = Vector3.zero;
                    indoorPanel.transform.DOScale(originalScales[indoorPanel], 0.18f).SetEase(Ease.OutBack);
                }
            }

            if (outdoorPanel != null && outdoorPanel.activeSelf)
            {
                outdoorPanel.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                    .OnComplete(() => outdoorPanel.SetActive(false));
            }
            if (scrollviewOutdoor != null && scrollviewOutdoor.activeSelf)
            {
                scrollviewOutdoor.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                    .OnComplete(() => scrollviewOutdoor.SetActive(false));
            }
            if (viewportOutdoor != null && viewportOutdoor.activeSelf)
            {
                viewportOutdoor.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                    .OnComplete(() => viewportOutdoor.SetActive(false));
            }
            if (outdoorPanelContent != null && outdoorPanelContent.activeSelf)
            {
                outdoorPanelContent.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                    .OnComplete(() => outdoorPanelContent.SetActive(false));
            }
        }
        else
        {
            if (outdoorPanel != null)
            {
                if (outdoorPanel.activeSelf)
                {
                    outdoorPanel.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                        .OnComplete(() => outdoorPanel.SetActive(false));
                }
                else
                {
                    outdoorPanel.SetActive(true);
                    outdoorPanel.transform.localScale = Vector3.zero;
                    outdoorPanel.transform.DOScale(originalScales[outdoorPanel], 0.18f).SetEase(Ease.OutBack);
                }
            }
            if (scrollviewOutdoor != null)
            {
                if (scrollviewOutdoor.activeSelf)
                {
                    scrollviewOutdoor.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                        .OnComplete(() => scrollviewOutdoor.SetActive(false));
                }
                else
                {
                    scrollviewOutdoor.SetActive(true);
                    scrollviewOutdoor.transform.localScale = Vector3.zero;
                    scrollviewOutdoor.transform.DOScale(originalScales[scrollviewOutdoor], 0.18f).SetEase(Ease.OutBack);
                }
            }
            if (viewportOutdoor != null)
            {
                if (viewportOutdoor.activeSelf)
                {
                    viewportOutdoor.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                        .OnComplete(() => viewportOutdoor.SetActive(false));
                }
                else
                {
                    viewportOutdoor.SetActive(true);
                    viewportOutdoor.transform.localScale = Vector3.zero;
                    viewportOutdoor.transform.DOScale(originalScales[viewportOutdoor], 0.18f).SetEase(Ease.OutBack);
                }
            }
            if (outdoorPanelContent != null)
            {
                if (outdoorPanelContent.activeSelf)
                {
                    outdoorPanelContent.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                        .OnComplete(() => outdoorPanelContent.SetActive(false));
                }
                else
                {
                    outdoorPanelContent.SetActive(true);
                    outdoorPanelContent.transform.localScale = Vector3.zero;
                    outdoorPanelContent.transform.DOScale(originalScales[outdoorPanelContent], 0.18f).SetEase(Ease.OutBack);
                }
            }

            if (indoorPanel != null && indoorPanel.activeSelf)
            {
                indoorPanel.transform.DOScale(Vector3.zero, 0.18f).SetEase(Ease.InBack)
                    .OnComplete(() => indoorPanel.SetActive(false));
            }
        }
    }

    private bool IsIndoorMode()
    {
        if (MapModeController.Instance != null)
        {
            return MapModeController.Instance.IsIndoorMode();
        }

        if (ARMapModeController.Instance != null)
        {
            return ARMapModeController.Instance.IsIndoorMode();
        }

        return false;
    }
}