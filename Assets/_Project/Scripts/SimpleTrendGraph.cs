using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SimpleTrendGraph : MonoBehaviour
{
    public int pointCount = 30;
    public float graphWidth = 260f;
    public float graphHeight = 90f;

    public float baseValue = 90f;
    public float variation = 5f;
    public float speed = 1f;

    private LineRenderer line;
    private float[] values;

    void Start()
    {
        line = GetComponent<LineRenderer>();

        line.positionCount = pointCount;
        line.useWorldSpace = false;

        values = new float[pointCount];

        for (int i = 0; i < pointCount; i++)
            values[i] = baseValue;
    }

    void Update()
    {
        for (int i = 0; i < pointCount - 1; i++)
            values[i] = values[i + 1];

        values[pointCount - 1] =
            baseValue +
            Mathf.Sin(Time.time * speed) * variation +
            Random.Range(-0.4f, 0.4f);

        DrawGraph();
    }

    void DrawGraph()
    {
        for (int i = 0; i < pointCount; i++)
        {
            float x = i * graphWidth / (pointCount - 1);

            float y = Mathf.InverseLerp(
                baseValue - variation,
                baseValue + variation,
                values[i]);

            y *= graphHeight;

            line.SetPosition(i, new Vector3(x, y, 0));
        }
    }
}