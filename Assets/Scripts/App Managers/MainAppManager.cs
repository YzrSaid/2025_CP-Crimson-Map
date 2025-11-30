using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MainAppManager : MonoBehaviour
{
    public static MainAppManager Instance { get; private set; }

    public Button homeButton;
    public Button navigateButton;
    public Button settingsButton;

    public Image homeButtonImage;
    public Image navigateButtonImage;
    public Image settingsButtonImage;

    public GameObject homeUnderline;
    public GameObject navigateUnderline;
    public GameObject settingsUnderline;
    public Color activeColor = new Color32(184, 16, 19, 255);
    public Color inactiveColor = new Color32(30, 30, 30, 255);

    public GameObject homePanel;
    public GameObject explorePanel;
    public GameObject settingsPanel;

    [Header("Lock Location Panel")]
    public GameObject lockPanel;

    [Header("GPS Strength Indicators")]
    public GameObject gpsStrongImage;
    public GameObject gpsWeakImage;
    public GameObject gpsNoneImage;

    [Header("GPS Strength Thresholds")]
    public float strongGPSAccuracyThreshold = 20f;
    public float weakGPSAccuracyThreshold = 50f;
    public float gpsCheckInterval = 2f;
    private float lastGPSCheckTime = 0f;

    [Header("Debug GPS Strength Testing (Editor Only)")]
    public bool useDebugGPSStrength = false;
    public GPSStrength debugGPSStrength = GPSStrength.Strong;

    private float currentGPSAccuracy = 0f;

    public enum GPSStrength
    {
        Strong,
        Weak,
        None
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        homeButton.onClick.AddListener(OnHomeButtonClicked);
        navigateButton.onClick.AddListener(OnNavigateButtonClicked);
        settingsButton.onClick.AddListener(OnSettingsButtonClicked);

        HideAllGPSIndicators();
    }

    void Update()
    {
        if (Time.time - lastGPSCheckTime >= gpsCheckInterval)
        {
            CheckGPSStrength();
            lastGPSCheckTime = Time.time;
        }
    }

    void OnHomeButtonClicked()
    {
        homeButtonImage.color = activeColor;
        navigateButtonImage.color = inactiveColor;
        settingsButtonImage.color = inactiveColor;

        homeUnderline.SetActive(true);
        navigateUnderline.SetActive(false);
        settingsUnderline.SetActive(false);

        homePanel.SetActive(true);
        explorePanel.SetActive(false);
        settingsPanel.SetActive(false);

        if (lockPanel != null)
        {
            lockPanel.SetActive(true);
        }
    }

    void OnNavigateButtonClicked()
    {
        navigateButtonImage.color = activeColor;
        homeButtonImage.color = inactiveColor;
        settingsButtonImage.color = inactiveColor;

        homeUnderline.SetActive(false);
        navigateUnderline.SetActive(true);
        settingsUnderline.SetActive(false);

        homePanel.SetActive(false);
        explorePanel.SetActive(true);
        settingsPanel.SetActive(false);


        if (lockPanel != null)
        {
            lockPanel.SetActive(false);
        }

    }

    void OnSettingsButtonClicked()
    {
        settingsButtonImage.color = activeColor;
        homeButtonImage.color = inactiveColor;
        navigateButtonImage.color = inactiveColor;

        homeUnderline.SetActive(false);
        navigateUnderline.SetActive(false);
        settingsUnderline.SetActive(true);

        homePanel.SetActive(false);
        explorePanel.SetActive(false);
        settingsPanel.SetActive(true);

        if (lockPanel != null)
        {
            lockPanel.SetActive(false);
        }

    }

    private void CheckGPSStrength()
    {
        if (GPSManager.Instance == null)
        {
            UpdateGPSStrengthIndicator(GPSStrength.None);
            return;
        }

        Vector2 gpsCoords = GPSManager.Instance.GetSmoothedCoordinates();

        if (gpsCoords.magnitude < 0.0001f)
        {
            currentGPSAccuracy = -1f;
            UpdateGPSStrengthIndicator(GPSStrength.None);
            return;
        }

#if UNITY_EDITOR
        if (useDebugGPSStrength)
        {
            switch (debugGPSStrength)
            {
                case GPSStrength.Strong:
                    currentGPSAccuracy = 10f;
                    break;
                case GPSStrength.Weak:
                    currentGPSAccuracy = 35f;
                    break;
                case GPSStrength.None:
                    currentGPSAccuracy = 100f;
                    break;
            }
            UpdateGPSStrengthIndicator(debugGPSStrength);
        }
        else
        {
            currentGPSAccuracy = 10f;
            UpdateGPSStrengthIndicator(GPSStrength.Strong);
        }
#else
        if (Input.location.status == LocationServiceStatus.Running)
        {
            currentGPSAccuracy = Input.location.lastData.horizontalAccuracy;

            if (currentGPSAccuracy <= strongGPSAccuracyThreshold)
            {
                UpdateGPSStrengthIndicator(GPSStrength.Strong);
            }
            else if (currentGPSAccuracy <= weakGPSAccuracyThreshold)
            {
                UpdateGPSStrengthIndicator(GPSStrength.Weak);
            }
            else
            {
                UpdateGPSStrengthIndicator(GPSStrength.None);
            }
        }
        else
        {
            currentGPSAccuracy = -1f;
            UpdateGPSStrengthIndicator(GPSStrength.None);
        }
#endif
    }

    private void UpdateGPSStrengthIndicator(GPSStrength strength)
    {
        HideAllGPSIndicators();

        switch (strength)
        {
            case GPSStrength.Strong:
                if (gpsStrongImage != null)
                    gpsStrongImage.SetActive(true);
                break;

            case GPSStrength.Weak:
                if (gpsWeakImage != null)
                    gpsWeakImage.SetActive(true);
                break;

            case GPSStrength.None:
                if (gpsNoneImage != null)
                    gpsNoneImage.SetActive(true);
                break;
        }
    }

    private void HideAllGPSIndicators()
    {
        if (gpsStrongImage != null)
            gpsStrongImage.SetActive(false);

        if (gpsWeakImage != null)
            gpsWeakImage.SetActive(false);

        if (gpsNoneImage != null)
            gpsNoneImage.SetActive(false);
    }

    public float GetCurrentGPSAccuracy()
    {
        return currentGPSAccuracy;
    }
}