using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Newtonsoft.Json;

public class MainAppLoader : MonoBehaviour
{
    [Header("Mapbox Offline")]
    public MapboxOfflineManager mapboxOffline;

    [Header("Loading UI")]
    public GameObject loadingPanel;
    public Image loadingBar;
    public TextMeshProUGUI loadingText;
    public TextMeshProUGUI progressText;

    [Header("Main App UI")]
    public GameObject mainAppUI;

    [Header("Error Handling")]
    public GameObject errorContainer;
    public Button retryButton;
    public TextMeshProUGUI errorText;
    public TextMeshProUGUI retryButtonText;
    public float maxWaitTimeForGlobalManager = 10f;

    public bool isInitialized = false;
    public bool hasError = false;
    private bool hasOfflineData = false;

    void Start()
    {
        bool skipFullInitialization = GlobalManager.ShouldSkipFullInitialization();
        
        if (skipFullInitialization)
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (mainAppUI != null) mainAppUI.SetActive(true);
            if (errorContainer != null) errorContainer.SetActive(false);
            
            isInitialized = true;
            return;
        }

        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (mainAppUI != null) mainAppUI.SetActive(false);
        if (errorContainer != null) errorContainer.SetActive(false);

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RetryInitialization);
        }

        StartCoroutine(InitializeApp());
    }

    public IEnumerator InitializeApp()
    {
        hasError = false;
        hasOfflineData = false;
        if (errorContainer != null) errorContainer.SetActive(false);

        UpdateLoadingUI("Starting app...", 0.1f);
        yield return new WaitForSeconds(0.5f);

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

        UpdateLoadingUI("Initializing files...", 0.25f);
        yield return new WaitForSeconds(0.2f);

        yield return StartCoroutine(EnsureManagersExist());

        bool hasInternet = Application.internetReachability != NetworkReachability.NotReachable;
        
        if (!hasInternet)
        {
            UpdateLoadingUI("No internet connection...", 0.3f);
            yield return new WaitForSeconds(0.5f);
            
            hasOfflineData = CheckForOfflineData();
            
            if (hasOfflineData)
            {
                UpdateLoadingUI("Loading offline data...", 0.5f);
                yield return StartCoroutine(LoadFromOfflineData());
                
                if (GlobalManager.Instance != null && GlobalManager.Instance.availableMaps != null && GlobalManager.Instance.availableMaps.Count > 0)
                {
                    ShowOfflineWarning();
                    yield break;
                }
                else
                {
                    ShowError("Failed to load offline data. Please connect to the internet.", false);
                    yield break;
                }
            }
            else
            {
                ShowError("No internet connection. Connect to the internet to download map data for the first time.", false);
                yield break;
            }
        }

        UpdateLoadingUI("Checking map system...", 0.25f);
        yield return new WaitForSeconds(0.2f);

        if (mapboxOffline != null)
        {
            UpdateLoadingUI("Map system ready!", 0.35f);
        }
        else
        {
            UpdateLoadingUI("Map ready...", 0.35f);
        }
        
        yield return new WaitForSeconds(0.3f);

        UpdateLoadingUI("Setting up data systems...", 0.4f);
        yield return new WaitForSeconds(0.3f);

        bool dataInitComplete = false;

        System.Action onComplete = () => { dataInitComplete = true; };
        GlobalManager.Instance.OnDataInitializationComplete += onComplete;

        try
        {
            UpdateLoadingUI("Creating managers...", 0.5f);
            GlobalManager.Instance.InitializeDataSystems();
        }
        catch (System.Exception e)
        {
            GlobalManager.Instance.OnDataInitializationComplete -= onComplete;
            ShowError($"Failed to load map data: {e.Message}. Check your internet connection.", false);
            yield break;
        }

        float initWaitTime = 0f;
        float maxInitWaitTime = 30f;
        float lastProgress = 0.5f;

        while (!dataInitComplete && initWaitTime < maxInitWaitTime)
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                GlobalManager.Instance.OnDataInitializationComplete -= onComplete;
                
                hasOfflineData = CheckForOfflineData();
                
                if (hasOfflineData)
                {
                    UpdateLoadingUI("Internet lost. Loading offline data...", 0.5f);
                    yield return StartCoroutine(LoadFromOfflineData());
                    
                    if (GlobalManager.Instance.availableMaps != null && GlobalManager.Instance.availableMaps.Count > 0)
                    {
                        ShowOfflineWarning();
                        yield break;
                    }
                }
                
                ShowError("Internet connection lost during loading. Please check your connection and retry.", false);
                yield break;
            }
            
            initWaitTime += Time.deltaTime;

            float timeProgress = initWaitTime / maxInitWaitTime;
            float currentProgress = 0.5f + (timeProgress * 0.4f);

            if (currentProgress < 0.6f && lastProgress < 0.6f)
            {
                UpdateLoadingUI("Initializing app files...", currentProgress);
            }
            else if (currentProgress < 0.7f && lastProgress < 0.7f)
            {
                UpdateLoadingUI("Connecting to Firebase...", currentProgress);
            }
            else if (currentProgress < 0.85f && lastProgress < 0.85f)
            {
                UpdateLoadingUI("Syncing map data...", currentProgress);
            }
            else
            {
                UpdateLoadingUI("Loading map data...", currentProgress);
            }

            lastProgress = currentProgress;
            yield return new WaitForSeconds(0.1f);
        }

        GlobalManager.Instance.OnDataInitializationComplete -= onComplete;

        if (!dataInitComplete)
        {
            ShowError("Data initialization timed out. Check your internet connection.", false);
            yield break;
        }
        
        if (GlobalManager.Instance.availableMaps == null || GlobalManager.Instance.availableMaps.Count == 0)
        {
            ShowError("Failed to load map data from Firebase. Check your internet connection.", false);
            yield break;
        }
        
        SaveMapIdsToPlayerPrefs(GlobalManager.Instance.availableMaps);

        UpdateLoadingUI("Finalizing...", 0.95f);
        yield return new WaitForSeconds(0.5f);

        UpdateLoadingUI("Ready!", 1.0f);
        yield return new WaitForSeconds(0.5f);

        if (loadingPanel != null) loadingPanel.SetActive(false);
        if (mainAppUI != null) mainAppUI.SetActive(true);

        isInitialized = true;
    }

    private bool CheckForOfflineData()
    {
        bool hasJsonData = false;
        
        if (JSONFileManager.Instance != null)
        {
            string mapsJson = JSONFileManager.Instance.ReadJSONFile("maps.json");
            
            if (!string.IsNullOrEmpty(mapsJson) && mapsJson.Trim() != "[]")
            {
                hasJsonData = true;
            }
            
            if (hasJsonData)
            {
                string[] requiredFiles = { 
                    "categories.json", 
                    "infrastructure.json", 
                    "campus.json",
                    "indoor.json"
                };
                
                foreach (string file in requiredFiles)
                {
                    string content = JSONFileManager.Instance.ReadJSONFile(file);
                    if (string.IsNullOrEmpty(content) || content.Trim() == "[]")
                    {
                        hasJsonData = false;
                        break;
                    }
                }
            }
        }
        
        return hasJsonData;
    }
    
    private IEnumerator LoadFromOfflineData()
    {
        float waitTime = 0f;
        while (GlobalManager.Instance == null && waitTime < maxWaitTimeForGlobalManager)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }
        
        if (GlobalManager.Instance == null)
        {
            yield break;
        }
        
        if (JSONFileManager.Instance != null)
        {
            string mapsJson = JSONFileManager.Instance.ReadJSONFile("maps.json");
            if (!string.IsNullOrEmpty(mapsJson))
            {
                try
                {
                    var mapsArray = JsonConvert.DeserializeObject<List<MapInfo>>(mapsJson);
                    if (mapsArray != null && mapsArray.Count > 0)
                    {
                        GlobalManager.Instance.availableMaps = mapsArray;
                        GlobalManager.Instance.isDataInitialized = true;
                        
                        GlobalManager.Instance.UpdateCurrentMapVersions();
                    }
                }
                catch (System.Exception)
                {
                }
            }
        }
        
        yield return new WaitForSeconds(0.5f);
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
        
        if (FirestoreManager.Instance == null)
        {
            GameObject firestoreManager = new GameObject("FirestoreManager");
            firestoreManager.AddComponent<FirestoreManager>();
            DontDestroyOnLoad(firestoreManager);
            
            yield return new WaitUntil(() => FirestoreManager.Instance != null);
        }
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
            loadingBar.fillAmount = progress;

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
    }

    private void ShowError(string message, bool showRestartMessage = false)
    {
        hasError = true;

        if (errorContainer != null)
            errorContainer.SetActive(true);

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
    
    private void ShowOfflineWarning()
    {
        hasError = true;

        if (errorContainer != null)
            errorContainer.SetActive(true);

        if (errorText != null)
            errorText.text = "You have no Internet Connection but saved data is available. This may not be the latest data. Proceed with caution.";

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(true);
            
            if (retryButtonText != null)
            {
                retryButtonText.text = "Confirm";
            }
        }

        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        if (progressText != null) progressText.gameObject.SetActive(false);
        if (loadingText != null)
            loadingText.text = "Offline Mode";
    }

    public void RetryInitialization()
    {
        if (!hasError)
        {
            return;
        }
        
        if (hasOfflineData && retryButtonText != null && retryButtonText.text == "Confirm")
        {
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (errorContainer != null) errorContainer.SetActive(false);
            if (mainAppUI != null) mainAppUI.SetActive(true);
            
            isInitialized = true;
            hasError = false;
            return;
        }

        isInitialized = false;
        hasError = false;
        hasOfflineData = false;
        
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (errorContainer != null) errorContainer.SetActive(false);
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
        hasOfflineData = false;
        StopAllCoroutines();
    }
}