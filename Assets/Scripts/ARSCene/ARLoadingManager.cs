using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ARLoadingManager : MonoBehaviour
{
    [Header("Loading Panel")]
    public GameObject loadingPanel;
    public TextMeshProUGUI loadingText;
    public GameObject loadingAnimation;

    [Header("References")]
    public UnifiedARManager unifiedARManager;
    public ARMapManager arMapManager;
    public ARCameraLayerManager cameraLayerManager;
    public UnifiedARNavigationMarkerSpawner navigationMarkerSpawner;

    private bool isARReady = false;
    private bool isMapReady = false;
    private bool isCameraSetupReady = false;
    private bool isNavigationReady = false;
    private bool isCompassInitialized = false;

    void Awake()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(true);

        if (loadingAnimation != null)
            loadingAnimation.SetActive(true);

        FindReferences();
    }

    void Start()
    {
        StartCoroutine(WaitForAllSystems());
    }

    private void FindReferences()
    {
        if (unifiedARManager == null)
            unifiedARManager = FindObjectOfType<UnifiedARManager>();

        if (arMapManager == null)
            arMapManager = FindObjectOfType<ARMapManager>();

        if (cameraLayerManager == null)
            cameraLayerManager = FindObjectOfType<ARCameraLayerManager>();

        if (navigationMarkerSpawner == null)
            navigationMarkerSpawner = FindObjectOfType<UnifiedARNavigationMarkerSpawner>();
    }

    private IEnumerator WaitForAllSystems()
    {

        yield return StartCoroutine(InitializeCompassForAR());

        UpdateLoadingText("Initializing AR System...");
        yield return new WaitForSeconds(0.5f);

        yield return StartCoroutine(WaitForARManager());

        yield return StartCoroutine(WaitForMapSpawning());

        yield return StartCoroutine(WaitForCameraSetup());

        if (ARModeHelper.IsARMode())
        {
            yield return StartCoroutine(WaitForNavigationMarkers());
        }

        yield return new WaitForSeconds(0.5f);

        HideLoadingPanel();
    }

    private IEnumerator InitializeCompassForAR()
    {
        UpdateLoadingText("Initializing Compass...");

        float timeout = 5f;
        float elapsed = 0f;

        while (GPSManager.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (GPSManager.Instance == null)
        {
            Debug.LogError("[ARLoading] GPSManager not found!");
            yield break;
        }

        UpdateLoadingText("Waiting for Compass...");
        elapsed = 0f;

        while (!GPSManager.Instance.IsCompassReady() && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!GPSManager.Instance.IsCompassReady())
        {
            Debug.LogWarning("[ARLoading] Compass not ready, using default orientation");
            yield break;
        }

        UpdateLoadingText("Hold device steady\nCalibrating compass...");

        float calibrationTime = 3f;
        float timer = calibrationTime;
        List<float> headingSamples = new List<float>();

        while (timer > 0)
        {
            timer -= Time.deltaTime;

            float currentHeading = GPSManager.Instance.GetHeading();
            headingSamples.Add(currentHeading);

            int secondsLeft = Mathf.CeilToInt(timer);
            UpdateLoadingText($"Hold device steady\nCalibrating compass... {secondsLeft}s");

            yield return new WaitForSeconds(0.1f);
        }

        float avgHeading = AverageHeadings(headingSamples);

        GPSManager.Instance.InitializeARCompassHeading(avgHeading);
        isCompassInitialized = true;

        UpdateLoadingText("✅ Compass calibrated!");
        Debug.Log($"[ARLoading] Compass calibrated to {avgHeading:F1}° (from {headingSamples.Count} samples)");

        yield return new WaitForSeconds(0.5f);
    }
    private float AverageHeadings(List<float> headings)
    {
        if (headings.Count == 0) return 0f;

        float sumX = 0f;
        float sumY = 0f;

        foreach (float heading in headings)
        {
            float rad = heading * Mathf.Deg2Rad;
            sumX += Mathf.Cos(rad);
            sumY += Mathf.Sin(rad);
        }

        sumX /= headings.Count;
        sumY /= headings.Count;

        float avgHeading = Mathf.Atan2(sumY, sumX) * Mathf.Rad2Deg;
        avgHeading = (avgHeading + 360f) % 360f;

        return avgHeading;
    }

    private IEnumerator WaitForARManager()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (!isARReady && elapsed < timeout)
        {
            if (unifiedARManager != null && unifiedARManager.isActiveAndEnabled)
            {
                isARReady = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isARReady)
        {
            isARReady = true;
        }
    }

    private IEnumerator WaitForMapSpawning()
    {
        float timeout = 15f;
        float elapsed = 0f;

        while (!isMapReady && elapsed < timeout)
        {
            if (arMapManager != null && arMapManager.IsSpawningComplete())
            {
                isMapReady = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.2f);
        }

        if (!isMapReady)
        {
            isMapReady = true;
        }

        yield return new WaitForSeconds(1f);
    }

    private IEnumerator WaitForCameraSetup()
    {
        yield return new WaitForSeconds(1f);
        isCameraSetupReady = true;
    }

    private IEnumerator WaitForNavigationMarkers()
    {
        float timeout = 5f;
        float elapsed = 0f;

        while (!isNavigationReady && elapsed < timeout)
        {
            if (navigationMarkerSpawner != null && navigationMarkerSpawner.isActiveAndEnabled)
            {
                isNavigationReady = true;
                break;
            }

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.2f);
        }

        if (!isNavigationReady)
        {
            isNavigationReady = true;
        }
    }

    private void UpdateLoadingText(string message)
    {
        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }

    public void HideLoadingPanel()
    {
        if (loadingAnimation != null)
            loadingAnimation.SetActive(false);

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    public void ShowLoadingPanel(string message = "Loading...")
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            UpdateLoadingText(message);

            if (loadingAnimation != null)
                loadingAnimation.SetActive(true);
        }
    }

    public bool IsCompassCalibrated()
    {
        return isCompassInitialized;
    }


    public bool IsLoadingComplete()
    {
        return isARReady && isMapReady && isCameraSetupReady &&
               (!ARModeHelper.IsARMode() || isNavigationReady);
    }
}