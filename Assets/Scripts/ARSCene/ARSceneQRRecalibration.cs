using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ZXing;
using ZXing.Common;
using Unity.Collections;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;
using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ARSceneQRRecalibration : MonoBehaviour
{
    [Header("AR References")]
    public ARCameraManager arCameraManager;
    public UnifiedARManager unifiedARManager;
    public ARUIManager arUIManager;

    [Header("UI References")]
    public GameObject scanTriggerButton;
    public TextMeshProUGUI scanButtonText;
    public GameObject qrFrameContainer;

    [Header("Confirmation Panel")]
    public GameObject confirmationPanel;
    public TextMeshProUGUI confirmationNote;
    public Button confirmButton;
    public Button cancelButton;

    [Header("Calibration Panel")]
    public GameObject calibrationPanel;
    public TextMeshProUGUI calibrationInstruction;
    public GameObject figure8Animation;
    public Button calibrationExitButton;
    [Header("Security Settings")]
    public string qrSignature = "CRIMSON";
    public string qrDelimiter = "_";

    [Header("Scanning Settings")]
    public bool autoScanMode = false;
    public int frameSkip = 2;

    [Header("Calibration Settings")]
    public int maxCalibrationAttempts = 3;

    [Header("Test Mode (Editor Only)")]
    public bool enableTestMode = false;
    public string testNodeId = "ND-001";

    private bool isScanning = false;
    private bool isScanningActive = false;
    private string scannedNodeId;
    private Node scannedNodeInfo;
    private List<string> availableMapIds = new List<string>();
    private Texture2D cameraImageTexture;
    private int frameCount = 0;
    private int calibrationAttemptCount = 0;

    private IBarcodeReader barcodeReader = new BarcodeReader
    {
        AutoRotate = false,
        Options = new DecodingOptions
        {
            TryHarder = false
        }
    };

    void Start()
    {
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();

        if (unifiedARManager == null)
            unifiedARManager = FindObjectOfType<UnifiedARManager>();

        if (arUIManager == null)
            arUIManager = FindObjectOfType<ARUIManager>();

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        if (calibrationPanel != null)
            calibrationPanel.SetActive(false);

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmRecalibration);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelRecalibration);

        if (calibrationExitButton != null)
        {
            calibrationExitButton.onClick.AddListener(OnCalibrationExit);
            calibrationExitButton.gameObject.SetActive(false);
        }

        if (scanTriggerButton != null)
        {
            Button btn = scanTriggerButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(ToggleScanMode);
        }

        StartCoroutine(InitializeScanner());
    }

    void Update()
    {
#if UNITY_EDITOR
        if (enableTestMode && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            string simulatedQRData = qrSignature + qrDelimiter + testNodeId;
            OnQRCodeScanned(simulatedQRData);
        }
#endif

        if (calibrationPanel != null && calibrationPanel.activeSelf && GPSManager.Instance != null)
        {
            UpdateCalibrationProgress();
        }
    }

    public void ToggleScanMode()
    {
        if (isScanningActive)
        {
            StopScanning();
        }
        else
        {
            StartScanning();
        }
    }

    public void StartScanning()
    {
        isScanningActive = true;

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(true);

        if (scanButtonText != null)
            scanButtonText.text = "Cancel Scan";
    }

    public void StopScanning()
    {
        isScanningActive = false;

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(false);

        if (scanButtonText != null)
            scanButtonText.text = "Scan QR to Recalibrate";
    }

    IEnumerator InitializeScanner()
    {
        yield return StartCoroutine(LoadAvailableMaps());

        if (arCameraManager == null)
        {
            Debug.LogError("[ARQRRecalibration] AR Camera Manager not found!");
            yield break;
        }

        float timeout = 0f;
        while (!arCameraManager.enabled && timeout < 5f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (!arCameraManager.enabled)
        {
            Debug.LogError("[ARQRRecalibration] AR Camera Manager failed to initialize!");
            yield break;
        }

        arCameraManager.frameReceived += OnCameraFrameReceived;
        isScanning = true;

        if (autoScanMode)
        {
            StartScanning();
        }

        Debug.Log("[ARQRRecalibration] Scanner initialized successfully");
    }

    IEnumerator LoadAvailableMaps()
    {
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "maps.json",
            (jsonContent) =>
            {
                try
                {
                    MapList mapList = JsonUtility.FromJson<MapList>("{\"maps\":" + jsonContent + "}");

                    if (mapList != null && mapList.maps != null && mapList.maps.Count > 0)
                    {
                        availableMapIds.Clear();
                        foreach (var map in mapList.maps)
                        {
                            availableMapIds.Add(map.map_id);
                        }
                        Debug.Log($"[ARQRRecalibration] Loaded {availableMapIds.Count} available maps");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ARQRRecalibration] Error loading maps: {ex.Message}");
                }
            },
            (error) =>
            {
                Debug.LogError($"[ARQRRecalibration] Failed to load maps.json: {error}");
            }
        ));
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        frameCount++;

        if (!isScanning || !isScanningActive || arCameraManager == null)
            return;

        if (frameCount % frameSkip != 0)
            return;

        if (!arCameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return;

        try
        {
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, image.width, image.height),
                outputDimensions = new Vector2Int(image.width / 2, image.height / 2),
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            int size = image.GetConvertedDataSize(conversionParams);
            var buffer = new NativeArray<byte>(size, Allocator.Temp);

            image.Convert(conversionParams, buffer);
            image.Dispose();

            if (cameraImageTexture == null)
            {
                cameraImageTexture = new Texture2D(
                    conversionParams.outputDimensions.x,
                    conversionParams.outputDimensions.y,
                    conversionParams.outputFormat,
                    false);
            }

            cameraImageTexture.LoadRawTextureData(buffer);
            cameraImageTexture.Apply();
            buffer.Dispose();

            Color32[] pixels = cameraImageTexture.GetPixels32();
            Result result = barcodeReader.Decode(pixels, cameraImageTexture.width, cameraImageTexture.height);

            if (result != null)
            {
                OnQRCodeScanned(result.Text);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ARQRRecalibration] Error processing frame: {ex.Message}");
        }
    }

    void OnQRCodeScanned(string qrData)
    {
        isScanningActive = false;

        Debug.Log($"[ARQRRecalibration] QR Code scanned: {qrData}");

        if (!ValidateQRCode(qrData, out string nodeId))
        {
            Debug.LogWarning("[ARQRRecalibration] Invalid QR code format");
            StartCoroutine(ShowErrorAndResume("Invalid QR code. Please scan a valid CRIMSON campus QR code."));
            return;
        }

        scannedNodeId = nodeId;
        Debug.Log($"[ARQRRecalibration] Valid node ID extracted: {nodeId}");
        StartCoroutine(SearchNodeInLocalFiles(scannedNodeId));
    }

    bool ValidateQRCode(string qrData, out string nodeId)
    {
        nodeId = null;

        if (!qrData.Contains(qrDelimiter))
            return false;

        string[] parts = qrData.Split(new string[] { qrDelimiter }, StringSplitOptions.None);

        if (parts.Length != 2)
            return false;

        string signature = parts[0];
        if (signature != qrSignature)
            return false;

        nodeId = parts[1];

        if (string.IsNullOrEmpty(nodeId) || nodeId.Length < 3)
            return false;

        return true;
    }

    IEnumerator SearchNodeInLocalFiles(string nodeId)
    {
        bool foundNode = false;

        if (availableMapIds.Count > 0)
        {
            foreach (string mapId in availableMapIds)
            {
                string nodesFileName = $"nodes_{mapId}.json";
                bool searchComplete = false;

                yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
                    nodesFileName,
                    (jsonContent) =>
                    {
                        Node foundNodeInfo = SearchNodeInJson(jsonContent, nodeId);
                        if (foundNodeInfo != null)
                        {
                            scannedNodeInfo = foundNodeInfo;
                            foundNode = true;
                            Debug.Log($"[ARQRRecalibration] Node found: {foundNodeInfo.name} ({foundNodeInfo.node_id})");
                        }
                        searchComplete = true;
                    },
                    (error) =>
                    {
                        Debug.LogWarning($"[ARQRRecalibration] Could not load {nodesFileName}: {error}");
                        searchComplete = true;
                    }
                ));

                while (!searchComplete)
                    yield return null;

                if (foundNode)
                    break;
            }
        }

        if (foundNode)
        {
            if (scannedNodeInfo.type == "infrastructure" || scannedNodeInfo.type == "intermediate")
            {
                ShowConfirmation();
            }
            else
            {
                Debug.LogWarning($"[ARQRRecalibration] Node type is not infrastructure: {scannedNodeInfo.type}");
                StartCoroutine(ShowErrorAndResume("This QR code is not for an outdoor location."));
            }
        }
        else
        {
            Debug.LogWarning($"[ARQRRecalibration] Node not found: {nodeId}");
            StartCoroutine(ShowErrorAndResume("Location not found. This QR code may not be registered in the system."));
        }
    }

    Node SearchNodeInJson(string jsonContent, string nodeId)
    {
        try
        {
            NodeList nodeList = JsonUtility.FromJson<NodeList>("{\"nodes\":" + jsonContent + "}");

            if (nodeList != null && nodeList.nodes != null && nodeList.nodes.Count > 0)
            {
                Node foundNode = nodeList.nodes.FirstOrDefault(n => n.node_id == nodeId);
                return foundNode;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ARQRRecalibration] Error searching node in JSON: {ex.Message}");
        }

        return null;
    }

    void ShowConfirmation()
    {
        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(false);

        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);

            CanvasGroup canvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = confirmationPanel.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0;
            canvasGroup.DOFade(1, 0.5f).SetEase(Ease.OutQuad);
        }

        string coordinateInfo = $"GPS: {scannedNodeInfo.latitude:F6}, {scannedNodeInfo.longitude:F6}";
        string noteText = "Your GPS position will be updated to this location: " + $"<b>{scannedNodeInfo.name}</b>\n\n{coordinateInfo}";

        if (confirmationNote != null)
            confirmationNote.text = noteText;

        Debug.Log($"[ARQRRecalibration] Showing confirmation for: {scannedNodeInfo.name}");
    }

    IEnumerator ShowErrorAndResume(string errorMessage)
    {
        Debug.LogWarning($"[ARQRRecalibration] Error: {errorMessage}");
        yield return new WaitForSeconds(2f);
        ResumeScanning();
    }

    void ResumeScanning()
    {
        isScanningActive = true;

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(true);

        Debug.Log("[ARQRRecalibration] Resuming scanning");
    }

    void OnConfirmRecalibration()
    {
        Debug.Log("=============== QR RECALIBRATION START ===============");
        Debug.Log($"[ARQRRecalibration] Confirming recalibration to: {scannedNodeInfo.name}");
        Debug.Log($"[ARQRRecalibration] Coordinates: ({scannedNodeInfo.latitude}, {scannedNodeInfo.longitude})");

        calibrationAttemptCount = 0;

        if (confirmationPanel != null)
        {
            CanvasGroup canvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = confirmationPanel.AddComponent<CanvasGroup>();

            canvasGroup.DOFade(0, 0.3f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                confirmationPanel.SetActive(false);

                ShowCalibrationPanel();
            });
        }
        else
        {
            ShowCalibrationPanel();
        }

        if (GPSManager.Instance != null)
        {
            Debug.Log("[ARQRRecalibration] Step 1: Clearing existing location lock...");
            GPSManager.Instance.UnlockLocationForPathfinding();

            Debug.Log("[ARQRRecalibration] Step 2: Clearing any previous QR override...");
            GPSManager.Instance.ClearQRLocationOverride();

            Debug.Log("[ARQRRecalibration] Step 3: Setting new QR location override...");
            GPSManager.Instance.SetQRLocationOverride(
                scannedNodeInfo.latitude,
                scannedNodeInfo.longitude,
                0f
            );

            Vector2 newCoords = GPSManager.Instance.GetCoordinates();
            Debug.Log($"[ARQRRecalibration] Verification - GPS now reports: ({newCoords.x}, {newCoords.y})");
        }
        else
        {
            Debug.LogError("[ARQRRecalibration] ❌ GPSManager.Instance is null!");
        }

        Debug.Log("[ARQRRecalibration] Step 4: Saving recalibration to PlayerPrefs...");
        PlayerPrefs.SetString("ScannedNodeID", scannedNodeInfo.node_id);
        PlayerPrefs.SetString("ScannedLocationName", scannedNodeInfo.name);
        PlayerPrefs.SetFloat("ScannedLat", scannedNodeInfo.latitude);
        PlayerPrefs.SetFloat("ScannedLng", scannedNodeInfo.longitude);

        PlayerPrefs.SetString("ARNavigation_OriginalFromNodeId", scannedNodeInfo.node_id);
        PlayerPrefs.Save();

        if (unifiedARManager != null)
        {
            Debug.Log("[ARQRRecalibration] Step 5: Notifying UnifiedARManager...");
            unifiedARManager.OnQRCodeScanned(scannedNodeInfo);
            unifiedARManager.ReloadNavigationData();
        }

        DirectionDisplayManager directionManager = FindObjectOfType<DirectionDisplayManager>();
        if (directionManager != null)
        {
            Debug.Log("[ARQRRecalibration] Step 6: Notifying DirectionDisplayManager...");
            directionManager.OnNodeReached(scannedNodeInfo.node_id);
        }

        UserIndicator userIndicator = FindObjectOfType<UserIndicator>();
        if (userIndicator != null)
        {
            Debug.Log("[ARQRRecalibration] Step 7: Force updating UserIndicator...");
            userIndicator.ForceUpdate();
        }

        UnifiedARNavigationMarkerSpawner markerSpawner = FindObjectOfType<UnifiedARNavigationMarkerSpawner>();
        if (markerSpawner != null)
        {
            Debug.Log("[ARQRRecalibration] Step 8: Force updating marker visibility...");
            markerSpawner.OnLocationRecalibrated();
        }

        Debug.Log("=============== QR RECALIBRATION COMPLETE ===============");
    }

    void ShowCalibrationPanel()
    {
        calibrationAttemptCount++;
        Debug.Log($"[QR Calibration] ShowCalibrationPanel called - Now at attempt {calibrationAttemptCount}/{maxCalibrationAttempts}");

        float calibrationTime = GPSManager.Instance != null ? GPSManager.Instance.qrCalibrationSmoothTime : 7f;

        if (calibrationPanel == null)
        {
            StartCoroutine(CheckGPSAfterCalibration(calibrationTime));
            StopScanning();
            return;
        }

        calibrationPanel.SetActive(true);

        if (calibrationInstruction != null)
        {
            calibrationInstruction.text = "Move your device in a figure-8 pattern to help GPS lock onto your location.";
        }

        if (figure8Animation != null)
        {
            figure8Animation.SetActive(true);
        }

        if (calibrationExitButton != null)
        {
            calibrationExitButton.gameObject.SetActive(false);
        }

        CanvasGroup canvasGroup = calibrationPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = calibrationPanel.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0;
        canvasGroup.DOFade(1, 0.5f).SetEase(Ease.OutQuad);

        StartCoroutine(CheckGPSAfterCalibration(calibrationTime));

        StopScanning();
    }
    void ShowFinalCalibrationFailure()
    {
        if (calibrationPanel == null)
            return;

        Debug.Log("[QR Calibration] Showing final calibration failure UI");

        calibrationPanel.SetActive(true);

        if (calibrationInstruction != null)
        {
            calibrationInstruction.text = "GPS signal is too weak to navigate safely. Please try again later when you have better signal.";
        }

        if (figure8Animation != null)
        {
            figure8Animation.SetActive(false);
        }

        if (calibrationExitButton != null)
        {
            calibrationExitButton.gameObject.SetActive(true);
        }

        CanvasGroup canvasGroup = calibrationPanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = calibrationPanel.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
    }

    IEnumerator CheckGPSAfterCalibration(float delay)
    {
        Debug.Log($"[QR Calibration] Starting {delay}s calibration wait... (Attempt {calibrationAttemptCount}/{maxCalibrationAttempts})");
        if (GPSManager.Instance != null)
        {
            Debug.Log($"[QR Calibration] Current GPS Thresholds: Strong={GPSManager.Instance.strongGPSThreshold}m, Weak={GPSManager.Instance.weakGPSThreshold}m");
        }

        yield return new WaitForSeconds(delay);

        bool isGPSGood = false;
        float currentAccuracy = -1f;

        if (GPSManager.Instance != null)
        {
            isGPSGood = GPSManager.Instance.IsGPSAccurate();
            currentAccuracy = GPSManager.Instance.GetGPSAccuracy();

            Debug.Log("========== GPS CALIBRATION CHECK ==========");
            Debug.Log($"GPS Accuracy: {currentAccuracy:F1}m");
            Debug.Log($"Strong Threshold: {GPSManager.Instance.strongGPSThreshold}m");
            Debug.Log($"Is GPS Good? {(isGPSGood ? "YES ✅" : "NO ❌")}");
            Debug.Log($"Attempt: {calibrationAttemptCount}/{maxCalibrationAttempts}");
            Debug.Log("==========================================");
        }
        else
        {
            Debug.LogError("[QR Calibration] ❌ GPSManager.Instance is NULL!");
        }

        if (isGPSGood)
        {
            Debug.Log("[QR Calibration] ✅ GPS signal improved! Continuing navigation...");
            Debug.Log($"[QR Calibration] Final accuracy: {currentAccuracy:F1}m (threshold: {GPSManager.Instance.strongGPSThreshold}m)");

            if (GPSManager.Instance != null)
            {
                GPSManager.Instance.ClearQRLocationOverride();
                Debug.Log("[QR Calibration] QR override cleared - now using real GPS");
            }

            HideCalibrationPanel();
        }
        else
        {
            Debug.Log($"[QR Calibration] ⚠️ GPS still weak: {currentAccuracy:F1}m (need: {GPSManager.Instance?.strongGPSThreshold}m)");

            if (calibrationAttemptCount >= maxCalibrationAttempts)
            {
                Debug.LogError("[QR Calibration] ❌ GPS signal unavailable after all attempts!");
                Debug.LogError($"[QR Calibration] Final accuracy: {currentAccuracy:F1}m (needed: {GPSManager.Instance?.strongGPSThreshold}m)");

                ShowFinalCalibrationFailure();
            }
            else
            {
                Debug.Log($"[QR Calibration] Retrying... (will be attempt {calibrationAttemptCount + 1}/{maxCalibrationAttempts})");

                if (calibrationPanel != null)
                {
                    CanvasGroup canvasGroup = calibrationPanel.GetComponent<CanvasGroup>();
                    if (canvasGroup == null)
                        canvasGroup = calibrationPanel.AddComponent<CanvasGroup>();

                    canvasGroup.DOFade(0, 0.3f).SetEase(Ease.OutQuad).OnComplete(() =>
                    {
                        ShowCalibrationPanel();
                    });
                }
                else
                {
                    ShowCalibrationPanel();
                }
            }
        }
    }
    void UpdateCalibrationProgress()
    {
        if (GPSManager.Instance == null)
            return;

        if (calibrationAttemptCount >= maxCalibrationAttempts)
            return;

        float progress = GPSManager.Instance.GetQRCalibrationProgress();
    }

    void HideCalibrationPanel()
    {
        if (calibrationPanel != null && calibrationPanel.activeSelf)
        {
            CanvasGroup canvasGroup = calibrationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = calibrationPanel.AddComponent<CanvasGroup>();

            canvasGroup.DOFade(0, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                calibrationPanel.SetActive(false);
            });
        }
    }

    void OnCalibrationExit()
    {
        if (unifiedARManager != null)
        {
            unifiedARManager.ExitARScene();
        }

        HideCalibrationPanel();
    }

    void OnCancelRecalibration()
    {
        Debug.Log("[ARQRRecalibration] Recalibration cancelled");

        if (confirmationPanel != null)
        {
            CanvasGroup canvasGroup = confirmationPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = confirmationPanel.AddComponent<CanvasGroup>();

            canvasGroup.DOFade(0, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                confirmationPanel.SetActive(false);
            });
        }

        ResumeScanning();

        if (qrFrameContainer != null)
            qrFrameContainer.SetActive(true);
    }

    void OnDestroy()
    {
        isScanning = false;

        if (arCameraManager != null)
        {
            arCameraManager.frameReceived -= OnCameraFrameReceived;
        }

        if (cameraImageTexture != null)
        {
            Destroy(cameraImageTexture);
        }

        Debug.Log("[ARQRRecalibration] Scanner destroyed");
    }
}