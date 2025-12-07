using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mapbox.Utils;
using Mapbox.Unity.Map;
using UnityEngine.InputSystem;

public class GPSManager : MonoBehaviour
{
    public static GPSManager Instance;
    public bool useMockLocationInEditor = true;

    [Header("Mock GPS Settings (Editor Only)")]
    private float mockLatitude = 6.91261f;
    private float mockLongitude = 122.06359f;
    private float mockHeading = 0f;

    private float arSceneCompassHeading = 0f;
    private bool arCompassInitialized = false;

    [Header("GPS Smoothing")]
    private List<Vector2> recentCoordinates = new List<Vector2>();
    private int maxHistorySize = 3;

    [Header("GPS Accuracy Settings")]
    public float strongGPSThreshold = 5f;
    public float weakGPSThreshold = 10f;

    [Header("QR Calibration")]
    public float qrCalibrationSmoothTime = 7f;
    private float qrCalibrationStartTime = -1f;
    private bool isQRCalibrating = false;

    [Header("Compass Debug")]
    public bool enableCompassDebug = true;

    private MagneticFieldSensor magnetometer;
    private Accelerometer accelerometer;
    private UnityEngine.InputSystem.Gyroscope gyroscope;

    private bool sensorsInitialized = false;
    private float currentHeading = 0f;

    private const string PREF_LOCATION_LOCKED = "GPS_LocationLocked";
    private const string PREF_LOCKED_LAT = "GPS_LockedLatitude";
    private const string PREF_LOCKED_LNG = "GPS_LockedLongitude";
    private const string PREF_QR_OVERRIDE = "GPS_QROverride";
    private const string PREF_QR_LAT = "GPS_QRLatitude";
    private const string PREF_QR_LNG = "GPS_QRLongitude";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeSensors();
        LoadLockStateFromPlayerPrefs();
    }

    public void InitializeARCompassHeading(float? overrideHeading = null)
    {
        if (overrideHeading.HasValue)
        {
            // Use the provided heading from calibration
            arSceneCompassHeading = overrideHeading.Value;
            arCompassInitialized = true;
            Debug.Log($"[GPSManager] AR Scene Compass Initialized (Override): {arSceneCompassHeading}°");
        }
        else if (IsCompassReady())
        {
            // Use current heading
            arSceneCompassHeading = GetHeading();
            arCompassInitialized = true;
            Debug.Log($"[GPSManager] AR Scene Compass Initialized: {arSceneCompassHeading}°");
        }
        else
        {
            Debug.LogWarning("[GPSManager] Compass not ready for AR initialization");
        }
    }
    public float GetARSceneCompassHeading()
    {
        return arCompassInitialized ? arSceneCompassHeading : GetHeading();
    }

    public bool IsARCompassInitialized()
    {
        return arCompassInitialized;
    }

    public float GetNorthCorrectionAngle()
    {
        if (!arCompassInitialized)
            return 0f;

        return arSceneCompassHeading;
    }

    private void InitializeSensors()
    {
        try
        {
            magnetometer = MagneticFieldSensor.current;
            if (magnetometer != null)
            {
                InputSystem.EnableDevice(magnetometer);
            }

            accelerometer = Accelerometer.current;
            if (accelerometer != null)
            {
                InputSystem.EnableDevice(accelerometer);
            }

            gyroscope = UnityEngine.InputSystem.Gyroscope.current;
            if (gyroscope != null)
            {
                InputSystem.EnableDevice(gyroscope);
            }

            sensorsInitialized = (magnetometer != null && accelerometer != null);
        }
        catch (System.Exception)
        {
        }
    }

    private void LoadLockStateFromPlayerPrefs()
    {
        bool isLocked = PlayerPrefs.GetInt(PREF_LOCATION_LOCKED, 0) == 1;
        bool hasQROverride = PlayerPrefs.GetInt(PREF_QR_OVERRIDE, 0) == 1;
    }

    public void Start()
    {
        StartCoroutine(StartLocationService());
    }

    public IEnumerator StartLocationService()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            sensorsInitialized = true;
            yield break;
        }
