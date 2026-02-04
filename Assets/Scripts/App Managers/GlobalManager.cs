using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Android;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine.SceneManagement;

public class GlobalManager : MonoBehaviour
{
    public static GlobalManager Instance { get; private set; }

    [Header("AR Scene Compatibility")]
    public bool isInARMode = false;
    private bool wasInARMode = false;
    private bool hasInitialized = false;

    public bool onboardingComplete = false;
    public bool isDataInitialized = false;
    public List<MapInfo> availableMaps = new List<MapInfo>();

    public GameObject jsonFileManagerPrefab;

    private string onboardingSavePath;
    private static bool skipFullInitializationOnReturn = false;

    public System.Action OnDataInitializationComplete;
    public System.Action<List<MapInfo>> OnAvailableMapsChanged;

    void Start()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            onboardingSavePath = Path.Combine(Application.persistentDataPath, "saveData.json");
            if (Application.isFocused)
            {
                hasInitialized = true;
                CheckOnboardingAndNavigate();
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && Instance == this)
        {
            if (!hasInitialized)
            {
                CheckOnboardingAndNavigate();
            }
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private bool IsARScene(string sceneName)
    {
        string[] arScenes = { "ARScene", "ReadQRCode" };
        return System.Array.Exists(arScenes, scene =>
            sceneName.Equals(scene, System.StringComparison.OrdinalIgnoreCase));
    }

    public static void SetSkipFullInitialization(bool skip)
    {
        skipFullInitializationOnReturn = skip;
    }

    public static bool ShouldSkipFullInitialization()
    {
        return skipFullInitializationOnReturn;
    }

    public void InitializeDataSystems()
    {
        if (isInARMode)
        {
            OnDataInitializationComplete?.Invoke();
            return;
        }

        if (isDataInitialized)
        {
            OnDataInitializationComplete?.Invoke();
            return;
        }

        if (skipFullInitializationOnReturn)
        {
            StartCoroutine(QuickInitializationFromAR());
        }
        else
        {
            StartCoroutine(FullInitializationFromScratch());
        }
    }

    private IEnumerator QuickInitializationFromAR()
    {
        skipFullInitializationOnReturn = false;

        if (JSONFileManager.Instance == null)
        {
            yield return StartCoroutine(RecreateJSONManager());
        }

        isDataInitialized = true;
        OnDataInitializationComplete?.Invoke();
    }

    private IEnumerator FullInitializationFromScratch()
    {
        Debug.Log("[GlobalManager] Starting OFFLINE initialization...");

        if (JSONFileManager.Instance == null)
        {
            yield return StartCoroutine(RecreateJSONManager());
        }

        bool jsonInitComplete = false;
        JSONFileManager.Instance.InitializeJSONFiles(() =>
        {
            jsonInitComplete = true;
        });
        yield return new WaitUntil(() => jsonInitComplete);

        Debug.Log("[GlobalManager] JSON files loaded!");

        LoadAvailableMaps();

        FinalizeDataInitialization();
    }

    private IEnumerator RecreateJSONManager()
    {
        GameObject jsonManager;
        if (jsonFileManagerPrefab != null)
        {
            jsonManager = Instantiate(jsonFileManagerPrefab);
            if (jsonManager.GetComponent<JSONFileManager>() == null)
            {
                jsonManager.AddComponent<JSONFileManager>();
            }
        }
        else
        {
            jsonManager = new GameObject("JSONFileManager");
            jsonManager.AddComponent<JSONFileManager>();
        }

        DontDestroyOnLoad(jsonManager);
        yield return new WaitUntil(() => JSONFileManager.Instance != null);
    }

    private void CheckOnboardingAndNavigate()
    {
        LoadOnboardingData();

        if (!onboardingComplete)
        {
            SceneManager.LoadScene("OnboardingScreensScene");
        }
        else
        {
            SceneManager.LoadScene("MainAppScene");
        }
    }

    private void FinalizeDataInitialization()
    {
        isDataInitialized = true;
        Debug.Log("[GlobalManager] ✅ Data initialization complete!");
        OnDataInitializationComplete?.Invoke();
    }

    private void LoadAvailableMaps()
    {
        availableMaps.Clear();

        if (JSONFileManager.Instance != null)
        {
            string mapsJson = JSONFileManager.Instance.ReadJSONFile("maps.json");
            if (!string.IsNullOrEmpty(mapsJson))
            {
                try
                {
                    var mapsArray = JsonConvert.DeserializeObject<List<MapInfo>>(mapsJson);
                    availableMaps.AddRange(mapsArray);
                    Debug.Log($"[GlobalManager] Loaded {availableMaps.Count} maps");
                    OnAvailableMapsChanged?.Invoke(availableMaps);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GlobalManager] Error loading maps: {e.Message}");
                }
            }
        }
    }

