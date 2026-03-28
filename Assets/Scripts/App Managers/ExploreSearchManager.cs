using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ExploreSearchManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField searchField;
    public AccordionManager accordionManager;

    [Header("Settings")]
    public string searchPlaceholder = "Search categories or places...";

    private string currentSearchText = "";
    private List<Infrastructure> allInfrastructures = new List<Infrastructure>();
    private bool infrastructuresLoaded = false;

    void Start()
    {
        if (searchField != null)
        {
            searchField.onValueChanged.AddListener(OnSearchTextChanged);

            if (searchField.placeholder != null)
            {
                TextMeshProUGUI placeholder = searchField.placeholder.GetComponent<TextMeshProUGUI>();
                if (placeholder != null)
                {
                    placeholder.text = searchPlaceholder;
                }
            }
        }

        StartCoroutine(LoadAllInfrastructures());
    }

    private IEnumerator LoadAllInfrastructures()
    {
        yield return StartCoroutine(CrossPlatformFileLoader.LoadJsonFile(
            "infrastructure.json",
            (jsonData) =>
            {
                try
                {
                    string wrappedJson = "{\"infrastructures\":" + jsonData + "}";
                    InfrastructureList list = JsonUtility.FromJson<InfrastructureList>(wrappedJson);
                    allInfrastructures = list.infrastructures
                        .Where(i => !i.is_deleted)
                        .ToList();

                    // Push cached data to each accordion so they can spawn without re-loading
                    if (accordionManager != null && accordionManager.accordionItems != null)
                    {
                        foreach (var accordion in accordionManager.accordionItems)
                        {
                            if (accordion == null) continue;
                            string catId = accordion.GetCategoryId();
                            if (string.IsNullOrEmpty(catId)) continue;
                            var categoryInfras = allInfrastructures
                                .Where(i => i.category_id == catId)
                                .ToList();
                            accordion.SetCachedInfraData(categoryInfras);
                        }
                    }
                }
                catch { }
                infrastructuresLoaded = true;
            },
            (error) => { infrastructuresLoaded = true; }
        ));
    }

    private void OnSearchTextChanged(string searchText)
    {
        currentSearchText = searchText.ToLower().Trim();

        if (string.IsNullOrWhiteSpace(currentSearchText))
        {
            ShowAllAccordions();
            return;
        }

        FilterAccordions(currentSearchText);
    }

    private void ShowAllAccordions()
    {
        if (accordionManager == null || accordionManager.accordionItems == null)
            return;

        foreach (var accordion in accordionManager.accordionItems)
        {
            if (accordion != null)
            {
                accordion.gameObject.SetActive(true);
                accordion.ShowAllInfrastructures();
                accordion.Collapse();
            }
        }
    }

    private void FilterAccordions(string searchText)
    {
        if (accordionManager == null || accordionManager.accordionItems == null)
            return;

        foreach (var accordion in accordionManager.accordionItems)
        {
            if (accordion == null)
                continue;

            string categoryName = accordion.GetCategoryName().ToLower();
            bool categoryMatches = categoryName.Contains(searchText);

            // Always search centrally-loaded data — works even before accordion is expanded
            bool hasMatchingInfras = false;
            string categoryId = accordion.GetCategoryId();
            if (!string.IsNullOrEmpty(categoryId) && infrastructuresLoaded)
            {
                hasMatchingInfras = allInfrastructures.Any(i =>
                    i.category_id == categoryId &&
                    !string.IsNullOrEmpty(i.name) &&
                    i.name.ToLower().Contains(searchText));
            }
            else
            {
                // Fall back to per-accordion search (handles Recent/Saved or when data isn't loaded yet)
                hasMatchingInfras = accordion.FilterInfrastructuresBySearch(searchText).Count > 0;
            }

            if (categoryMatches || hasMatchingInfras)
            {
                accordion.gameObject.SetActive(true);

                if (hasMatchingInfras)
                {
                    accordion.SetPendingFilter(searchText);
                    accordion.Expand();
                }
                else
                {
                    accordion.Collapse();
                }
            }
            else
            {
                accordion.gameObject.SetActive(false);
            }
        }
    }

    void OnDestroy()
    {
        if (searchField != null)
        {
            searchField.onValueChanged.RemoveListener(OnSearchTextChanged);
        }
    }
}
