using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Linq;

public class AccordionItem : MonoBehaviour
{
    [Header("UI References")]
    public Button headerButton;
    public RectTransform contentPanel;
    public Transform infrastructureContainer;
    public string infrastructureContainerName = "Content_Panel";

    [Header("Prefab Reference")]
    [HideInInspector]
    public GameObject infrastructurePrefab;

    [Header("Animation Settings")]
    public float animationSpeed = 5f;
    public float minHeight = 60f;
    public float itemHeight = 120f;
    public float padding = 20f;
    public float emptyMessageHeight = 80f;

    [Header("Empty State")]
    public bool showEmptyMessage = true;
    public string emptyMessageText = "No infrastructures available";
    public string emptyRecentMessageText = "No navigation history yet";
    public string emptyBookmarksMessageText = "No saved infrastructures";
    public TMP_FontAsset emptyMessageFont;
    public float emptyMessageFontSize = 16f;
    public Color emptyMessageColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    public TextAlignmentOptions emptyMessageAlignment = TextAlignmentOptions.Center;
    public FontStyles emptyMessageFontStyle = FontStyles.Normal;
    public float emptyMessagePaddingTop = 20f;
    public float emptyMessagePaddingBottom = 20f;
    public float emptyMessagePaddingLeft = 20f;
    public float emptyMessagePaddingRight = 20f;

    [HideInInspector]
    public AccordionManager manager;

    private List<GameObject> spawnedInfrastructures = new List<GameObject>();
    private RectTransform rectTransform;
    private float targetHeight;
    private bool isExpanded = false;
    private string categoryId;
    private bool infrastructuresLoaded = false;
    private GameObject emptyMessageObject;
    private bool isRecentCategory = false;
    private bool isBookmarksCategory = false;

    // Preloaded data for search before items are spawned
    private List<Infrastructure> cachedInfraData = new List<Infrastructure>();
    private bool dataPreloaded = false;
    private string pendingSearchFilter = null;

    public bool IsExpanded => isExpanded;
    public bool IsBookmarksCategory => isBookmarksCategory;
    public void RefreshBookmarks()
    {
        if (!isBookmarksCategory) return;
        
        infrastructuresLoaded = false;
        
        if (isExpanded)
        {
            StartCoroutine(LoadBookmarkedInfrastructuresCoroutine());
        }
    }

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (contentPanel == null)
        {
            contentPanel = transform.Find("Content")?.GetComponent<RectTransform>();
            if (contentPanel == null)
            {
                contentPanel = transform.Find("Content_Panel")?.GetComponent<RectTransform>();
            }
        }

