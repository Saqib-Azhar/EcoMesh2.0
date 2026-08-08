using UnityEngine;
using UnityEngine.UI;

public class SimpleGraph : MonoBehaviour
{
    public Color lineColor = Color.green;

    public int pointCount = 40;

    public float graphHeight = 60f;

    public float animationSpeed = 2f;

    private RectTransform rect;

    private Image[] points;

    private float[] values;

    void Start()
    {
        rect = GetComponent<RectTransform>();

        values = new float[pointCount];
        points = new Image[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            values[i] = Random.Range(0.6f, 0.95f);

            GameObject p = new GameObject("Point_" + i);

            p.transform.SetParent(transform, false);

            Image img = p.AddComponent<Image>();

            img.color = lineColor;

            RectTransform rt = img.rectTransform;

            rt.sizeDelta = new Vector2(5, 5); 
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            points[i] = img;
        }

        DrawGraph();
    }

    void Update()
    {
        for (int i = 0; i < values.Length - 1; i++)
            values[i] = values[i + 1];

        values[values.Length - 1] =
            Mathf.Clamp(
                values[values.Length - 2] +
                Random.Range(-0.02f, 0.02f),
                0.55f,
                1f);

        DrawGraph();
    }

    void DrawGraph()
    {
        float width = rect.rect.width;

        for (int i = 0; i < pointCount; i++)
        {
            float x = i * width / (pointCount - 1);

            float y = Mathf.Lerp(10, rect.rect.height - 10, values[i]);
           
            points[i].rectTransform.anchoredPosition =
                new Vector2(x, y);
        }
    }
}