using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class InfrastructureLabelManager : MonoBehaviour
{
    public static InfrastructureLabelManager Instance { get; private set; }

    private List<InfrastructureLabel> allLabels = new List<InfrastructureLabel>();
    private float checkInterval = 0.5f; // Check for overlaps every 0.5 seconds
    private float lastCheckTime = 0f;

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

    void Update()
    {
        // Periodically check and resolve overlaps
        if (Time.time - lastCheckTime > checkInterval)
        {
            ResolveAllOverlaps();
            lastCheckTime = Time.time;
        }
    }

    public void RegisterLabel(InfrastructureLabel label)
    {
        if (!allLabels.Contains(label))
        {
            allLabels.Add(label);
        }
    }

    public void UnregisterLabel(InfrastructureLabel label)
    {
        allLabels.Remove(label);
    }

    private void ResolveAllOverlaps()
    {
        // First, reset all labels to default position
        foreach (var label in allLabels)
        {
            if (label != null)
            {
                label.ResetToDefaultPosition();
            }
        }

        // Then resolve overlaps using priority (larger labels or more important ones first)
        var sortedLabels = new List<InfrastructureLabel>(allLabels);
        sortedLabels.Sort((a, b) => b.GetPriority().CompareTo(a.GetPriority()));

        foreach (var label in sortedLabels)
        {
            if (label != null)
            {
                label.ResolveOverlap(allLabels);
            }
        }
    }

    public void ClearAllLabels()
    {
        allLabels.Clear();
    }
}