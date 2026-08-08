using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TabNavigationManager : MonoBehaviour
{
    [Header("Screen Panel GameObjects")]
    [Tooltip("Place the corresponding screen panels from the hierarchy here in sequence.")]
    public GameObject[] screenPanels;

    [Header("Navigation Tab Buttons")]
    [Tooltip("Place the interactive tab buttons here in sequence.")]
    public UnityEngine.UI.Button[] tabButtons;

    [Header("SCADA Palette Colors (Set in Inspector)")]
    public Color activeTabBGColor = new Color(0.12f, 0.16f, 0.23f, 1.0f); // Default #1e293b
    public Color activeTextColor = new Color(0.97f, 0.98f, 0.99f, 1.0f);   // Default #f8fafc
    public Color inactiveTextColor = new Color(0.58f, 0.64f, 0.72f, 1.0f); // Default #94a3b8

    private void Awake()
    {
        if (screenPanels.Length == 0 || tabButtons.Length == 0 || screenPanels.Length != tabButtons.Length)
        {
            Debug.LogError("TabNavigationManager error: Screen Panels and Tab Buttons arrays must be assigned and equal in length!");
            return;
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int indexToBePassed = i;
            tabButtons[i].onClick.AddListener(() => SwitchToTabPanel(indexToBePassed));
        }

        SwitchToTabPanel(0);
    }

    public void SwitchToTabPanel(int targetIndex)
    {
        for (int i = 0; i < screenPanels.Length; i++)
        {
            if (screenPanels[i] == null || tabButtons[i] == null) continue;

            Image btnImage = tabButtons[i].GetComponent<Image>();
            TextMeshProUGUI btnText = tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();

            if (i == targetIndex)
            {
                screenPanels[i].SetActive(true);

                if (btnImage != null)
                {
                    btnImage.color = activeTabBGColor; // Uses your color picker choice!
                }
                if (btnText != null)
                {
                    btnText.color = activeTextColor;
                }
            }
            else
            {
                screenPanels[i].SetActive(false);

                if (btnImage != null)
                {
                    // Inactive background goes completely transparent to stay stable in the layout row
                    btnImage.color = new Color(0, 0, 0, 0f);
                }
                if (btnText != null)
                {
                    btnText.color = inactiveTextColor;
                }
            }
        }
    }
}