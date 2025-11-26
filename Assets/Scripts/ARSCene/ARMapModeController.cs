using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ARMapModeController : MonoBehaviour
{
    [Header("Map References")]
    public GameObject outdoorMapContainer;
    public GameObject indoorMapContainer;

    [Header("Outdoor Buttons")]
    public Button goInsideButton;

    [Header("Indoor Buttons")]
    public Button goOutsideButton;
    public GameObject floorButtonPanel;
    public Button floorUpButton;
    public Button floorDownButton;

    [Header("Floor Indicator")]
    public GameObject floorIndicator;
    public TextMeshProUGUI floorIndicatorText;

    [Header("Managers")]
    public ARIndoorMapManager arIndoorMapManager;
    public ARMapManager arMapManager;

    private bool isIndoorMode = false;
    private string destinationInfraId;
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private Dictionary<string, IndoorInfrastructure> indoorInfrastructures = new Dictionary<string, IndoorInfrastructure>();

    public static ARMapModeController Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (goInsideButton != null)
            goInsideButton.onClick.AddListener(OnGoInsideClicked);

        if (goOutsideButton != null)
            goOutsideButton.onClick.AddListener(OnGoOutsideClicked);

        if (floorUpButton != null)
            floorUpButton.onClick.AddListener(OnFloorUpClicked);

        if (floorDownButton != null)
            floorDownButton.onClick.AddListener(OnFloorDownClicked);

        if (floorIndicator != null)
            floorIndicator.SetActive(false);

        SetOutdoorMode();

        StartCoroutine(LoadAllData());
    }

    private IEnumerator LoadAllData()
    {
        string mapId = PlayerPrefs.GetString("ARScene_MapId", "MAP-01");
        yield return StartCoroutine(LoadNodes(mapId));
        yield return StartCoroutine(LoadIndoorData());

        CheckDestinationForIndoor();
    }

    private IEnumerator LoadNodes(string mapId)
    {
        string fileName = $"nodes_{mapId}.json";
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            fileName,
            (jsonContent) =>
            {
                try
                {
                    Node[] nodes = JsonHelper.FromJson<Node>(jsonContent);
                    allNodes.Clear();

                    foreach (var node in nodes)
                    {
                        allNodes[node.node_id] = node;
                    }

                    loadComplete = true;
                }
                catch (System.Exception)
                {
                    loadComplete = true;
                }
            },
            (error) =>
            {
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
    }

    private IEnumerator LoadIndoorData()
    {
        bool loadComplete = false;

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "indoor.json",
            (jsonContent) =>
            {
                try
                {
                    IndoorInfrastructure[] indoorArray = JsonHelper.FromJson<IndoorInfrastructure>(jsonContent);
                    indoorInfrastructures.Clear();

                    foreach (var indoor in indoorArray)
                    {
                        if (!indoor.is_deleted)
                        {
                            indoorInfrastructures[indoor.room_id] = indoor;
                        }
                    }

                    loadComplete = true;
                }
                catch (System.Exception)
                {
                    loadComplete = true;
                }
            },
            (error) =>
            {
                loadComplete = true;
            }
        ));

        yield return new WaitUntil(() => loadComplete);
    }

    private void CheckDestinationForIndoor()
    {
        string toNodeId = PlayerPrefs.GetString("ARNavigation_OriginalToNodeId", "");

        if (string.IsNullOrEmpty(toNodeId))
        {
            if (goInsideButton != null)
                goInsideButton.gameObject.SetActive(false);
            return;
        }

        if (!allNodes.ContainsKey(toNodeId))
        {
            if (goInsideButton != null)
                goInsideButton.gameObject.SetActive(false);
            return;
        }

        Node destinationNode = allNodes[toNodeId];

        if (destinationNode.type == "indoorinfra" && destinationNode.HasRelatedRoomId)
        {
            if (indoorInfrastructures.ContainsKey(destinationNode.related_room_id))
            {
                IndoorInfrastructure indoorInfo = indoorInfrastructures[destinationNode.related_room_id];
                destinationInfraId = indoorInfo.infra_id;

                if (goInsideButton != null)
                    goInsideButton.gameObject.SetActive(true);

                return;
            }
        }

        if (goInsideButton != null)
            goInsideButton.gameObject.SetActive(false);
    }

    private void OnGoInsideClicked()
    {
        if (string.IsNullOrEmpty(destinationInfraId))
            return;

        Node infraNode = allNodes.Values.FirstOrDefault(n =>
            n.type == "infrastructure" && n.related_infra_id == destinationInfraId);

        if (infraNode == null)
            return;

        SetIndoorMode(destinationInfraId, infraNode);
    }

    private void OnGoOutsideClicked()
    {
        SetOutdoorMode();
    }

    private void OnFloorUpClicked()
    {
        if (arIndoorMapManager != null)
        {
            arIndoorMapManager.ChangeFloor(1);
        }
    }

    private void OnFloorDownClicked()
    {
        if (arIndoorMapManager != null)
        {
            arIndoorMapManager.ChangeFloor(-1);
        }
    }

    public void UpdateFloorIndicator(int floor)
    {
        if (floorIndicatorText != null)
        {
            floorIndicatorText.text = floor.ToString();
        }
    }

    private void SetOutdoorMode()
    {
        isIndoorMode = false;

        if (outdoorMapContainer != null)
            outdoorMapContainer.SetActive(true);

        if (indoorMapContainer != null)
            indoorMapContainer.SetActive(false);

        if (goInsideButton != null)
            goInsideButton.gameObject.SetActive(false);

        if (goOutsideButton != null)
            goOutsideButton.gameObject.SetActive(false);

        if (floorButtonPanel != null)
            floorButtonPanel.SetActive(false);

        if (floorUpButton != null)
            floorUpButton.gameObject.SetActive(false);

        if (floorDownButton != null)
            floorDownButton.gameObject.SetActive(false);

        if (floorIndicator != null)
            floorIndicator.SetActive(false);

        CheckDestinationForIndoor();
    }

    private void SetIndoorMode(string infraId, Node infraNode)
    {
        isIndoorMode = true;

        if (outdoorMapContainer != null)
            outdoorMapContainer.SetActive(false);

        if (indoorMapContainer != null)
            indoorMapContainer.SetActive(true);

        if (goInsideButton != null)
            goInsideButton.gameObject.SetActive(false);

        if (goOutsideButton != null)
            goOutsideButton.gameObject.SetActive(true);

        if (floorButtonPanel != null)
            floorButtonPanel.SetActive(true);

        if (floorUpButton != null)
            floorUpButton.gameObject.SetActive(true);

        if (floorDownButton != null)
            floorDownButton.gameObject.SetActive(true);

        if (floorIndicator != null)
            floorIndicator.SetActive(true);

        if (arIndoorMapManager != null)
        {
            arIndoorMapManager.LoadIndoorMap(infraId, infraNode);
        }
    }

    public bool IsIndoorMode()
    {
        return isIndoorMode;
    }

    public string GetCurrentInfraId()
    {
        return destinationInfraId;
    }

    void OnDestroy()
    {
        if (goInsideButton != null)
            goInsideButton.onClick.RemoveListener(OnGoInsideClicked);

        if (goOutsideButton != null)
            goOutsideButton.onClick.RemoveListener(OnGoOutsideClicked);

        if (floorUpButton != null)
            floorUpButton.onClick.RemoveListener(OnFloorUpClicked);

        if (floorDownButton != null)
            floorDownButton.onClick.RemoveListener(OnFloorDownClicked);
    }
}   