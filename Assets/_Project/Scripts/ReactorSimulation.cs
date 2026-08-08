using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ReactorSimulation : MonoBehaviour
{
    [Header("1. Mesh Objects (2mm, 4mm, 6mm, 8mm)")]
    public GameObject[] catalystMeshes;
    public TMP_Dropdown meshDropdown;

    [Header("2. Bed Depth Dropdown (Changes Y-Axis Scale)")]
    public TMP_Dropdown bedDepthDropdown;
    private float[] meshBaseYScales = { 0.5f, 1f, 1.5f, 2f }; // Adjust multipliers as needed

    [Header("3. Engage Reaction Button & Particles")]
    public Button engageButton;
    public ParticleSystem gasInletParticles;
    public ParticleSystem gasOutletParticles;
    private bool isSimulationActive = false;

    [Header("4. Gas Volume Load Slider")]
    public Slider gasVolumeSlider;
    private float baseInletRate = 30f;
    private float baseOutletRate = 30f;

    [Header("5. Inlet H2S Concentration Slider")]
    public Slider h2sSlider;
    public Color lightGreenH2S = new Color(0.7f, 0.9f, 0.4f, 0.5f);
    public Color darkGreenH2S = new Color(0.1f, 0.4f, 0.05f, 0.7f);

    [Header("6. Temperature Control")]
    public Slider temperatureSlider;
    public Renderer reactorMainBodyRenderer;
    public Color baseReactorColor = new Color(122f, 122f, 122f, 255f);
    public Color heatedReactorColor = new Color(150.45f, 150.2f, 150.2f, 255f); // Reddish tint
    public Transform temperatureNeedle; // Drag the needle object of the temperature gauge here

    [Header("7. Zoom Settings")]
    public Camera studioCamera;
    public float zoomSpeed = 2f;
    public float minZoom = 3f;
    public float maxZoom = 15f;

    [Header("8. Sprint Gate Generation Button")]
    public Button btnGenerateModel; // [NEW] We will hook the Generate button here!

    private Material[] instancedMaterials;
    private Material reactorMaterial;
    private float simulationTimer = 0f;
    private float simulationDuration = 10f;

    // Base color tracking for rust simulation
    private Color freshColor = new Color(0.8f, 0.45f, 0.15f, 1f);
    private Color exhaustedColor = new Color(0.3f, 0.4f, 0.2f, 1f);
    static private Color defaultReactorColor = new Color();

    void Start()
    {
        try
        {
            // Initialize Mesh Materials
            instancedMaterials = new Material[catalystMeshes.Length];
            for (int i = 0; i < catalystMeshes.Length; i++)
            {
                if (catalystMeshes[i] != null)
                {
                    Renderer r = catalystMeshes[i].GetComponent<Renderer>();
                    instancedMaterials[i] = r.material;
                }
            }

            // Initialize Reactor Body Material
            if (reactorMainBodyRenderer != null)
            {
                reactorMaterial = reactorMainBodyRenderer.material;
            }

            // [FIXED] We now listen to the Generate Button instead of the Dropdown changing!
            if (btnGenerateModel != null && meshDropdown != null)
            {
                btnGenerateModel.onClick.AddListener(() => OnMeshChanged(meshDropdown.value));
            }

            // Setup other UI Listeners
            if (engageButton != null) engageButton.onClick.AddListener(StartSimulation);
            if (gasVolumeSlider != null) gasVolumeSlider.onValueChanged.AddListener(OnGasVolumeChanged);
            if (h2sSlider != null) h2sSlider.onValueChanged.AddListener(OnH2SChanged);
            if (temperatureSlider != null) temperatureSlider.onValueChanged.AddListener(OnTemperatureChanged);

            // Stop particles initially
            if (gasInletParticles != null) gasInletParticles.Stop();
            if (gasOutletParticles != null) gasOutletParticles.Stop();

            // [RESTORED] Call this once on Start so the reactor isn't invisible when you hit Play!
            if (meshDropdown != null) OnMeshChanged(meshDropdown.value);

            if (reactorMaterial != null)
            {
                defaultReactorColor = reactorMaterial.GetColor("_BaseColor");
            }

        }
        catch (System.Exception)
        {
        }
    }

    void Update()
    {
        try
        {
            // Handle Zoom via Mouse Scroll
            HandleZoom();

            // Handle Rust Simulation over time once engaged
            if (isSimulationActive && simulationTimer < simulationDuration)
            {
                simulationTimer += Time.deltaTime;
                float progress = simulationTimer / simulationDuration;
                Color currentRustColor = Color.Lerp(freshColor, exhaustedColor, progress);

                for (int i = 0; i < instancedMaterials.Length; i++)
                {
                    if (instancedMaterials[i] != null)
                    {
                        instancedMaterials[i].SetColor("_BaseColor", currentRustColor);
                    }
                }
            }

        }
        catch (System.Exception)
        {

        }
    }

    // 1 & 2. Mesh Selection & Bed Depth Scaling
    void OnMeshChanged(int index)
    {
        try
        {
            for (int i = 0; i < catalystMeshes.Length; i++)
            {
                if (catalystMeshes[i] != null)
                {
                    bool active = (i == index);
                    catalystMeshes[i].SetActive(active);

                    // Also apply Bed Depth scale adjustment if this mesh is active
                    if (active && bedDepthDropdown != null)
                    {
                        ApplyBedDepthScale(catalystMeshes[i], bedDepthDropdown.value);
                    }
                }
            }
        }
        catch (System.Exception)
        {
        }
    }

    void OnBedDepthChanged(int depthIndex)
    {
        try
        {
            if (meshDropdown != null)
            {
                int activeMeshIndex = meshDropdown.value;
                if (catalystMeshes[activeMeshIndex] != null)
                {
                    ApplyBedDepthScale(catalystMeshes[activeMeshIndex], depthIndex);
                }
            }

        }
        catch (System.Exception)
        {
        }
    }

    void ApplyBedDepthScale(GameObject meshObj, int depthIndex)
    {
        //    Vector3 currentScale = meshObj.transform.localScale;
        //    float depthMultiplier = (depthIndex < meshBaseYScales.Length) ? meshBaseYScales[depthIndex] : 1f;
        //    meshObj.transform.localScale = new Vector3(currentScale.x, depthMultiplier, currentScale.z);
    }

    // 3. Start Simulation on Button Click
    void StartSimulation()
    {
        simulationTimer = 0f;

        if (gasInletParticles != null)
        {
            if (isSimulationActive)
            {
                isSimulationActive = false;
                gasInletParticles.Stop();
                gasOutletParticles.Stop();
            }
            else
            {
                isSimulationActive = true;
                gasInletParticles.Play();
                gasOutletParticles.Play();
            }
        }
    }

    // 4. Gas Volume Load Slider
    void OnGasVolumeChanged(float sliderValue)
    {
        if (gasInletParticles != null)
        {
            var emissionIn = gasInletParticles.emission;
            emissionIn.rateOverTime = baseInletRate * sliderValue;
        }
        if (gasOutletParticles != null)
        {
            var emissionOut = gasOutletParticles.emission;
            emissionOut.rateOverTime = baseOutletRate * sliderValue;
        }
    }

    // 5. Inlet H2S Concentration Slider (Changes Gas Color)
    void OnH2SChanged(float sliderValue)
    {
        if (gasInletParticles != null)
        {
            var mainInlet = gasInletParticles.main;
            Color dynamicH2SColor = Color.Lerp(lightGreenH2S, darkGreenH2S, sliderValue);
            mainInlet.startColor = dynamicH2SColor;
        }
    }

    // 6. System Operating Temperature (Changes Reactor Color & Needle Rotation)
    void OnTemperatureChanged(float tempValue)
    {
        Color targetReactorColor = Color.Lerp(baseReactorColor, heatedReactorColor, tempValue);
        // Tint Reactor Red
        if (reactorMaterial != null)
        {
            if (reactorMaterial.color == targetReactorColor && !isSimulationActive && tempValue == 15)
            {
                reactorMaterial.SetColor("_BaseColor", defaultReactorColor);
            }
            else
            {
                reactorMaterial.SetColor("_BaseColor", targetReactorColor);
            }
        }
    }

    // 7. Zoom In and Out via Mouse Scroll
    void HandleZoom()
    {
        try
        {
            if (studioCamera != null && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                if (studioCamera.orthographic)
                {
                    studioCamera.orthographicSize = Mathf.Clamp(studioCamera.orthographicSize - Input.mouseScrollDelta.y * zoomSpeed, minZoom, maxZoom);
                }
                else
                {
                    // Move camera forward/backward along local Z axis
                    studioCamera.transform.Translate(Vector3.forward * Input.mouseScrollDelta.y * zoomSpeed, Space.Self);

                    // Optional distance clamp for perspective cameras
                    Vector3 pos = studioCamera.transform.localPosition;
                    pos.z = Mathf.Clamp(pos.z, -maxZoom, -minZoom);
                    studioCamera.transform.localPosition = pos;
                }
            }

        }
        catch (System.Exception)
        {
        }
    }
}