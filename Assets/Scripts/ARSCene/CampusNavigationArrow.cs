using UnityEngine;
using UnityEngine.UI;

public class CompassNavigationArrow : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform arrowTransform;

    [Header("Settings")]
    public float rotationSmoothSpeed = 5f;
    public bool enableDebugLogs = false;

    private Vector2 userLocation;
    private Node targetNode;
    private bool isActive = false;
    private UnifiedARManager arManager;

    void Start()
    {
        arManager = FindObjectOfType<UnifiedARManager>();

        Input.compass.enabled = true;
        Input.location.Start();
    }

    void Update()
    {
        if (!isActive || targetNode == null)
            return;

        UpdateUserLocation();
        UpdateArrowRotation();
    }

    private void UpdateUserLocation()
    {
        if (GPSManager.Instance != null && GPSManager.Instance.IsUsingQROverride())
        {
            userLocation = GPSManager.Instance.GetCoordinates();
            if (enableDebugLogs)
            {
                Debug.Log($"[CompassArrow] Using QR Override: ({userLocation.x:F6}, {userLocation.y:F6})");
            }
            return;
        }
        
        if (arManager != null)
        {
            userLocation = arManager.GetUserRawGPS();
            if (enableDebugLogs)
            {
                Debug.Log($"[CompassArrow] Using Raw GPS from ARManager: ({userLocation.x:F6}, {userLocation.y:F6})");
            }
            return;
        }
        
        if (GPSManager.Instance != null)
        {
            userLocation = GPSManager.Instance.GetRawSmoothedGPSCoordinates();
            if (enableDebugLogs)
            {
                Debug.Log($"[CompassArrow] Using Raw GPS from GPSManager: ({userLocation.x:F6}, {userLocation.y:F6})");
            }
        }
    }

    private void UpdateArrowRotation()
    {
        if (arrowTransform == null || targetNode == null)
            return;

        bool isIndoor = (arManager != null && arManager.IsIndoorMode()) || 
                        targetNode.type == "indoorinfra";

        float targetAngle = 0f;

        if (isIndoor)
        {
            Vector2 targetXY;
            if (targetNode.indoor != null)
            {
                targetXY = new Vector2(targetNode.indoor.x, targetNode.indoor.y);
            }
            else
            {
                targetXY = new Vector2(targetNode.x_coordinate, targetNode.y_coordinate);
            }

            Vector2 direction = targetXY - userLocation;

            float bearingToTarget = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

            float deviceHeading = 0f;
            if (arManager != null && Camera.main != null)
            {
                deviceHeading = Camera.main.transform.eulerAngles.y;
            }

            targetAngle = bearingToTarget - deviceHeading;
            targetAngle = (targetAngle + 360f) % 360f;
        }
        else
        {
            Vector2 targetGPS = new Vector2(targetNode.latitude, targetNode.longitude);
            float bearingToTarget = CalculateBearingGPS(userLocation, targetGPS);

            float deviceHeading = GPSManager.Instance != null ? GPSManager.Instance.GetHeading() : 0f;

            targetAngle = bearingToTarget - deviceHeading;
            targetAngle = (targetAngle + 360f) % 360f;
        }

        Quaternion targetRotation = Quaternion.Euler(0, 0, -targetAngle);
        arrowTransform.rotation = Quaternion.Lerp(
            arrowTransform.rotation,
            targetRotation,
            Time.deltaTime * rotationSmoothSpeed
        );

        if (enableDebugLogs)
        {
            string mode = isIndoor ? "Indoor" : "Outdoor";
        }
    }

    private float CalculateBearingGPS(Vector2 from, Vector2 to)
    {
        float lat1 = from.x * Mathf.Deg2Rad;
        float lat2 = to.x * Mathf.Deg2Rad;
        float deltaLng = (to.y - from.y) * Mathf.Deg2Rad;

        float y = Mathf.Sin(deltaLng) * Mathf.Cos(lat2);
        float x = Mathf.Cos(lat1) * Mathf.Sin(lat2) -
                  Mathf.Sin(lat1) * Mathf.Cos(lat2) * Mathf.Cos(deltaLng);

        float bearing = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        bearing = (bearing + 360f) % 360f;

        return bearing;
    }

    public void SetTargetNode(Node node)
    {
        targetNode = node;
        isActive = (node != null);
        
        if (enableDebugLogs && node != null)
        {
            string nodeType = node.type == "indoorinfra" ? "Indoor" : "Outdoor";
            Debug.Log($"CompassArrow: Target set to {node.name} ({nodeType})");
        }
    }

    public void SetActive(bool active)
    {
        isActive = active;
        gameObject.SetActive(active);
    }

    void OnDestroy()
    {
        Input.compass.enabled = false;
    }
}