    private void LoadOnboardingData()
    {
        if (File.Exists(onboardingSavePath))
        {
            try
            {
                string json = File.ReadAllText(onboardingSavePath);
                if (!string.IsNullOrEmpty(json.Trim()))
                {
                    SaveData data = JsonUtility.FromJson<SaveData>(json);
                    this.onboardingComplete = data.onboardingComplete;
                }
                else
                {
                    this.onboardingComplete = false;
                }
            }
            catch (System.Exception)
            {
                this.onboardingComplete = false;
            }
        }
        else
        {
            this.onboardingComplete = false;
        }
    }

    public void SaveOnboardingData()
    {
        try
        {
            SaveData data = new SaveData();
            data.onboardingComplete = this.onboardingComplete;

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(onboardingSavePath, json);
        }
        catch (System.Exception)
        {
        }
    }

    public string GetSystemStatus()
    {
        string status = "=== CRIMSON MAP SYSTEM STATUS (OFFLINE) ===\n";
        status += $"Data Initialized: {isDataInitialized}\n";
        status += $"Available Maps: {availableMaps.Count}\n";

        foreach (var map in availableMaps)
        {
            status += $"  - {map.map_id} ({map.map_name})\n";
        }

        status += $"JSON Manager Ready: {JSONFileManager.Instance != null}\n";
        status += $"Onboarding Complete: {onboardingComplete}\n";
        status += $"System Ready: {IsSystemReady()}";

        return status;
    }

    public string GetJSONData(string fileName)
    {
        if (JSONFileManager.Instance != null)
        {
            return JSONFileManager.Instance.ReadJSONFile(fileName);
        }
        return null;
    }

