using UnityEngine;
using System.Collections;

public class HomePageLoadingManager : MonoBehaviour
{
    public GameObject loaderPanel;

    public MapManager mapManager;
    public BarrierSpawner barrierSpawner;
    public PathRenderer pathRenderer;
    public InfrastructureSpawner infrastructureSpawner;
    public InfrastructurePopulator infrastructurePopulator;

    public float minimumLoadingTime = 1f;
    public float checkInterval = 0.1f;
    public float maxWaitTime = 30f;

    private float loadingStartTime;
    private bool isLoading = false;

    void Start()
    {
        bool returningFromAR = GlobalManager.ShouldSkipFullInitialization();
        
        if (returningFromAR || !GlobalManager.Instance.isDataInitialized)
        {
            ShowLoaderAndWait();
        }
    }

    void OnEnable()
    {
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.OnDataInitializationComplete += OnDataInitComplete;
        }
    }

    void OnDisable()
    {
        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.OnDataInitializationComplete -= OnDataInitComplete;
        }
    }

    private void OnDataInitComplete()
    {
        if (!isLoading)
        {
            ShowLoaderAndWait();
        }
    }

    private void ShowLoaderAndWait()
    {
        if (isLoading)
            return;

        isLoading = true;

        if (loaderPanel != null)
        {
            loaderPanel.SetActive(true);
        }

        loadingStartTime = Time.time;

        StartCoroutine(WaitForAllSystemsReady());
    }

    public void TriggerMapChangeLoading()
    {
        if (isLoading)
            return;

        isLoading = true;

        if (loaderPanel != null)
        {
            loaderPanel.SetActive(true);
        }

        loadingStartTime = Time.time;

        StartCoroutine(WaitForMapChangeComplete());
    }

    private IEnumerator WaitForMapChangeComplete()
    {
        Debug.Log("[LoadingManager] Waiting for map change to complete...");
        
        yield return StartCoroutine(WaitForMapLoadingComplete());
        
        yield return StartCoroutine(WaitForSpawnersToFinish());
        
        yield return StartCoroutine(WaitForInfrastructurePopulator());

        float elapsedTime = Time.time - loadingStartTime;
        if (elapsedTime < minimumLoadingTime)
        {
            yield return new WaitForSeconds(minimumLoadingTime - elapsedTime);
        }

        if (loaderPanel != null)
        {
            loaderPanel.SetActive(false);
        }

        isLoading = false;
        Debug.Log("[LoadingManager] Map change loading complete!");
    }

    private IEnumerator WaitForAllSystemsReady()
    {
        yield return StartCoroutine(WaitForGlobalManager());

        yield return StartCoroutine(WaitForMapManager());
        
        yield return StartCoroutine(WaitForMapLoadingComplete());
        
        yield return StartCoroutine(WaitForSpawnersToFinish());
        
        yield return StartCoroutine(WaitForInfrastructurePopulator());

        float elapsedTime = Time.time - loadingStartTime;
        if (elapsedTime < minimumLoadingTime)
        {
            yield return new WaitForSeconds(minimumLoadingTime - elapsedTime);
        }

        if (loaderPanel != null)
        {
            loaderPanel.SetActive(false);
        }

        isLoading = false;
    }

    private IEnumerator WaitForGlobalManager()
    {
        float waitTime = 0f;
        
        while (GlobalManager.Instance == null || !GlobalManager.Instance.IsSystemReady())
        {
            if (waitTime >= maxWaitTime)
            {
                yield break;
            }

            waitTime += checkInterval;
            yield return new WaitForSeconds(checkInterval);
        }
    }

    private IEnumerator WaitForMapManager()
    {
        if (mapManager == null)
        {
            mapManager = FindObjectOfType<MapManager>();
        }

        float waitTime = 0f;
        while (mapManager == null || !mapManager.IsReady())
        {
            if (waitTime >= maxWaitTime)
            {
                yield break;
            }

            waitTime += checkInterval;
            yield return new WaitForSeconds(checkInterval);
        }
    }

    private IEnumerator WaitForMapLoadingComplete()
    {
        bool loadingComplete = false;

        if (mapManager != null)
        {
            mapManager.OnMapLoadingComplete += () => loadingComplete = true;
        }

        float waitTime = 0f;
        while (!loadingComplete)
        {
            if (waitTime >= maxWaitTime)
            {
                Debug.LogWarning("[LoadingManager] Map loading timeout!");
                yield break;
            }

            waitTime += checkInterval;
            yield return new WaitForSeconds(checkInterval);
        }
        
        Debug.Log("[LoadingManager] Map loading complete");
    }

    private IEnumerator WaitForSpawnersToFinish()
    {
        if (barrierSpawner == null)
            barrierSpawner = FindObjectOfType<BarrierSpawner>();

        if (pathRenderer == null)
            pathRenderer = FindObjectOfType<PathRenderer>();

        if (infrastructureSpawner == null)
            infrastructureSpawner = FindObjectOfType<InfrastructureSpawner>();

        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("[LoadingManager] Spawners finished");
    }

    private IEnumerator WaitForInfrastructurePopulator()
    {
        if (infrastructurePopulator == null)
        {
            infrastructurePopulator = FindObjectOfType<InfrastructurePopulator>();
        }

        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("[LoadingManager] Infrastructure populator ready");
    }

    public void TriggerLoading()
    {
        if (!isLoading)
        {
            ShowLoaderAndWait();
        }
    }
}