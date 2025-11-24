using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HomePageLoadingManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject loaderPanel;
    public Image loaderFillImage;
    public TMPro.TextMeshProUGUI loaderText;

    [Header("Managers to Wait For")]
    public MapManager mapManager;
    public BarrierSpawner barrierSpawner;
    public PathRenderer pathRenderer;
    public InfrastructureSpawner infrastructureSpawner;
    public UserIndicator userIndicator;

    [Header("Settings")]
    public float minimumLoadingTime = 1f;
    public float checkInterval = 0.1f;
    public float maxWaitTime = 30f;

    private bool isLoadingComplete = false;
    private float loadingStartTime;
    private int totalSteps = 5;
    private int completedSteps = 0;

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
        UpdateLoadingUI("Initializing map...", 0f);

        yield return StartCoroutine(WaitForMapManager());
        IncrementProgress("Map loaded");

        yield return StartCoroutine(WaitForMapLoadingComplete());
        IncrementProgress("Spawners initialized");

        yield return StartCoroutine(WaitForBarrierSpawner());
        IncrementProgress("Barriers loaded");

        yield return StartCoroutine(WaitForPathRenderer());
        IncrementProgress("Paths rendered");

        yield return StartCoroutine(WaitForInfrastructureSpawner());
        IncrementProgress("Infrastructure loaded");

        float elapsedTime = Time.time - loadingStartTime;
        if (elapsedTime < minimumLoadingTime)
        {
            UpdateLoadingUI("Finalizing...", 0.95f);
            yield return new WaitForSeconds(minimumLoadingTime - elapsedTime);
        }

        CompleteLoading();
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

    private IEnumerator WaitForBarrierSpawner()
    {
        if (barrierSpawner == null)
        {
            barrierSpawner = FindObjectOfType<BarrierSpawner>();
        }

        if (barrierSpawner == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator WaitForPathRenderer()
    {
        if (pathRenderer == null)
        {
            pathRenderer = FindObjectOfType<PathRenderer>();
        }

        if (pathRenderer == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator WaitForInfrastructureSpawner()
    {
        if (infrastructureSpawner == null)
        {
            infrastructureSpawner = FindObjectOfType<InfrastructureSpawner>();
        }

        if (infrastructureSpawner == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(0.5f);
    }

    private void IncrementProgress(string statusText)
    {
        completedSteps++;
        float progress = (float)completedSteps / totalSteps;
        UpdateLoadingUI(statusText, progress);
    }

    private void UpdateLoadingUI(string text, float progress)
    {
        if (loaderText != null)
        {
            loaderText.text = text;
        }

        if (loaderFillImage != null)
        {
            loaderFillImage.fillAmount = progress;
        }
    }

    private void CompleteLoading()
    {
        isLoadingComplete = true;
        UpdateLoadingUI("Ready!", 1f);

        if (loaderPanel != null)
        {
            loaderPanel.SetActive(false);
        }
    }

    public bool IsLoadingComplete()
    {
        return isLoadingComplete;
    }
}