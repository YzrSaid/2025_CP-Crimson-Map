using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class InfrastructureLabel : MonoBehaviour
{
    [Header("References")]
    public TextMeshPro textMesh;
    public Transform markerTransform;
    public LineRenderer leaderLine;

    [Header("Settings")]
    public float defaultYOffset = 4f;
    public float separationDistance = 1.5f;
    public float markerAvoidanceRadius = 2f; // NEW: How far to stay away from markers
    public bool useLeaderLine = true;
    public Color leaderLineColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    [Header("Priority")]
    public int priority = 0;

    private Vector3 defaultLocalPosition;
    private Vector3 currentOffset;
    private Bounds textBounds;

    // Updated offset positions - removed positions at marker height (y=0)
    private Vector3[] offsetPositions = new Vector3[]
   {
    new Vector3(0, 3, 0),      // High above (default)
    new Vector3(2, 3, 0),      // High right
    new Vector3(-2, 3, 0),     // High left
    new Vector3(3, 3, 0),      // Far right
    new Vector3(-3, 3, 0),     // Far left
    new Vector3(2, 4, 0),      // Very high right
    new Vector3(-2, 4, 0),     // Very high left
    new Vector3(0, 4, 0),      // Very high above
    new Vector3(3, 4, 0),      // Extra far high right
    new Vector3(-3, 4, 0),     // Extra far high left
   };

    void Start()
    {
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshPro>();
        }

        defaultLocalPosition = transform.localPosition;
        currentOffset = Vector3.up * defaultYOffset;

        if (leaderLine == null && useLeaderLine)
        {
            GameObject lineObj = new GameObject("LeaderLine");
            lineObj.transform.SetParent(transform.parent);
            leaderLine = lineObj.AddComponent<LineRenderer>();

            leaderLine.startWidth = 0.05f;
            leaderLine.endWidth = 0.05f;
            leaderLine.material = new Material(Shader.Find("Sprites/Default"));
            leaderLine.startColor = leaderLineColor;
            leaderLine.endColor = leaderLineColor;
            leaderLine.positionCount = 2;
            leaderLine.enabled = false;
        }

        if (InfrastructureLabelManager.Instance != null)
        {
            InfrastructureLabelManager.Instance.RegisterLabel(this);
        }

        UpdateBounds();
    }

    void OnDestroy()
    {
        if (InfrastructureLabelManager.Instance != null)
        {
            InfrastructureLabelManager.Instance.UnregisterLabel(this);
        }

        if (leaderLine != null)
        {
            Destroy(leaderLine.gameObject);
        }
    }

    public void ResetToDefaultPosition()
    {
        currentOffset = Vector3.up * defaultYOffset;
        ApplyOffset();

        if (leaderLine != null)
        {
            leaderLine.enabled = false;
        }
    }

    public void ResolveOverlap(List<InfrastructureLabel> allLabels)
    {
        UpdateBounds();

        foreach (var offset in offsetPositions)
        {
            currentOffset = offset;
            ApplyOffset();
            UpdateBounds();

            bool hasOverlap = false;

            // Check overlap with own marker
            if (IsOverlappingMarker())
            {
                hasOverlap = true;
                continue; // Try next position
            }

            // Check overlap with other labels
            foreach (var otherLabel in allLabels)
            {
                if (otherLabel == null || otherLabel == this)
                    continue;

                float distance = Vector3.Distance(transform.position, otherLabel.transform.position);
                if (distance > 10f) continue;

                if (CheckOverlap(otherLabel))
                {
                    hasOverlap = true;
                    break;
                }

                // Also check if we're overlapping their marker
                if (otherLabel.markerTransform != null)
                {
                    float distanceToOtherMarker = Vector3.Distance(transform.position, otherLabel.markerTransform.position);
                    if (distanceToOtherMarker < markerAvoidanceRadius)
                    {
                        hasOverlap = true;
                        break;
                    }
                }
            }

            if (!hasOverlap)
            {
                UpdateLeaderLine();
                return;
            }
        }

        // If all positions overlap, use the highest one (safest)
        currentOffset = offsetPositions[offsetPositions.Length - 1];
        ApplyOffset();
        UpdateLeaderLine();
    }

    private bool IsOverlappingMarker()
    {
        if (markerTransform == null)
            return false;

        // Check if label is too close to its own marker
        float distanceToMarker = Vector3.Distance(transform.position, markerTransform.position);

        return distanceToMarker < markerAvoidanceRadius;
    }

    private void ApplyOffset()
    {
        if (markerTransform != null)
        {
            transform.position = markerTransform.position + currentOffset;
        }
        else
        {
            transform.localPosition = defaultLocalPosition + currentOffset;
        }
    }

    private void UpdateBounds()
    {
        if (textMesh != null)
        {
            textBounds = textMesh.bounds;
            textBounds.Expand(separationDistance);
        }
    }

    private bool CheckOverlap(InfrastructureLabel other)
    {
        if (other == null || other.textMesh == null)
            return false;

        return textBounds.Intersects(other.textBounds);
    }

    private void UpdateLeaderLine()
    {
        if (leaderLine == null || markerTransform == null)
            return;

        float distanceFromDefault = Vector3.Distance(currentOffset, Vector3.up * defaultYOffset);

        if (distanceFromDefault > 0.5f && useLeaderLine)
        {
            leaderLine.enabled = true;
            leaderLine.SetPosition(0, markerTransform.position + Vector3.up * 0.5f);
            leaderLine.SetPosition(1, transform.position);
        }
        else
        {
            leaderLine.enabled = false;
        }
    }

    public int GetPriority()
    {
        return priority;
    }

    void LateUpdate()
    {
        if (Camera.main != null)
        {
            transform.LookAt(Camera.main.transform);
            transform.Rotate(0, 180, 0);
        }
    }
}