    public void SaveJSONData(string fileName, string jsonContent)
    {
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.WriteJSONFile(fileName, jsonContent);
        }
    }

    public string GetMapSpecificData(string collectionName, string mapId)
    {
        if (JSONFileManager.Instance != null)
        {
            return JSONFileManager.Instance.ReadMapSpecificData(collectionName, mapId);
        }
        return null;
    }

    public List<MapInfo> GetAvailableMaps()
    {
        return new List<MapInfo>(availableMaps);
    }

    public void AddToRecentDestinations(Dictionary<string, object> destination)
    {
        if (JSONFileManager.Instance != null)
        {
            JSONFileManager.Instance.AddRecentDestination(destination);
        }
    }

    public bool IsSystemReady()
    {
        return isDataInitialized && JSONFileManager.Instance != null;
    }

    public string GetComprehensiveStatus()
    {
        string systemStatus = GetSystemStatus();

        if (JSONFileManager.Instance != null)
        {
            systemStatus += "\n\n" + JSONFileManager.Instance.GetFileSystemStatus();
        }

        return systemStatus;
    }

    private IEnumerator CleanupXRSubsystems()
    {
        List<UnityEngine.XR.ARSubsystems.XRSessionSubsystem> sessionSubsystems = null;
        List<UnityEngine.XR.ARSubsystems.XRPlaneSubsystem> planeSubsystems = null;
        List<UnityEngine.XR.ARSubsystems.XRRaycastSubsystem> raycastSubsystems = null;

        try
        {
            sessionSubsystems = new List<UnityEngine.XR.ARSubsystems.XRSessionSubsystem>();
            planeSubsystems = new List<UnityEngine.XR.ARSubsystems.XRPlaneSubsystem>();
            raycastSubsystems = new List<UnityEngine.XR.ARSubsystems.XRRaycastSubsystem>();

            SubsystemManager.GetInstances(sessionSubsystems);
            SubsystemManager.GetInstances(planeSubsystems);
            SubsystemManager.GetInstances(raycastSubsystems);
        }
        catch (System.Exception)
        {
        }

        if (sessionSubsystems != null)
        {
            foreach (var subsystem in sessionSubsystems)
            {
                if (subsystem.running)
                {
                    subsystem.Stop();
                }
            }
        }

        yield return new WaitForSeconds(0.1f);

        if (planeSubsystems != null)
        {
            foreach (var subsystem in planeSubsystems)
            {
                if (subsystem.running)
                {
                    subsystem.Stop();
                }
            }
        }

        if (raycastSubsystems != null)
        {
            foreach (var subsystem in raycastSubsystems)
            {
                if (subsystem.running)
                {
                    subsystem.Stop();
                }
            }
        }

        yield return new WaitForSeconds(0.1f);
    }

    public IEnumerator SafeARCleanupAndExit(string sceneName)
    {
        yield return StartCoroutine(CleanupXRSubsystems());
        yield return new WaitForSeconds(0.2f);

        UnifiedARManager arManager = FindObjectOfType<UnifiedARManager>();
        if (arManager != null)
        {
            Destroy(arManager.gameObject);
        }

        yield return new WaitForSeconds(0.1f);

        if (isInARMode)
        {
            yield return StartCoroutine(ManuallyRecreateManagers(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    public IEnumerator ManuallyRecreateManagers(string targetScene)
    {
        isInARMode = false;

        MonoBehaviour[] arComponents = FindObjectsOfType<MonoBehaviour>();
        foreach (MonoBehaviour component in arComponents)
        {
            if (component != null && component != this)
            {
                if (component.name.Contains("AR") ||
                    component.GetType().Namespace?.Contains("UnityEngine.XR") == true ||
                    component.GetType().Name.Contains("AR"))
                {
                    component.StopAllCoroutines();
                }
            }
        }

        yield return new WaitForEndOfFrame();

        bool managersRecreated = false;
        yield return StartCoroutine(RecreateDestroyedManagersCoroutine((success) => managersRecreated = success));

        SetSkipFullInitialization(true);
        SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
    }

    private IEnumerator RecreateDestroyedManagersCoroutine(Action<bool> onComplete)
    {
        bool success = true;
        bool shouldRecreateJSON = false;

        try
        {
            shouldRecreateJSON = ARManagerCleanup.ShouldRecreateJSONManager() && JSONFileManager.Instance == null;
        }
        catch (System.Exception)
        {
            success = false;
        }

        if (shouldRecreateJSON)
        {
            try
            {
                GameObject jsonManager;
                if (jsonFileManagerPrefab != null)
                {
                    jsonManager = Instantiate(jsonFileManagerPrefab);
                }
                else
                {
                    jsonManager = new GameObject("JSONFileManager");
                    jsonManager.AddComponent<JSONFileManager>();
                }
                DontDestroyOnLoad(jsonManager);
            }
            catch (System.Exception)
            {
                success = false;
            }
        }

        if (shouldRecreateJSON)
        {
            yield return new WaitUntil(() => JSONFileManager.Instance != null);
        }

        yield return new WaitForSeconds(0.2f);
        onComplete?.Invoke(success);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        wasInARMode = isInARMode;

        if (IsARScene(scene.name))
        {
            isInARMode = true;
        }
        else
        {
            isInARMode = false;

            if (wasInARMode)
            {
                StartCoroutine(EnsureManagersAfterAR());
            }
        }
    }

    private IEnumerator EnsureManagersAfterAR()
    {
        yield return new WaitForSeconds(0.1f);

        bool needsManagerCheck = false;
        bool shouldRecreateJSON = false;

        try
        {
            shouldRecreateJSON = ARManagerCleanup.ShouldRecreateJSONManager() && JSONFileManager.Instance == null;
        }
        catch (System.Exception)
        {
        }

        if (shouldRecreateJSON)
        {
            needsManagerCheck = true;
            try
            {
                GameObject jsonManager;
                if (jsonFileManagerPrefab != null)
                {
                    jsonManager = Instantiate(jsonFileManagerPrefab);
                }
                else
                {
                    jsonManager = new GameObject("JSONFileManager");
                    jsonManager.AddComponent<JSONFileManager>();
                }
                DontDestroyOnLoad(jsonManager);
            }
            catch (System.Exception)
            {
            }
        }

        if (needsManagerCheck)
        {
            yield return new WaitUntil(() => JSONFileManager.Instance != null);
            InitializeDataSystems();
        }

        ARManagerCleanup.ResetManagerStates();
    }
}