using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class ARManagerCleanup : MonoBehaviour
{
    [Header("AR Scene Settings")]
    [SerializeField] private string arSceneName = "ARScene";

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

    public void LoadARNavigation()
    {
        StartCoroutine(CleanupAndLoadAR());
    }

    public void LoadARNavigationWithScene(string sceneName)
    {
        StartCoroutine(CleanupAndLoadAR(sceneName));
    }

    public void LoadReadQRCode()
    {
        StartCoroutine(CleanupAndLoadAR());
    }

    public IEnumerator CleanupAndLoadAR(string targetSceneName = "")
    {
        RecordManagerStates();
        DestroyNonEssentialManagers();

        yield return new WaitForEndOfFrame();

        string sceneToLoad = string.IsNullOrEmpty(targetSceneName) ? arSceneName : targetSceneName;
        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);
    }

    private void RecordManagerStates()
    {
        hadJSONManager = JSONFileManager.Instance != null;
        hadFirestoreManager = FirestoreManager.Instance != null;
        hadMapboxManager = FindObjectOfType<MapboxOfflineManager>() != null;
    }

    private void DestroyNonEssentialManagers()
    {
        MainAppLoader mainAppLoader = FindObjectOfType<MainAppLoader>();
        if (mainAppLoader != null)
        {
            Destroy(mainAppLoader.gameObject);
        }

        if (GlobalManager.Instance != null)
        {
            GlobalManager.Instance.isInARMode = true;
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