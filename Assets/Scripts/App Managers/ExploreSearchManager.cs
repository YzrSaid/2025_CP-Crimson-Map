using UnityEngine;
using TMPro;
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

            List<string> matchingInfraIds = accordion.FilterInfrastructuresBySearch(searchText);
            bool hasMatchingInfras = matchingInfraIds.Count > 0;

            if (categoryMatches || hasMatchingInfras)
            {
                accordion.gameObject.SetActive(true);

                if (hasMatchingInfras)
                {
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