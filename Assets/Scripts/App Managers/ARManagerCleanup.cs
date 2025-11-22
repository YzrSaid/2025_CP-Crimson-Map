using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class ARManagerCleanup : MonoBehaviour
{
    [Header("AR Scene Settings")]
    [SerializeField] private string arSceneName = "ARScene";

    [Header("Direct AR Button (Optional)")]
    [SerializeField] private Button directARButton;

    [Header("AR Navigation Button (Optional)")]
    [SerializeField] private Button arNavigationButton;
    [SerializeField] private GameObject pathFindingPanel;

    [Header("READQRCODE Button (Optional)")]
    [SerializeField] private Button readQRCodeButton;

    public GameObject localizationPanel;

    private static bool hadJSONManager = false;
    private static bool hadFirestoreManager = false;
    private static bool hadMapboxManager = false;

    private void Awake()
    {
        if (directARButton != null)
        {
            directARButton.onClick.RemoveAllListeners();
            directARButton.onClick.AddListener(LoadDirectAR);
        }
        if (arNavigationButton != null)
        {
            arNavigationButton.onClick.RemoveAllListeners();
            arNavigationButton.onClick.AddListener(LoadARNavigation);
        }
        if (readQRCodeButton != null)
        {
            readQRCodeButton.onClick.RemoveAllListeners();
            readQRCodeButton.onClick.AddListener(LoadReadQRCode);
        }
    }

    public void LoadDirectAR()
    {
        PlayerPrefs.SetString("ARMode", "DirectAR");
        PlayerPrefs.Save();
        StartCoroutine(CleanupAndLoadAR());
    }

    public void LoadARNavigation()
    {
        PlayerPrefs.SetString("ARMode", "Navigation");
        PlayerPrefs.Save();
        StartCoroutine(CleanupAndLoadAR());
    }

    public void LoadARNavigationWithScene(string sceneName)
    {
        PlayerPrefs.SetString("ARMode", "Navigation");
        PlayerPrefs.Save();
        StartCoroutine(CleanupAndLoadAR(sceneName));
    }

    public void LoadReadQRCode()
    {
        StartCoroutine(CleanupAndLoadAR());
    }

    public IEnumerator CleanupAndLoadAR(string targetSceneName = "")
    {
        Debug.Log("=============== CLEANUP AND LOAD AR START ===============");
        Debug.Log($"ARMode: {PlayerPrefs.GetString("ARMode", "NOT SET")}");
        Debug.Log($"PathNodeCount BEFORE cleanup: {PlayerPrefs.GetInt("ARNavigation_PathNodeCount", -1)}");

        RecordManagerStates();
        DestroyNonEssentialManagers();

        yield return new WaitForEndOfFrame();

        Debug.Log($"PathNodeCount AFTER cleanup: {PlayerPrefs.GetInt("ARNavigation_PathNodeCount", -1)}");
        Debug.Log("=============== LOADING AR SCENE ===============");

        string sceneToLoad = string.IsNullOrEmpty(targetSceneName) ? arSceneName : targetSceneName;
        Debug.Log($"Loading scene: {sceneToLoad}");
        
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }

    private void RecordManagerStates()
    {
        hadJSONManager = JSONFileManager.Instance != null;
        hadFirestoreManager = FirestoreManager.Instance != null;
        hadMapboxManager = FindObjectOfType<MapboxOfflineManager>() != null;

        Debug.Log($"📝 Manager States - JSON: {hadJSONManager}, Firestore: {hadFirestoreManager}, Mapbox: {hadMapboxManager}");
    }

    private void DestroyNonEssentialManagers()
    {
        MainAppLoader mainAppLoader = FindObjectOfType<MainAppLoader>();
        if (mainAppLoader != null)
        {
            Debug.Log("🗑️ Destroying MainAppLoader");
            Destroy(mainAppLoader.gameObject);
        }

        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.isInARMode = true;
            Debug.Log("✅ GlobalManager: AR Mode enabled");
        }
    }

    public static bool ShouldRecreateJSONManager()
    {
        return hadJSONManager;
    }

    public static bool ShouldRecreateFirestoreManager()
    {
        return hadFirestoreManager;
    }

    public static bool ShouldRecreateMapboxManager()
    {
        return hadMapboxManager;
    }

    public static void ResetManagerStates()
    {
        hadJSONManager = false;
        hadFirestoreManager = false;
        hadMapboxManager = false;
    }
}