#endif

        if (!Input.location.isEnabledByUser)
        {
            yield break;
        }

        Input.location.Start();

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }
    }

    public Vector2 GetCoordinates()
    {
        if (PlayerPrefs.GetInt(PREF_QR_OVERRIDE, 0) == 1)
        {
            float lat = PlayerPrefs.GetFloat(PREF_QR_LAT, 0f);
            float lng = PlayerPrefs.GetFloat(PREF_QR_LNG, 0f);
            return new Vector2(lat, lng);
        }

        if (PlayerPrefs.GetInt(PREF_LOCATION_LOCKED, 0) == 1)
        {
            float lat = PlayerPrefs.GetFloat(PREF_LOCKED_LAT, 0f);
            float lng = PlayerPrefs.GetFloat(PREF_LOCKED_LNG, 0f);
            return new Vector2(lat, lng);
        }

        return GetRealGPSCoordinates();
    }

    public Vector2 GetRealGPSCoordinates()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            return new Vector2(mockLatitude, mockLongitude);
        }
#endif

        if (Input.location.status == LocationServiceStatus.Running)
        {
            return new Vector2(Input.location.lastData.latitude, Input.location.lastData.longitude);
        }
        else
        {
            return new Vector2(mockLatitude, mockLongitude);
        }
    }

    public Vector2 GetRawSmoothedGPSCoordinates()
    {
        Vector2 rawCoords = GetRealGPSCoordinates();

        if (rawCoords.magnitude < 0.0001f)
            return rawCoords;

        recentCoordinates.Add(rawCoords);
        if (recentCoordinates.Count > maxHistorySize)
            recentCoordinates.RemoveAt(0);

        Vector2 sum = Vector2.zero;
        foreach (var coord in recentCoordinates)
            sum += coord;

        return sum / recentCoordinates.Count;
    }

    public float GetHeading()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.qKey.isPressed)
                    mockHeading -= 90f * Time.deltaTime;
                if (Keyboard.current.eKey.isPressed)
                    mockHeading += 90f * Time.deltaTime;
            }

            mockHeading = mockHeading % 360f;
            if (mockHeading < 0) mockHeading += 360f;

            return mockHeading;
        }
#endif

        if (sensorsInitialized && magnetometer != null && accelerometer != null)
        {
            return CalculateHeadingFromSensors();
        }
        else
        {
            return currentHeading;
        }
    }

    private float CalculateHeadingFromSensors()
    {
        try
        {
            Vector3 magnetic = magnetometer.magneticField.ReadValue();
            Vector3 accel = accelerometer.acceleration.ReadValue();

            if (magnetic.sqrMagnitude < 0.01f || accel.sqrMagnitude < 0.01f)
                return currentHeading;

            magnetic.Normalize();
            accel.Normalize();

            float pitch = Mathf.Asin(-accel.x);
            float roll = Mathf.Asin(accel.y / Mathf.Cos(pitch));

            float magX = magnetic.x * Mathf.Cos(pitch) + magnetic.z * Mathf.Sin(pitch);
            float magY = -magnetic.x * Mathf.Sin(roll) * Mathf.Sin(pitch)
                         + magnetic.y * Mathf.Cos(roll)
                         + magnetic.z * Mathf.Sin(roll) * Mathf.Cos(pitch);

            float heading = Mathf.Atan2(-magY, -magX) * Mathf.Rad2Deg + 90f;
            heading = (heading + 360f) % 360f;

            return heading;
        }
        catch (System.Exception)
        {
            return currentHeading;
        }
    }

    public void LockLocationForPathfinding(float latitude, float longitude)
    {
        PlayerPrefs.SetInt(PREF_LOCATION_LOCKED, 1);
        PlayerPrefs.SetFloat(PREF_LOCKED_LAT, latitude);
        PlayerPrefs.SetFloat(PREF_LOCKED_LNG, longitude);
        PlayerPrefs.Save();
    }

    public void UnlockLocationForPathfinding()
    {
        PlayerPrefs.DeleteKey(PREF_LOCATION_LOCKED);
        PlayerPrefs.DeleteKey(PREF_LOCKED_LAT);
        PlayerPrefs.DeleteKey(PREF_LOCKED_LNG);
        PlayerPrefs.Save();
    }

    public bool IsLocationLocked()
    {
        bool isLocked = PlayerPrefs.GetInt(PREF_LOCATION_LOCKED, 0) == 1;
        bool hasQROverride = PlayerPrefs.GetInt(PREF_QR_OVERRIDE, 0) == 1;
        return isLocked || hasQROverride;
    }

    public void SetQRLocationOverride(Vector2 location, float heading = 0f)
    {
        PlayerPrefs.SetInt(PREF_QR_OVERRIDE, 1);
        PlayerPrefs.SetFloat(PREF_QR_LAT, location.x);
        PlayerPrefs.SetFloat(PREF_QR_LNG, location.y);
        PlayerPrefs.Save();

        isQRCalibrating = true;
        qrCalibrationStartTime = Time.time;

        Debug.Log($"[GPS] QR Override SET: ({location.x}, {location.y}) - Calibration started");
    }

    public void SetQRLocationOverride(float latitude, float longitude, float heading = 0f)
    {
        SetQRLocationOverride(new Vector2(latitude, longitude), heading);
    }

    public void ClearQRLocationOverride()
    {
        PlayerPrefs.DeleteKey(PREF_QR_OVERRIDE);
        PlayerPrefs.DeleteKey(PREF_QR_LAT);
        PlayerPrefs.DeleteKey(PREF_QR_LNG);
        PlayerPrefs.Save();

        isQRCalibrating = false;
        qrCalibrationStartTime = -1f;

        Debug.Log("[GPS] QR Override CLEARED");
    }

    public bool IsUsingQROverride()
    {
        return PlayerPrefs.GetInt(PREF_QR_OVERRIDE, 0) == 1;
    }

    public bool IsGPSAccurate()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            return true;
        }
