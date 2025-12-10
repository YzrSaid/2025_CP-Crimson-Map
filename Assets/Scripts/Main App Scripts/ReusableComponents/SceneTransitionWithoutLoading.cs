using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

public class SceneTransitionWithoutLoading : MonoBehaviour
{
    public static SceneTransitionWithoutLoading Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public static void GoToTargetSceneSimple(string sceneName)
    {
        GlobalManager.SetSkipFullInitialization(true);
        if (GlobalManager.Instance != null && ARModeHelper.IsARMode())
        {
            // Save and delete the navigation data
            ARNavigationDataHelper.SaveAndClearARNavigationData();
            // Delete the highlights in the map
            if (ARMapManager.Instance != null)
            {
                ARMapManager.Instance.ClearNavigationHighlights();
            }
            ARModeHelper.DisableARMode();
            PlayerPrefs.SetString("ARNavigation_SameBuilding", "false");
            PlayerPrefs.Save();
            GlobalManager.Instance.StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(sceneName));
        }
        else if (GlobalManager.Instance != null && ARModeHelper.IsNotARMode())
        {
            GlobalManager.Instance.StartCoroutine(GlobalManager.Instance.SafeARCleanupAndExit(sceneName));
        }
    }
}