        if (infrastructureContainer == null && contentPanel != null)
        {
            infrastructureContainer = FindInfrastructureContainer();
        }

        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, minHeight);

        if (contentPanel != null)
            contentPanel.gameObject.SetActive(false);
    }

    private Transform FindInfrastructureContainer()
    {
        Transform found = contentPanel.Find(infrastructureContainerName);
        if (found != null)
        {
            return found;
        }

        if (contentPanel.childCount > 0)
        {
            return contentPanel.GetChild(0);
        }

        return null;
    }

    public void SetCategoryId(string catId)
    {
        categoryId = catId;
        isRecentCategory = false;
        isBookmarksCategory = false;
    }

    public string GetCategoryId() => categoryId;

    // Called by ExploreSearchManager to give this accordion its pre-loaded infra data
    public void SetCachedInfraData(List<Infrastructure> data)
    {
        cachedInfraData = data ?? new List<Infrastructure>();
        dataPreloaded = true;
    }

    public void SetPendingFilter(string searchText)
    {
        pendingSearchFilter = searchText;
    }
    public void SetAsBookmarksCategory()
    {
        isBookmarksCategory = true;
        isRecentCategory = false;
        categoryId = null;
    }

    public void SetAsRecentCategory()
    {
        isRecentCategory = true;
        isBookmarksCategory = false;
        categoryId = null;
    }

    public IEnumerator LoadInfrastructures()
    {
        if (infrastructuresLoaded)
        {
            yield break;
        }

        if (string.IsNullOrEmpty(categoryId))
        {
            infrastructuresLoaded = true;

            if (isExpanded)
            {
                yield return new WaitForEndOfFrame();
                ShowEmptyMessage();
                targetHeight = minHeight + emptyMessageHeight;
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            }
            yield break;
        }

        // Use preloaded data if ready — avoids re-reading infrastructure.json
        if (dataPreloaded && cachedInfraData.Count > 0)
        {
            foreach (var infra in cachedInfraData)
                SpawnInfrastructureItem(infra);
            infrastructuresLoaded = true;
            StartCoroutine(FinishExpandAfterLoad());
            yield break;
        }

        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            OnInfrastructuresLoadSuccess,
            OnInfrastructuresLoadError
        ));
    }

    void OnInfrastructuresLoadSuccess(string jsonData)
    {
        try
        {
            string wrappedJson = "{\"infrastructures\":" + jsonData + "}";
            InfrastructureList infrastructureList = JsonUtility.FromJson<InfrastructureList>(wrappedJson);

            int loadedCount = 0;
            foreach (Infrastructure infra in infrastructureList.infrastructures)
            {
                if (!infra.is_deleted && infra.category_id == categoryId)
                {
                    SpawnInfrastructureItem(infra);
                    loadedCount++;
                }
            }

            infrastructuresLoaded = true;

            StartCoroutine(FinishExpandAfterLoad());
        }
        catch (System.Exception)
        {
            infrastructuresLoaded = true;

            if (isExpanded)
            {
                StartCoroutine(ShowEmptyStateAfterError());
            }
        }
    }

    IEnumerator ShowEmptyStateAfterError()
    {
        yield return new WaitForEndOfFrame();
        ShowEmptyMessage();
        targetHeight = minHeight + emptyMessageHeight;
        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
        ForceLayoutUpdate();
    }

    IEnumerator FinishExpandAfterLoad()
    {
        if (infrastructureContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(infrastructureContainer.GetComponent<RectTransform>());
        }

        yield return null;

        if (spawnedInfrastructures.Count > 0)
        {
            HideEmptyMessage();
            UpdateContentHeight();
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
        }
        else
        {
            ShowEmptyMessage();
            targetHeight = minHeight + emptyMessageHeight;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
        }

        ForceLayoutUpdate();

        if (!string.IsNullOrEmpty(pendingSearchFilter))
            ApplyPendingFilter();
    }

    void OnInfrastructuresLoadError(string errorMessage)
    {
        infrastructuresLoaded = true;

        if (isExpanded)
        {
            StartCoroutine(ShowEmptyStateAfterError());
        }
    }

    private IEnumerator LoadRecentDestinationsCoroutine()
    {
        infrastructuresLoaded = true;

        if (infrastructureContainer == null)
        {
            yield break;
        }

        ClearSpawnedItems();

        List<SavedNavigation> recentDestinations = ARNavigationDataHelper.GetNavigationHistory();

        if (recentDestinations == null || recentDestinations.Count == 0)
        {
            ShowEmptyMessage();
            targetHeight = minHeight + emptyMessageHeight;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            ForceLayoutUpdate();
            yield break;
        }

        recentDestinations.Sort((a, b) => DateTime.Parse(b.timestamp).CompareTo(DateTime.Parse(a.timestamp)));

        foreach (SavedNavigation nav in recentDestinations)
        {
            GameObject itemObj = Instantiate(infrastructurePrefab, infrastructureContainer);
            RecentDestinationItem itemScript = itemObj.GetComponent<RecentDestinationItem>();

            if (itemScript != null)
            {
                itemScript.SetNavigationData(nav);
                spawnedInfrastructures.Add(itemObj);
            }
            else
            {
                Destroy(itemObj);
            }
        }

        yield return StartCoroutine(FinishExpandAfterLoad());
    }

    private IEnumerator LoadBookmarkedInfrastructuresCoroutine()
    {
        infrastructuresLoaded = true;

        if (infrastructureContainer == null)
        {
            yield break;
        }

        ClearSpawnedItems();

        BookmarkData bookmarkData = LoadBookmarkData();

        if (bookmarkData == null || bookmarkData.bookmarked_infra_ids == null || bookmarkData.bookmarked_infra_ids.Count == 0)
        {
            ShowEmptyMessage();
            targetHeight = minHeight + emptyMessageHeight;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            ForceLayoutUpdate();
            yield break;
        }

        yield return StartCoroutine(LoadBookmarkedInfrastructuresFromJson(bookmarkData.bookmarked_infra_ids));
    }

    private IEnumerator LoadBookmarkedInfrastructuresFromJson(List<string> bookmarkedIds)
    {
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            (jsonData) => OnBookmarkedInfrastructuresLoadSuccess(jsonData, bookmarkedIds),
            (error) => OnBookmarkedInfrastructuresLoadError(error)
        ));
    }

    void OnBookmarkedInfrastructuresLoadSuccess(string jsonData, List<string> bookmarkedIds)
    {
        try
        {
            string wrappedJson = "{\"infrastructures\":" + jsonData + "}";
            InfrastructureList infrastructureList = JsonUtility.FromJson<InfrastructureList>(wrappedJson);

            foreach (Infrastructure infra in infrastructureList.infrastructures)
            {
                if (!infra.is_deleted && bookmarkedIds.Contains(infra.infra_id))
                {
                    SpawnInfrastructureItem(infra);
                }
            }

            StartCoroutine(FinishExpandAfterLoad());
        }
        catch (System.Exception e)
        {
            StartCoroutine(ShowEmptyStateAfterError());
        }
    }

    void OnBookmarkedInfrastructuresLoadError(string errorMessage)
    {
        StartCoroutine(ShowEmptyStateAfterError());
    }

    private BookmarkData LoadBookmarkData()
    {
        string filePath = GetBookmarkFilePath();

        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                BookmarkData data = JsonUtility.FromJson<BookmarkData>(json);
                return data ?? new BookmarkData();
            }
            catch (Exception e)
            {
                return new BookmarkData();
            }
        }

        return new BookmarkData();
    }

    private string GetBookmarkFilePath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.streamingAssetsPath, "bookmarks.json");
