using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AppVersionDisplay : MonoBehaviour
{
    [Header("Version Display")]
    public TextMeshProUGUI versionTextTMP;
    public string versionPrefix = "v";

    void Start()
    {
        UpdateVersionDisplay();
    }

    private void UpdateVersionDisplay()
    {
        string appVersion = Application.version;
        string displayVersion = $"{versionPrefix}{appVersion}";
        if (versionTextTMP != null)
        {
            versionTextTMP.text = displayVersion;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateVersionDisplay();
        }
    }
#endif
}