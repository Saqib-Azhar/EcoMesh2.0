using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LiveECGGraph : MonoBehaviour
{
    [Header("ECG Simulation Config")]
    public int pointCount = 50;
    public float updateRate = 0.5f; // Updates every 0.5 seconds for a heartbeat feel

    [Header("Center Target Line (Red)")]
    public Color centerLineColor = Color.red;
    public float centerLineThickness = 2f;

    [Header("Input 1: Efficiency")]
    public Color color1 = Color.green;
    public TextMeshProUGUI heading1Text;
    public float tolerance1 = 15f; // +/- 15% spread limits before hitting the top/bottom of graph

    [Header("Input 2: Daily Cost")]
    public Color color2 = Color.cyan;
    public TextMeshProUGUI heading2Text;
    public float tolerance2 = 1000f; // +/- €1000 spread limits

    [Header("Input 3: Pressure Drop")]
    public Color color3 = Color.yellow;
    public TextMeshProUGUI heading3Text;
    public float tolerance3 = 3f; // +/- 3 kPa spread limits

    private RectTransform rect;
    private float[] values1, values2, values3;
    private Image[] points1, points2, points3;
    private float timer;

    // Cache for the latest incoming live telemetry
    private float act1, tgt1, act2, tgt2, act3, tgt3;

    void Start()
    {
        rect = GetComponent<RectTransform>();
        CreateStableCenterLine();

        InitLine(ref points1, ref values1, color1);
        InitLine(ref points2, ref values2, color2);
        InitLine(ref points3, ref values3, color3);
    }

    private void InitLine(ref Image[] points, ref float[] values, Color c)
    {
        values = new float[pointCount];
        points = new Image[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            GameObject p = new GameObject("ECG_Node_" + i);
            p.transform.SetParent(transform, false);

            Image img = p.AddComponent<Image>();
            img.color = c;

            RectTransform rt = img.rectTransform;
            rt.sizeDelta = new Vector2(4, 4);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            points[i] = img;
        }
    }

    // This method gets called constantly by your UnifiedRefinerySimulator
    public void FeedTelemetryData(
        float actualEff, float expectedEff,
        float actualCost, float expectedCost,
        float actualPress, float expectedPress)
    {
        act1 = actualEff; tgt1 = expectedEff;
        act2 = actualCost; tgt2 = expectedCost;
        act3 = actualPress; tgt3 = expectedPress;

        // Update the textual headings in front of the graph
        if (heading1Text != null) heading1Text.text = $"Efficiency: <color=#00FF00>{act1:F1}%</color> (Target: {tgt1:F1}%)";
        if (heading2Text != null) heading2Text.text = $"Op. Cost: <color=#00FFFF>€{act2:F0}</color> (Est: €{tgt2:F0})";
        if (heading3Text != null) heading3Text.text = $"Pressure: <color=#FFFF00>{act3:F2} kPa</color> (Base: {tgt3:F1})";
    }

    void Update()
    {
        timer += Time.deltaTime;

        // Only shift the graph forward based on the updateRate
        if (timer >= updateRate)
        {
            timer = 0f;
            ShiftAndCalculateECG(values1, act1, tgt1, tolerance1);
            ShiftAndCalculateECG(values2, act2, tgt2, tolerance2);
            ShiftAndCalculateECG(values3, act3, tgt3, tolerance3);
            DrawGraph();
        }
    }

    private void ShiftAndCalculateECG(float[] vals, float actual, float target, float tolerance)
    {
        // Shift all old data points to the left
        for (int i = 0; i < vals.Length - 1; i++) vals[i] = vals[i + 1];

        // The Math: Actual minus Target. 
        // Positive result = Over the red line. Negative = Under the red line. 0 = On the red line.
        float difference = actual - target;

        // Normalize it against the tolerance so it fits nicely inside the UI Box
        float normalized = difference / tolerance;

        vals[vals.Length - 1] = Mathf.Clamp(normalized, -1f, 1f);
    }

    void DrawGraph()
    {
        float width = rect.rect.width;
        float halfHeight = rect.rect.height / 2f;
        float centerY = halfHeight;

        for (int i = 0; i < pointCount; i++)
        {
            float x = i * width / (pointCount - 1);

            points1[i].rectTransform.anchoredPosition = new Vector2(x, centerY + (values1[i] * (halfHeight - 5f)));
            points2[i].rectTransform.anchoredPosition = new Vector2(x, centerY + (values2[i] * (halfHeight - 5f)));
            points3[i].rectTransform.anchoredPosition = new Vector2(x, centerY + (values3[i] * (halfHeight - 5f)));
        }
    }

    private void CreateStableCenterLine()
    {
        GameObject lineObj = new GameObject("Target_CenterLine_Red");
        lineObj.transform.SetParent(transform, false);

        Image img = lineObj.AddComponent<Image>();
        img.color = centerLineColor;

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(0f, centerLineThickness);
        rt.anchoredPosition = Vector2.zero;
    }
}