#else
        return Path.Combine(Application.persistentDataPath, "bookmarks.json");
#endif
    }

    private void ClearSpawnedItems()
    {
        foreach (GameObject item in spawnedInfrastructures)
        {
            if (item != null)
                Destroy(item);
        }
        spawnedInfrastructures.Clear();
    }

    void SpawnInfrastructureItem(Infrastructure infra)
    {
        GameObject newItem = Instantiate(infrastructurePrefab, infrastructureContainer);

        if (isBookmarksCategory)
        {
            SavedInfrastructureItem itemScript = newItem.GetComponent<SavedInfrastructureItem>();
            if (itemScript != null)
            {
                itemScript.SetInfrastructureData(infra);
            }
        }
        else
        {
            ExploreInfrastructureItem itemScript = newItem.GetComponent<ExploreInfrastructureItem>();
            if (itemScript != null)
            {
                itemScript.SetInfrastructureData(infra);
            }
        }

        spawnedInfrastructures.Add(newItem);
    }

    void UpdateContentHeight()
    {
        int itemCount = spawnedInfrastructures.Count;

        if (itemCount == 0)
        {
            targetHeight = minHeight + emptyMessageHeight;
        }
        else
        {
            targetHeight = minHeight + (itemHeight * itemCount) + padding;
        }

        targetHeight = Mathf.Max(targetHeight, minHeight);
    }

    void ShowEmptyMessage()
    {
        if (!showEmptyMessage || contentPanel == null) return;

        HideEmptyMessage();

        GameObject emptyObj = new GameObject("EmptyMessage");
        emptyObj.transform.SetParent(infrastructureContainer != null ? infrastructureContainer : contentPanel, false);

        RectTransform emptyRect = emptyObj.AddComponent<RectTransform>();
        emptyRect.anchorMin = new Vector2(0, 1);
        emptyRect.anchorMax = new Vector2(1, 1);
        emptyRect.pivot = new Vector2(0.5f, 1);
        emptyRect.sizeDelta = new Vector2(0, emptyMessageHeight);
        emptyRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI emptyText = emptyObj.AddComponent<TextMeshProUGUI>();
        if (isBookmarksCategory)
        {
            emptyText.text = emptyBookmarksMessageText;
        }
        else if (isRecentCategory)
        {
            emptyText.text = emptyRecentMessageText;
        }
        else
        {
            emptyText.text = emptyMessageText;
        }

        if (emptyMessageFont != null)
            emptyText.font = emptyMessageFont;

        emptyText.fontSize = emptyMessageFontSize;
        emptyText.color = emptyMessageColor;
        emptyText.alignment = emptyMessageAlignment;
        emptyText.fontStyle = emptyMessageFontStyle;
        emptyText.enableWordWrapping = true;
        emptyText.margin = new Vector4(
            emptyMessagePaddingLeft,
            emptyMessagePaddingTop,
            emptyMessagePaddingRight,
            emptyMessagePaddingBottom
        );

        emptyMessageObject = emptyObj;
    }

    void HideEmptyMessage()
    {
        if (emptyMessageObject != null)
        {
            Destroy(emptyMessageObject);
            emptyMessageObject = null;
        }
    }

    public void Toggle()
    {
        if (!isExpanded)
        {
            Expand();
        }
        else
        {
            Collapse();
        }
    }

    public void Expand()
    {
        if (isExpanded)
        {
            // Already expanded — still apply any pending filter
            if (!string.IsNullOrEmpty(pendingSearchFilter))
                ApplyPendingFilter();
            return;
        }

        isExpanded = true;

        if (contentPanel != null)
            contentPanel.gameObject.SetActive(true);

        StopAllCoroutines();

        if (isRecentCategory)
        {
            if (!infrastructuresLoaded)
            {
                StartCoroutine(LoadRecentDestinationsCoroutine());
            }
            else
            {
                if (spawnedInfrastructures.Count > 0)
                {
                    HideEmptyMessage();
                    UpdateContentHeight();
                    rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
                }
                else
                {
                    ShowEmptyMessage();
                    targetHeight = minHeight + emptyMessageHeight;
                    rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
                }
                ForceLayoutUpdate();
                if (!string.IsNullOrEmpty(pendingSearchFilter))
                    ApplyPendingFilter();
            }
        }
        else if (isBookmarksCategory)
        {
            if (!infrastructuresLoaded)
            {
                StartCoroutine(LoadBookmarkedInfrastructuresCoroutine());
            }
            else
            {
                if (spawnedInfrastructures.Count > 0)
                {
                    HideEmptyMessage();
                    UpdateContentHeight();
                    rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
                }
                else
                {
                    ShowEmptyMessage();
                    targetHeight = minHeight + emptyMessageHeight;
                    rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
                }
                ForceLayoutUpdate();
                if (!string.IsNullOrEmpty(pendingSearchFilter))
                    ApplyPendingFilter();
            }
        }
        else if (infrastructuresLoaded)
        {
            if (spawnedInfrastructures.Count > 0)
            {
                HideEmptyMessage();
                UpdateContentHeight();
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            }
            else
            {
                ShowEmptyMessage();
                targetHeight = minHeight + emptyMessageHeight;
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
            }
            ForceLayoutUpdate();
            if (!string.IsNullOrEmpty(pendingSearchFilter))
                ApplyPendingFilter();
        }
        else
        {
            targetHeight = minHeight + emptyMessageHeight;
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);

            ForceLayoutUpdate();

            StartCoroutine(LoadInfrastructures());
        }
    }

    private void ApplyPendingFilter()
    {
        FilterInfrastructuresBySearch(pendingSearchFilter);
        int visibleCount = spawnedInfrastructures.Count(i => i != null && i.activeSelf);
        if (visibleCount > 0)
        {
            HideEmptyMessage();
            targetHeight = minHeight + (itemHeight * visibleCount) + padding;
        }
        else
        {
            ShowEmptyMessage();
            targetHeight = minHeight + emptyMessageHeight;
        }
        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, targetHeight);
        ForceLayoutUpdate();
        pendingSearchFilter = null;
    }

    public void Collapse()
    {
        if (!isExpanded) return;

        isExpanded = false;

        StopAllCoroutines();

        HideEmptyMessage();

        rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, minHeight);

        ForceLayoutUpdate();

        if (contentPanel != null)
            contentPanel.gameObject.SetActive(false);
    }

    void ForceLayoutUpdate()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        if (transform.parent != null)
        {
            RectTransform parentRect = transform.parent.GetComponent<RectTransform>();
            if (parentRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }

        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
    }

    void OnDestroy()
    {
        ClearSpawnedItems();

        if (emptyMessageObject != null)
            Destroy(emptyMessageObject);
    }

    public string GetCategoryName()
    {
        if (headerButton != null)
        {
            TMPro.TextMeshProUGUI headerText = headerButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (headerText != null)
            {
                return headerText.text;
            }
        }
        return "";
    }

    public List<string> FilterInfrastructuresBySearch(string searchText)
    {
        List<string> matchingIds = new List<string>();
        searchText = searchText.ToLower().Trim();

        // If items haven't been spawned yet, search the preloaded data cache
        if (spawnedInfrastructures == null || spawnedInfrastructures.Count == 0)
        {
            if (dataPreloaded && cachedInfraData != null)
            {
                foreach (var infra in cachedInfraData)
                {
                    if (!string.IsNullOrEmpty(infra.name) && infra.name.ToLower().Trim().Contains(searchText))
                        matchingIds.Add(infra.infra_id);
                }
            }
            return matchingIds;
        }

        foreach (GameObject infraObj in spawnedInfrastructures)
        {
            if (infraObj == null) continue;

            if (isRecentCategory)
            {
                RecentDestinationItem itemScript = infraObj.GetComponent<RecentDestinationItem>();
                if (itemScript != null)
                {
                    SavedNavigation navData = itemScript.GetNavigationData();
                    if (navData != null)
                    {
                        string routeText = $"{navData.startNodeName} {navData.endNodeName}".ToLower();
                        bool matches = routeText.Contains(searchText);

                        infraObj.SetActive(matches);

                        if (matches)
                        {
                            matchingIds.Add(infraObj.name);
                        }
                    }
                }
            }
            else if (isBookmarksCategory)
            {
                SavedInfrastructureItem itemScript = infraObj.GetComponent<SavedInfrastructureItem>();
                if (itemScript != null)
                {
                    string infraName = itemScript.GetInfrastructureName().ToLower().Trim();
                    bool matches = infraName.Contains(searchText);

                    infraObj.SetActive(matches);

                    if (matches)
                    {
                        matchingIds.Add(infraObj.name);
                    }
                }
            }
            else
            {
                ExploreInfrastructureItem itemScript = infraObj.GetComponent<ExploreInfrastructureItem>();
                if (itemScript != null)
                {
                    string infraName = itemScript.GetInfrastructureName().ToLower().Trim();
                    bool matches = infraName.Contains(searchText);

                    infraObj.SetActive(matches);

                    if (matches)
                    {
                        matchingIds.Add(infraObj.name);
                    }
                }
            }
        }

        return matchingIds;
    }

    public void ShowAllInfrastructures()
    {
        if (spawnedInfrastructures == null)
            return;

        foreach (GameObject infraObj in spawnedInfrastructures)
        {
            if (infraObj != null)
            {
                infraObj.SetActive(true);
            }
        }
    }
}