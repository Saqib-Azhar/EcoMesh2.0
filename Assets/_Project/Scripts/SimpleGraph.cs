using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasRenderer))]
public class LiveECGGraph : MaskableGraphic
{
    [Header("Simulation Control")]
    [Tooltip("Set to true via script when the simulation starts to begin scrolling.")]
    public bool isSimulationRunning = false;

    [Header("ECG Configuration")]
    public int maxDataPoints = 60;
    public float updateRate = 1.0f;
    public float lineThickness = 3.0f;

    [Tooltip("The color of the live data line. Change this for each UI Panel.")]
    public Color lineColor = Color.yellow;

    [Tooltip("How much variance = 100% deflection. Make this SMALL for Efficiency (e.g. 1.0) and LARGE for Temp (e.g. 100.0)")]
    public float tolerance = 5.0f;

    [Header("Center Target Line")]
    public Color centerLineColor = new Color(0.9f, 0.1f, 0.1f, 0.8f);
    public float centerLineThickness = 2.0f;

    // Telemetry Caches
    private float currentActual = 0f;
    private float currentTarget = 0f;

    private List<float> history = new List<float>();
    private float timer = 0f;

    /// <summary>
    /// Feed data into this specific graph. Call this from UnifiedRefinerySimulator.
    /// </summary>
    public void UpdateTelemetry(float actual, float target)
    {
        currentActual = actual;
        currentTarget = target;
    }

    private void Update()
    {
        // 1. STOP if the simulation hasn't started yet.
        if (!isSimulationRunning) return;

        timer += Time.deltaTime;

        // Force UI redraw to keep the line moving smoothly
        SetVerticesDirty();

        if (timer >= updateRate)
        {
            timer -= updateRate;

            // Calculate variance relative to the target
            float diff = currentActual - currentTarget;

            // Prevent division by zero
            float safeTolerance = Mathf.Max(tolerance, 0.001f);

            // Normalize between -1 (bottom of graph) and 1 (top of graph)
            float normalized = Mathf.Clamp(diff / safeTolerance, -1f, 1f);

            history.Add(normalized);

            if (history.Count > maxDataPoints) history.RemoveAt(0);
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect graphRect = rectTransform.rect;
        float xMin = graphRect.xMin;
        float xMax = graphRect.xMax;
        float yCenter = graphRect.center.y;
        float width = graphRect.width;
        float halfHeight = (graphRect.height / 2f) - 5f;

        // Draw the static center red line
        DrawLineSegment(vh, new Vector2(xMin, yCenter), new Vector2(xMax, yCenter), centerLineColor, centerLineThickness);

        if (history.Count < 2) return;

        float xSpacing = width / (maxDataPoints - 1);
        float scrollOffset = (timer / updateRate) * xSpacing;

        // Draw the historical data points moving from right to left
        for (int i = 0; i < history.Count - 1; i++)
        {
            int indexFromRight1 = (history.Count - 1) - i;
            int indexFromRight2 = (history.Count - 1) - (i + 1);

            float x1 = xMax - (indexFromRight1 * xSpacing) - scrollOffset;
            float x2 = xMax - (indexFromRight2 * xSpacing) - scrollOffset;

            float y1 = yCenter + (history[i] * halfHeight);
            float y2 = yCenter + (history[i + 1] * halfHeight);

            DrawLineSegment(vh, new Vector2(x1, y1), new Vector2(x2, y2), lineColor, lineThickness);
        }

        // Draw the live leading edge connecting the history to the exact current frame
        if (history.Count > 0)
        {
            float lastX = xMax - scrollOffset;
            float liveX = xMax + (xSpacing - scrollOffset);

            float lastY = yCenter + (history[history.Count - 1] * halfHeight);

            float diff = currentActual - currentTarget;
            float safeTolerance = Mathf.Max(tolerance, 0.001f);
            float liveY = yCenter + (Mathf.Clamp(diff / safeTolerance, -1f, 1f) * halfHeight);

            DrawLineSegment(vh, new Vector2(lastX, lastY), new Vector2(liveX, liveY), lineColor, lineThickness);
        }
    }

    private void DrawLineSegment(VertexHelper vh, Vector2 start, Vector2 end, Color color, float thickness)
    {
        Vector2 dir = (end - start).normalized;
        if (dir == Vector2.zero) return;

        Vector2 normal = new Vector2(-dir.y, dir.x) * (thickness / 2f);

        UIVertex v1 = UIVertex.simpleVert; v1.color = color; v1.position = start - normal;
        UIVertex v2 = UIVertex.simpleVert; v2.color = color; v2.position = start + normal;
        UIVertex v3 = UIVertex.simpleVert; v3.color = color; v3.position = end + normal;
        UIVertex v4 = UIVertex.simpleVert; v4.color = color; v4.position = end - normal;

        int startIndex = vh.currentVertCount;
        vh.AddVert(v1);
        vh.AddVert(v2);
        vh.AddVert(v3);
        vh.AddVert(v4);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }
}