using UnityEngine;
using System.Collections;

public class HomePageLoadingManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject loaderPanel;

    [Header("Managers to Wait For")]
    public MapManager mapManager;
    public BarrierSpawner barrierSpawner;
    public PathRenderer pathRenderer;
    public InfrastructureSpawner infrastructureSpawner;

    [Header("Settings")]
    public float minimumLoadingTime = 1f;
    public float checkInterval = 0.1f;
    public float maxWaitTime = 30f;

    private float loadingStartTime;

    void Start()
    {
        if (loaderPanel != null)
        {
            loaderPanel.SetActive(true);
        }

        loadingStartTime = Time.time;

        StartCoroutine(WaitForAllSystemsReady());
    }

    private IEnumerator WaitForAllSystemsReady()
    {
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
}