using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIValueUpdater : MonoBehaviour
{
    [Header("UI Component Associations")]
    public Slider targetSlider;
    public TextMeshProUGUI readoutLabel;

    [Header("Formatting Settings")]
    public string engineeringUnit = "units";
    public int decimalPlaces = 1;

    private void Start()
    {
        if (targetSlider == null || readoutLabel == null)
        {
            Debug.LogWarning($"UIValueUpdater missing assignments on: {gameObject.name}");
            return;
        }

        // Programmatically add a listener to catch real-time slider updates
        targetSlider.onValueChanged.AddListener(ProcessSliderValueDisplay);

        // Force a calculation check right at startup using the slider's default position
        ProcessSliderValueDisplay(targetSlider.value);
    }

    public void ProcessSliderValueDisplay(float rawValue)
    {
        // Build precision string formatting template (e.g., "F0", "F1", "F2")
        string formatSpecifier = "F" + decimalPlaces;

        // Print formatted text to UI label component
        readoutLabel.text = $"{rawValue.ToString(formatSpecifier)} {engineeringUnit}";
    }

    private void OnDestroy()
    {
        if (targetSlider != null)
        {
            targetSlider.onValueChanged.RemoveListener(ProcessSliderValueDisplay);
        }
    }
}