using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ButtonSceneLoader : MonoBehaviour
{
    [SerializeField] private Button targetButton;  
    [SerializeField] private string sceneName;     

    private void Awake()
    {
        if (targetButton != null)
        {
            targetButton.onClick.AddListener(LoadScene);
        }
        else
        {
            Debug.LogError("❌ ButtonSceneLoader: No button assigned in Inspector!");
        }
    }

    private void LoadScene()
    {
        if (FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>() != null)
        {
            FindObjectOfType<UnityEngine.XR.ARFoundation.ARSession>().Reset();
        }

        SceneManager.LoadScene("ARScene");
    }
}