#endif

        if (Input.location.status == LocationServiceStatus.Running)
        {
            float accuracy = Input.location.lastData.horizontalAccuracy;

            bool isAccurate = accuracy > 0 && accuracy <= strongGPSThreshold;

            Debug.Log($"[GPS] Accuracy check: {accuracy:F1}m - {(isAccurate ? "GOOD ✅" : "WEAK ⚠️")}");
            return isAccurate;
        }

        Debug.Log("[GPS] Accuracy check: No GPS signal ❌");
        return false;
    }

    public float GetGPSAccuracy()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            return 5f;
        }
#endif

        if (Input.location.status == LocationServiceStatus.Running)
        {
            return Input.location.lastData.horizontalAccuracy;
        }

        return -1f;
    }

    public float GetQRCalibrationProgress()
    {
        if (!isQRCalibrating || qrCalibrationStartTime < 0f)
            return 1f;

        float elapsed = Time.time - qrCalibrationStartTime;
        float progress = Mathf.Clamp01(elapsed / qrCalibrationSmoothTime);

        return progress;
    }

    public bool IsQRCalibrationComplete()
    {
        if (!isQRCalibrating)
            return true;

        return GetQRCalibrationProgress() >= 1f;
    }

    public Vector2 GetSmoothedCoordinates()
    {
        Vector2 rawCoords = GetCoordinates();

        if (IsLocationLocked())
        {
            return rawCoords;
        }

        recentCoordinates.Add(rawCoords);
        if (recentCoordinates.Count > maxHistorySize)
            recentCoordinates.RemoveAt(0);

        Vector2 sum = Vector2.zero;
        foreach (var coord in recentCoordinates)
            sum += coord;

        return sum / recentCoordinates.Count;
    }

    public bool IsCompassReady()
    {
        return sensorsInitialized && magnetometer != null;
    }

    public string GetSensorStatus()
    {
        string status = "";
        status += $"Magnetometer: {(magnetometer != null ? "✅" : "❌")}\n";
        status += $"Accelerometer: {(accelerometer != null ? "✅" : "❌")}\n";
        status += $"Gyroscope: {(gyroscope != null ? "✅" : "❌")}\n";
        status += $"Initialized: {sensorsInitialized}";
        return status;
    }

    void Update()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (IsUsingQROverride())
                ClearQRLocationOverride();
            else
                SetQRLocationOverride(mockLatitude + 0.001f, mockLongitude + 0.001f, mockHeading + 45f);
        }
#endif
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            InitializeSensors();
        }
    }

    void OnDestroy()
    {
        if (magnetometer != null)
            InputSystem.DisableDevice(magnetometer);
        if (accelerometer != null)
            InputSystem.DisableDevice(accelerometer);
        if (gyroscope != null)
            InputSystem.DisableDevice(gyroscope);
    }
}