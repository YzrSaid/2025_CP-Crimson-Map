using UnityEngine;
using System.Collections;

public class HomePageLoadingManager : MonoBehaviour
{
    public GameObject loaderPanel;

    public MapManager mapManager;
    public BarrierSpawner barrierSpawner;
    public PathRenderer pathRenderer;
    public InfrastructureSpawner infrastructureSpawner;

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

    private IEnumerator WaitForAllSystemsReady()
    {
        yield return StartCoroutine(WaitForGlobalManager());

        yield return StartCoroutine(WaitForMapManager());
        
        yield return StartCoroutine(WaitForMapLoadingComplete());
        
        yield return StartCoroutine(WaitForSpawnersToFinish());

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
                yield break;
            }

            waitTime += checkInterval;
            yield return new WaitForSeconds(checkInterval);
        }
    }

    private IEnumerator WaitForSpawnersToFinish()
    {
        if (barrierSpawner == null)
            barrierSpawner = FindObjectOfType<BarrierSpawner>();

        if (pathRenderer == null)
            pathRenderer = FindObjectOfType<PathRenderer>();

        if (infrastructureSpawner == null)
            infrastructureSpawner = FindObjectOfType<InfrastructureSpawner>();

        yield return new WaitForSeconds(1f);
    }

    public void TriggerLoading()
    {
        if (!isLoading)
        {
            ShowLoaderAndWait();
        }
    }
}