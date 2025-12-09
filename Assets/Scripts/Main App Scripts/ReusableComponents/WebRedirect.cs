using UnityEngine;

public class WebRedirect : MonoBehaviour
{
    [Header("Web URL Settings")]
    [Tooltip("The URL to redirect to when the button is clicked")]
    public string targetURL = "https://wmsucrimsonmapadmin.vercel.app/";
    
    [Header("Optional: Custom URLs for Different Purposes")]
    public string adminPanelURL = "https://wmsucrimsonmapadmin.vercel.app/";
    public string feedbackFormURL = "https://wmsucrimsonmapadmin.vercel.app/feedback";
    public string documentationURL = "https://wmsucrimsonmapadmin.vercel.app/docs";

    /// <summary>
    /// Opens the default target URL in the device's browser
    /// </summary>
    public void OpenTargetURL()
    {
        OpenURL(targetURL);
    }

    /// <summary>
    /// Opens the admin panel URL
    /// </summary>
    public void OpenAdminPanel()
    {
        OpenURL(adminPanelURL);
    }

    /// <summary>
    /// Opens the feedback form URL
    /// </summary>
    public void OpenFeedbackForm()
    {
        OpenURL(feedbackFormURL);
    }

    /// <summary>
    /// Opens the documentation URL
    /// </summary>
    public void OpenDocumentation()
    {
        OpenURL(documentationURL);
    }

    /// <summary>
    /// Opens any custom URL
    /// </summary>
    /// <param name="url">The URL to open</param>
    public void OpenURL(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogWarning("[WebRedirect] URL is empty or null. Cannot open.");
            return;
        }

        // Validate URL format
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            Debug.LogWarning($"[WebRedirect] URL '{url}' does not start with http:// or https://. Adding https://");
            url = "https://" + url;
        }

        Debug.Log($"[WebRedirect] Opening URL: {url}");
        
        try
        {
            Application.OpenURL(url);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[WebRedirect] Failed to open URL: {ex.Message}");
        }
    }
}