using UnityEngine;

/// <summary>
/// Helper class to determine if the app is in AR mode or not
/// </summary>
public static class ARModeHelper
{
    private const string AR_MODE_KEY = "IsARMode";

    /// <summary>
    /// Check if currently in AR mode
    /// </summary>
    public static bool IsARMode()
    {
        return PlayerPrefs.GetInt(AR_MODE_KEY, 0) == 1;
    }

    /// <summary>
    /// Check if currently NOT in AR mode (in map/home view)
    /// </summary>
    public static bool IsNotARMode()
    {
        return !IsARMode();
    }

    /// <summary>
    /// Enable AR mode
    /// </summary>
    public static void EnableARMode()
    {
        PlayerPrefs.SetInt(AR_MODE_KEY, 1);
        PlayerPrefs.Save();
        Debug.Log("[ARModeHelper] AR Mode enabled");
    }

    /// <summary>
    /// Disable AR mode (return to map view)
    /// </summary>
    public static void DisableARMode()
    {
        PlayerPrefs.SetInt(AR_MODE_KEY, 0);
        PlayerPrefs.Save();
        Debug.Log("[ARModeHelper] AR Mode disabled");
    }

    /// <summary>
    /// Set AR mode directly
    /// </summary>
    public static void SetARMode(bool isARMode)
    {
        PlayerPrefs.SetInt(AR_MODE_KEY, isARMode ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[ARModeHelper] AR Mode set to: {isARMode}");
    }

    /// <summary>
    /// Clear AR mode setting (resets to map mode)
    /// </summary>
    public static void ClearARMode()
    {
        PlayerPrefs.DeleteKey(AR_MODE_KEY);
        PlayerPrefs.Save();
        Debug.Log("[ARModeHelper] AR Mode cleared");
    }
}