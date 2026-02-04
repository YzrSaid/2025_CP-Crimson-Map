using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Newtonsoft.Json;
using DG.Tweening;

/// <summary>
/// FULLY OFFLINE MainAppLoader - no internet/Firebase checks
/// </summary>
public class MainAppLoader : MonoBehaviour
{
    [Header("Mapbox Offline")]
    public MapboxOfflineManager mapboxOffline;

    [Header("Loading UI")]
    public GameObject loadingPanel;
    public Slider loadingBar;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI progressText;

    [Header("Main App UI")]
    public GameObject mainAppUI;

    [Header("Error Handling")]
    public GameObject errorBGPanel;
    public GameObject errorContainer;
    public float errorContainerPanelAnimDuration = 0.3f;
    public Ease errorContainerPanelEaseType = Ease.OutBack;
    private Vector3 errorContainerPanelOriginalScale;
    public Button retryButton;
    public TextMeshProUGUI errorText;
    public TextMeshProUGUI retryButtonText;
    public float maxWaitTimeForGlobalManager = 10f;

    public bool isInitialized = false;
    public bool hasError = false;

    void Start()
    {
        bool skipFullInitialization = GlobalManager.ShouldSkipFullInitialization();

        if (skipFullInitialization)
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (mainAppUI != null) mainAppUI.SetActive(true);
            if (errorContainer != null) errorContainer.SetActive(false);
            if (errorBGPanel != null) errorBGPanel.SetActive(false);

            isInitialized = true;
            return;
        }

        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (mainAppUI != null) mainAppUI.SetActive(false);
        if (errorContainer != null) errorContainer.SetActive(false);
        if (errorBGPanel != null) errorBGPanel.SetActive(false);

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RetryInitialization);
        }

        StartCoroutine(InitializeApp());
    }

    public IEnumerator InitializeApp()
    {
        hasError = false;

        if (errorContainer != null)
        {
            errorContainerPanelOriginalScale = errorContainer.transform.localScale;
            errorContainer.SetActive(false);
        }

        if (errorBGPanel != null) errorBGPanel.SetActive(false);

        UpdateLoadingUI("Starting app...", 0.1f);
        yield return new WaitForSeconds(0.3f);

        UpdateLoadingUI("Waiting for system...", 0.2f);

        float waitTime = 0f;
        while (GlobalManager.Instance == null && waitTime < maxWaitTimeForGlobalManager)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        if (GlobalManager.Instance == null)
        {
            ShowError("Failed to initialize system. Please restart the app.", true);
            yield break;
        }

        if (!GlobalManager.Instance.onboardingComplete)
        {
            SceneManager.LoadScene("OnboardingScreensScene");
            yield break;
        }

        UpdateLoadingUI("Setting up files...", 0.3f);
        yield return StartCoroutine(EnsureManagersExist());

        UpdateLoadingUI("Loading map data...", 0.5f);
        yield return new WaitForSeconds(0.3f);

        bool dataInitComplete = false;
        System.Action onComplete = () => { dataInitComplete = true; };
        GlobalManager.Instance.OnDataInitializationComplete += onComplete;

        try
        {
            UpdateLoadingUI("Initializing data...", 0.6f);
            GlobalManager.Instance.InitializeDataSystems();
        }
        catch (System.Exception e)
        {
            GlobalManager.Instance.OnDataInitializationComplete -= onComplete;
            ShowError($"Failed to load data: {e.Message}", false);
            yield break;
        }

        float initWaitTime = 0f;
        float maxInitWaitTime = 15f;

        while (!dataInitComplete && initWaitTime < maxInitWaitTime)
        {
            initWaitTime += Time.deltaTime;

            float timeProgress = initWaitTime / maxInitWaitTime;
            float currentProgress = 0.6f + (timeProgress * 0.3f);

            UpdateLoadingUI("Loading maps...", currentProgress);
            yield return new WaitForSeconds(0.1f);
        }

        GlobalManager.Instance.OnDataInitializationComplete -= onComplete;

        if (!dataInitComplete)
        {
            ShowError("Data initialization timed out. Please restart the app.", false);
            yield break;
        }

        if (GlobalManager.Instance.availableMaps == null || GlobalManager.Instance.availableMaps.Count == 0)
        {
            ShowError("No map data found. Please reinstall the app.", true);
            yield break;
        }

        SaveMapIdsToPlayerPrefs(GlobalManager.Instance.availableMaps);

        UpdateLoadingUI("Almost ready...", 0.95f);
        yield return new WaitForSeconds(0.5f);

        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (mainAppUI != null) mainAppUI.SetActive(true);

        isInitialized = true;
        Debug.Log("[MainAppLoader] ✅ App initialized successfully!");
    }

    private IEnumerator EnsureManagersExist()
    {
        if (JSONFileManager.Instance == null)
        {
            GameObject jsonManager = new GameObject("JSONFileManager");
            jsonManager.AddComponent<JSONFileManager>();
            DontDestroyOnLoad(jsonManager);

            yield return new WaitUntil(() => JSONFileManager.Instance != null);
        }

        bool jsonInitComplete = false;
        JSONFileManager.Instance.InitializeJSONFiles(() => jsonInitComplete = true);
        yield return new WaitUntil(() => jsonInitComplete);
    }

    private void SaveMapIdsToPlayerPrefs(List<MapInfo> maps)
    {
        if (maps == null || maps.Count == 0) return;

        List<string> mapIds = new List<string>();
        foreach (MapInfo map in maps)
        {
            mapIds.Add(map.map_id);
        }

        PlayerPrefs.SetString("FetchedMapIds", string.Join(",", mapIds));
        PlayerPrefs.Save();
    }

    private void UpdateLoadingUI(string message, float progress)
    {
        if (loadingText != null)
            loadingText.text = message;

        if (loadingBar != null)
            loadingBar.value = progress;

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
    }

    private void ShowError(string message, bool showRestartMessage = false)
    {
        hasError = true;

        if (errorBGPanel != null)
            errorBGPanel.SetActive(true);

        if (errorContainer != null)
        {
            errorContainer.SetActive(true);
            errorContainer.transform.localScale = Vector3.zero;

            errorContainer.transform.DOScale(errorContainerPanelOriginalScale, errorContainerPanelAnimDuration)
                .SetEase(errorContainerPanelEaseType)
                .SetUpdate(true);
        }

        if (errorText != null)
            errorText.text = message;

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(!showRestartMessage);

            if (retryButtonText != null)
            {
                retryButtonText.text = "Retry";
            }
        }

        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        if (progressText != null) progressText.gameObject.SetActive(false);
        if (loadingText != null)
            loadingText.text = showRestartMessage ? "Please restart the app" : "Tap retry to try again";
    }

    public void RetryInitialization()
    {
        if (!hasError)
        {
            return;
        }

        isInitialized = false;
        hasError = false;

        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (errorContainer != null) errorContainer.SetActive(false);
        if (errorBGPanel != null) errorBGPanel.SetActive(false);
        if (loadingBar != null) loadingBar.gameObject.SetActive(true);
        if (progressText != null) progressText.gameObject.SetActive(true);

        if (retryButtonText != null)
        {
            retryButtonText.text = "Retry";
        }

        StopAllCoroutines();
        StartCoroutine(InitializeApp());
    }

    void OnDestroy()
    {
        if (GlobalManager.Instance != null && GlobalManager.Instance.OnDataInitializationComplete != null)
        {
            System.Delegate[] invocationList = GlobalManager.Instance.OnDataInitializationComplete.GetInvocationList();
            foreach (System.Action action in invocationList)
            {
                GlobalManager.Instance.OnDataInitializationComplete -= action;
            }
        }
    }

    public void ResetForReload()
    {
        isInitialized = false;
        hasError = false;
        StopAllCoroutines();
    }
}