using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UnifiedRefinerySimulator : MonoBehaviour
{
    // ==========================================
    // ORIGINAL REFINERY ENGINE VARIABLES
    // ==========================================
    public struct SimulationHistoryRecord
    {
        public string timestamp;
        public bool isSuccess;
        public float efficiency;
        public float dailyCost;
        public float pressureDrop;
        public float outletPpm;
        public string grade;
    }

    private List<SimulationHistoryRecord> simulationHistoryLog = new List<SimulationHistoryRecord>();

    static private float runtimeCountdownClockStatic = 200.0f;
    private float runtimeCountdownClock = runtimeCountdownClockStatic;
    private float dangerousConditionTimer = 0.0f;
    private float meshSaturationAccumulator = 0.0f;

    public enum SimulationState { STANDBY, RUNNING, CONCLUDED }
    private SimulationState currentRunState = SimulationState.STANDBY;

    [Header("Data Profile Pool")]
    public MeshProfile[] materialProfiles;

    [Header("New Sprint Gate Mechanics")]
    public CanvasGroup panel1HardwareCanvasGroup;
    public TMP_Dropdown discreteBedDepthDropdown;
    public Button btnGenerateModel; // Shared with Reactor Simulation

    [Header("Hardware Meshes")]
    public TMP_Dropdown meshOpeningSizeDropdown;
    public GameObject[] catalystMeshes; // Shared with Reactor Simulation

    [Header("Frontend Input Components")]
    public TMP_Dropdown meshMaterialDropdown;
    public Slider sliderGasVolume;
    public Slider sliderH2S;
    public Slider sliderTemperature;

    [Header("User Estimation Inputs")]
    public TMP_InputField expectedEfficiencyInput;
    public TMP_InputField estimatedCostInput;

    [Header("Persistent Viewport Telemetry")]
    public TextMeshProUGUI rightSideEfficiencyText;
    public TextMeshProUGUI rightSideCostText;
    public Button mainRunButton;
    private TextMeshProUGUI runButtonText;

    [Header("Panel 3 Detailed SCADA Readouts")]
    public TextMeshProUGUI scadaPressureDropText;
    public TextMeshProUGUI scadaOutletPpmText;
    public TextMeshProUGUI scadaServiceLifeText;
    public TextMeshProUGUI scadaComplianceStatusText;

    [Header("Performance Tab - Historical Reports")]
    public TextMeshProUGUI historyLogDisplayTexbox;

    [Header("Real-Time Graphing Targets")]
    public RectTransform graphBoundingBox;
    public RectTransform graphTrackingNode;

    [Header("Phase 4 Evaluation Popup Windows")]
    public GameObject evaluationOverlayPanel;
    public TextMeshProUGUI evaluationTitleText;
    public TextMeshProUGUI evaluationReportText;
    public Button restartRunButton;

    [Header("System Application Controls")]
    public Button quitApplicationButton;

    [Header("3D Viewport Live Model Drivers")]
    public Transform dynamicFilterBedTransform;
    public MeshRenderer filterBedMeshRenderer;

    [Header("Dual Particle Process Simulation")]
    public ParticleSystem inletParticles;
    public ParticleSystem outletParticles;

    [Header("Fullscreen Viewport Overlay Controls")]
    public Button maximizeViewportButton;
    public Button closeFullscreenButton;
    public GameObject fullscreenOverlayPanel;

    // Fixed Engineering Refinery Parameters
    private const float columnArea = 2.0f;
    private const float gasViscosity = 1.8e-5f;
    private const float gasDensity = 1.2f;
    private const float electricityCostKwH = 0.15f;
    private const float blowerEfficiency = 0.75f;
    private const float molarWeightH2S = 34.08f;

    private float cachedEfficiency = 0f;
    private float cachedDailyCost = 0f;
    private float cachedPressureDrop = 0f;
    private float cachedOutletPpm = 0f;

    // Background Hardware Configurations 
    private int cachedMaterialIndex = 0;
    private float cachedBedDepthL = 1.2f;
    private int cachedOpeningSizeIndex = 0;

    // ==========================================
    // EXACT VARIABLES PORTED FROM ReactorSimulation.cs
    // ==========================================
    [Header("1. Mesh Objects (2mm, 4mm, 6mm, 8mm)")]
    // Note: catalystMeshes is declared above to prevent C# duplicate name errors.
    public TMP_Dropdown meshDropdown;

    [Header("2. Bed Depth Dropdown (Changes Y-Axis Scale)")]
    public TMP_Dropdown bedDepthDropdown;
    private float[] meshBaseYScales = { 0.5f, 1f, 1.5f, 2f }; 

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
    public Color heatedReactorColor = new Color(150.45f, 150.2f, 150.2f, 255f); 
    public Transform temperatureNeedle; 

    [Header("7. Zoom Settings")]
    public Camera studioCamera;
    public float zoomSpeed = 2f;
    public float minZoom = 3f;
    public float maxZoom = 15f;

    [Header("8. Sprint Gate Generation Button")]
    // Note: btnGenerateModel is declared above to prevent C# duplicate name errors.

    // Hidden State Variables from Reactor Simulation
    private Material[] instancedMaterials;
    private Material reactorMaterial;
    private float simulationTimer = 0f;
    private float simulationDuration = 10f;
    private Color freshColor = new Color(0.8f, 0.45f, 0.15f, 1f);
    private Color exhaustedColor = new Color(0.3f, 0.4f, 0.2f, 1f);
    static private Color defaultReactorColor = new Color();

    private void Start()
    {
        // Engine Initialization
        if (mainRunButton != null)
        {
            runButtonText = mainRunButton.GetComponentInChildren<TextMeshProUGUI>();
            mainRunButton.onClick.AddListener(OnMainRunButtonClicked);
        }

        if (btnGenerateModel != null)
        {
            btnGenerateModel.onClick.AddListener(OnGenerateHardwareModelConfirmed);
        }

        if (restartRunButton != null)
        {
            restartRunButton.onClick.AddListener(ResetSimulationToStandby);
        }

        if (quitApplicationButton != null)
        {
            quitApplicationButton.onClick.AddListener(QuitRefinerySimulator);
        }

        if (maximizeViewportButton != null)
        {
            maximizeViewportButton.onClick.AddListener(() => SetFullscreenOverlayActive(true));
        }
        if (closeFullscreenButton != null)
        {
            closeFullscreenButton.onClick.AddListener(() => SetFullscreenOverlayActive(false));
        }

        // --- Visual System Initialization (Ported from ReactorSimulation) ---
        try
        {
            if (catalystMeshes != null)
            {
                instancedMaterials = new Material[catalystMeshes.Length];
                for (int i = 0; i < catalystMeshes.Length; i++)
                {
                    if (catalystMeshes[i] != null)
                    {
                        Renderer r = catalystMeshes[i].GetComponent<Renderer>();
                        if (r != null) instancedMaterials[i] = r.material;
                    }
                }
            }

            if (reactorMainBodyRenderer != null)
            {
                reactorMaterial = reactorMainBodyRenderer.material;
                defaultReactorColor = reactorMaterial.GetColor("_BaseColor");
            }

            // Hook into the original Reactor Slider if assigned
            if (temperatureSlider != null)
            {
                temperatureSlider.onValueChanged.AddListener(OnTemperatureChanged);
            }
        }
        catch (System.Exception) { }

        ClearParticles();
        ResetSimulationToStandby();
        UpdateHistoryLogDisplayUI();
    }

    private void Update()
    {
        EvaluateSystemPhysics();

        if (currentRunState == SimulationState.RUNNING)
        {
            ProcessSimulationCountdown();

            // --- Rust Lerp Mechanics ---
            try
            {
                if (simulationTimer < simulationDuration)
                {
                    simulationTimer += Time.deltaTime;
                    float progress = simulationTimer / simulationDuration;
                    Color currentRustColor = Color.Lerp(freshColor, exhaustedColor, progress);

                    if (instancedMaterials != null)
                    {
                        for (int i = 0; i < instancedMaterials.Length; i++)
                        {
                            if (instancedMaterials[i] != null)
                            {
                                instancedMaterials[i].SetColor("_BaseColor", currentRustColor);
                            }
                        }
                    }
                }
            }
            catch (System.Exception) { }
        }

        if (fullscreenOverlayPanel != null && fullscreenOverlayPanel.activeSelf)
        {
            HandleFullscreen3DClicking();
        }

        // --- Camera Zoom ---
        HandleZoom();
    }

    private void OnGenerateHardwareModelConfirmed()
    {
        if (meshMaterialDropdown != null) cachedMaterialIndex = meshMaterialDropdown.value;
        if (meshOpeningSizeDropdown != null) cachedOpeningSizeIndex = meshOpeningSizeDropdown.value;

        if (discreteBedDepthDropdown != null)
        {
            switch (discreteBedDepthDropdown.value)
            {
                case 0: cachedBedDepthL = 0.5f; break;
                case 1: cachedBedDepthL = 1.0f; break;
                case 2: cachedBedDepthL = 1.5f; break;
                case 3: cachedBedDepthL = 2.0f; break;
                default: cachedBedDepthL = 1.2f; break;
            }
        }

        Update3DModelStructure();
    }

    private void SetFullscreenOverlayActive(bool isTrue)
    {
        if (fullscreenOverlayPanel != null)
        {
            fullscreenOverlayPanel.SetActive(isTrue);
        }
    }

    private void HandleFullscreen3DClicking()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                string targetedNode = hit.transform.name;
                Debug.Log($"[SCADA Diagnostics] Operator clicked on: {targetedNode}");
            }
        }
    }

    private void OnMainRunButtonClicked()
    {
        if (currentRunState == SimulationState.STANDBY)
        {
            StartActiveSimulationRun();
        }
        else if (currentRunState == SimulationState.RUNNING)
        {
            TriggerSimulationSuccess();
        }
    }

    private void StartActiveSimulationRun()
    {
        currentRunState = SimulationState.RUNNING;
        runtimeCountdownClock = runtimeCountdownClockStatic;
        dangerousConditionTimer = 0.0f;
        meshSaturationAccumulator = 0.0f;
        
        simulationTimer = 0f; // Reset Rust Timer

        ToggleStructuralUIInteractability(false);
        ToggleStreamUIInteractability(true);

        if (expectedEfficiencyInput != null) expectedEfficiencyInput.interactable = false;
        if (estimatedCostInput != null) estimatedCostInput.interactable = false;

        if (runButtonText != null) runButtonText.text = "STOP SIMULATION";
        if (mainRunButton != null) mainRunButton.interactable = true;

        if (inletParticles != null && inletParticles.isStopped)
        {
            inletParticles.Play();
        }
        if (outletParticles != null && outletParticles.isStopped)
        {
            outletParticles.Play();
        }
    }

    private void ProcessSimulationCountdown()
    {
        runtimeCountdownClock -= Time.deltaTime;
        meshSaturationAccumulator += Time.deltaTime / runtimeCountdownClockStatic;

        if (cachedOutletPpm > 5.0f || cachedPressureDrop > 6.5f)
        {
            dangerousConditionTimer += Time.deltaTime;
            if (dangerousConditionTimer >= 3.0f)
            {
                TriggerSimulationFailure();
                return;
            }
        }
        else
        {
            dangerousConditionTimer = Mathf.Max(0f, dangerousConditionTimer - Time.deltaTime);
        }

        if (runtimeCountdownClock <= 0.0f)
        {
            TriggerSimulationSuccess();
        }
        else
        {
            if (runButtonText != null) runButtonText.text = $"STOP SIM ({runtimeCountdownClock.ToString("F0")}s)";
        }
    }

    private void TriggerSimulationFailure()
    {
        currentRunState = SimulationState.CONCLUDED;
        if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(true);

        if (evaluationTitleText != null) evaluationTitleText.text = "<color=red>CRITICAL PLANT DISASTER</color>";

        string diagnosticSummary = cachedOutletPpm > 5.0f
            ? $"Toxic venting breach! Outlet concentrations hit {cachedOutletPpm.ToString("F1")} ppm, violating EPA standards."
            : $"Mechanical housing rupture! Differential pressure hit {cachedPressureDrop.ToString("F2")} kPa, exceeding casing boundaries.";

        if (evaluationReportText != null)
        {
            evaluationReportText.text = $"<b>RUN FAILED</b>\n\n{diagnosticSummary}\n\n" +
                $"<b>Final Telemetry Snapshot:</b>\n" +
                $"Efficiency: {cachedEfficiency.ToString("F1")}%\n" +
                $"Real-time Operational Cost: €{cachedDailyCost.ToString("F2")}/day";
        }

        ArchiveRunToHistoryLog(false, "F");
        ClearParticles();
    }

    private void TriggerSimulationSuccess()
    {
        currentRunState = SimulationState.CONCLUDED;
        if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(true);

        string letterGrade = "D";
        string financialAssessment = "";

        if (cachedDailyCost <= 2200f) { letterGrade = "A"; financialAssessment = "Highly Optimized! Ideal utility balance."; }
        else if (cachedDailyCost <= 3000f) { letterGrade = "B"; financialAssessment = "Acceptable Run. Minor fan workload overheads."; }
        else if (cachedDailyCost <= 4000f) { letterGrade = "C"; financialAssessment = "Inefficient profile asset layouts."; }
        else { letterGrade = "D"; financialAssessment = "Financial Deficit bounds breached."; }

        if (evaluationTitleText != null) evaluationTitleText.text = $"<color=green>SHIFT SUCCESS - GRADE {letterGrade}</color>";

        if (evaluationReportText != null)
        {
            evaluationReportText.text = $"<b>CONGRATULATIONS, OPERATOR!</b>\n" +
                $"{financialAssessment}\n\n" +
                $"• System H2S Scrubbing Efficiency: {cachedEfficiency.ToString("F1")}%\n" +
                $"• Total System Pressure Drop: {cachedPressureDrop.ToString("F2")} kPa";
        }

        ArchiveRunToHistoryLog(true, letterGrade);
        ClearParticles();
    }

    private void ClearParticles()
    {
        if (inletParticles != null) inletParticles.Stop();
        if (outletParticles != null) outletParticles.Stop();
    }

    private void ArchiveRunToHistoryLog(bool wasSuccessful, string gradeEarned)
    {
        SimulationHistoryRecord record = new SimulationHistoryRecord
        {
            timestamp = System.DateTime.Now.ToString("HH:mm:ss"),
            isSuccess = wasSuccessful,
            efficiency = cachedEfficiency,
            dailyCost = cachedDailyCost,
            pressureDrop = cachedPressureDrop,
            outletPpm = cachedOutletPpm,
            grade = gradeEarned
        };

        simulationHistoryLog.Insert(0, record);

        while (simulationHistoryLog.Count > 10)
        {
            simulationHistoryLog.RemoveAt(simulationHistoryLog.Count - 1);
        }

        UpdateHistoryLogDisplayUI();
    }

    private void UpdateHistoryLogDisplayUI()
    {
        if (historyLogDisplayTexbox == null) return;

        if (simulationHistoryLog.Count == 0)
        {
            historyLogDisplayTexbox.text = "<i>No operational simulation run history compiled for this current workspace session yet.</i>";
            return;
        }

        string logCompiledText = "<b>HISTORICAL REFINERY SHIFT PERFORMANCE LOGS (LAST 10 RUNS)</b>\n";
        logCompiledText += "---------------------------------------------------------------------------------\n";

        for (int i = 0; i < simulationHistoryLog.Count; i++)
        {
            var run = simulationHistoryLog[i];
            string statusColor = run.isSuccess ? "green" : "red";
            string statusText = run.isSuccess ? $"SUCCESS (Grade {run.grade})" : "CRITICAL FAILURE";

            logCompiledText += $"[{run.timestamp}] <color={statusColor}><b>{statusText}</b></color> | " +
                               $"Eff: {run.efficiency.ToString("F1")}% | " +
                               $"Cost: €{run.dailyCost.ToString("F0")}/day | " +
                               $"Press: {run.pressureDrop.ToString("F2")} kPa\n";
        }

        historyLogDisplayTexbox.text = logCompiledText;
    }

    private void ResetSimulationToStandby()
    {
        currentRunState = SimulationState.STANDBY;
        runtimeCountdownClock = runtimeCountdownClockStatic;
        dangerousConditionTimer = 0.0f;
        meshSaturationAccumulator = 0.0f;

        if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(false);
        if (fullscreenOverlayPanel != null) fullscreenOverlayPanel.SetActive(false);

        ToggleStructuralUIInteractability(true);
        ToggleStreamUIInteractability(true);

        if (expectedEfficiencyInput != null) expectedEfficiencyInput.interactable = true;
        if (estimatedCostInput != null) estimatedCostInput.interactable = true;

        if (runButtonText != null) runButtonText.text = "ENGAGE REACTOR";
        if (mainRunButton != null) mainRunButton.interactable = true;

        ClearParticles();
        Update3DModelStructure();
    }

    private void ToggleStructuralUIInteractability(bool state)
    {
        if (panel1HardwareCanvasGroup != null)
        {
            panel1HardwareCanvasGroup.interactable = state;
            panel1HardwareCanvasGroup.blocksRaycasts = state;
            panel1HardwareCanvasGroup.alpha = state ? 1.0f : 0.5f;
        }
        else
        {
            if (meshMaterialDropdown != null) meshMaterialDropdown.interactable = state;
            if (discreteBedDepthDropdown != null) discreteBedDepthDropdown.interactable = state;
            if (meshOpeningSizeDropdown != null) meshOpeningSizeDropdown.interactable = state;
            if (btnGenerateModel != null) btnGenerateModel.interactable = state;
        }
    }

    private void ToggleStreamUIInteractability(bool state)
    {
        if (sliderGasVolume != null) sliderGasVolume.interactable = state;
        if (sliderH2S != null) sliderH2S.interactable = state;
        if (sliderTemperature != null) sliderTemperature.interactable = state;
    }

    private void Update3DModelStructure()
    {
        if (catalystMeshes != null && catalystMeshes.Length > 0)
        {
            for (int i = 0; i < catalystMeshes.Length; i++)
            {
                if (catalystMeshes[i] != null)
                {
                    catalystMeshes[i].SetActive(i == cachedOpeningSizeIndex);
                }
            }
        }

        if (dynamicFilterBedTransform != null)
        {
            float horizontalScaleModifier = 1.05f;
            dynamicFilterBedTransform.localScale = new Vector3(horizontalScaleModifier, cachedBedDepthL, horizontalScaleModifier);

            float baselineYOffset = -0.4f;
            float adjustedLocalY = baselineYOffset - (cachedBedDepthL * 0.8f);

            dynamicFilterBedTransform.localPosition = new Vector3(
                dynamicFilterBedTransform.localPosition.x,
                adjustedLocalY,
                dynamicFilterBedTransform.localPosition.z
            );
        }
    }

    private void EvaluateSystemPhysics()
    {
        if (materialProfiles == null || materialProfiles.Length == 0) return;

        int selectedIndex = Mathf.Clamp(cachedMaterialIndex, 0, materialProfiles.Length - 1);
        MeshProfile currentMesh = materialProfiles[selectedIndex];
        float bedDepthL = cachedBedDepthL;

        float volumeLoad = sliderGasVolume != null ? sliderGasVolume.value : 5000f;
        float inletC0_ppm = sliderH2S != null ? sliderH2S.value : 1000f;
        float tempC = sliderTemperature != null ? sliderTemperature.value : 25f;

        float flowRateQ = volumeLoad / 3333.3f;
        float meshOpeningDp = 0.002f;

        float superficialVelocity = flowRateQ / columnArea;
        float term1 = 150f * gasViscosity * Mathf.Pow(1f - currentMesh.porosity, 2f) * superficialVelocity;
        float term2 = Mathf.Pow(currentMesh.porosity, 3f) * Mathf.Pow(meshOpeningDp, 2f);
        float term3 = 1.75f * gasDensity * (1f - currentMesh.porosity) * Mathf.Pow(superficialVelocity, 2f);
        float term4 = Mathf.Pow(currentMesh.porosity, 3f) * meshOpeningDp;

        float pressureDropPerMeter = (term1 / term2) + (term3 / term4);
        float totalPressureDropPa = pressureDropPerMeter * bedDepthL;
        cachedPressureDrop = totalPressureDropPa / 1000f;

        float gasContactTime = (columnArea * bedDepthL) / flowRateQ;
        float adjustedKineticK = currentMesh.kineticCoefficient * (1.0f + (tempC - 25f) * 0.005f);
        cachedEfficiency = 100f * (1f - Mathf.Exp(-adjustedKineticK * gasContactTime));
        cachedEfficiency = Mathf.Clamp(cachedEfficiency, 0f, 99.99f);
        cachedOutletPpm = inletC0_ppm * (1f - (cachedEfficiency / 100f));

        float blowerPowerWatts = (flowRateQ * totalPressureDropPa) / blowerEfficiency;
        float blowerPowerKW = blowerPowerWatts / 1000f;
        float dailyEnergyCost = blowerPowerKW * 24f * electricityCostKwH;

        float h2sMassFlowKgPerSec = (flowRateQ * (inletC0_ppm * 1.2f * molarWeightH2S)) / 1e6f;
        float dailyCapturedH2SKg = h2sMassFlowKgPerSec * 86400f * (cachedEfficiency / 100f);

        float dailyRegenerationCost = dailyCapturedH2SKg * 1.80f;
        float dailyAmortizationCost = 450f * currentMesh.maintenanceFactor;

        cachedDailyCost = dailyEnergyCost + dailyRegenerationCost + dailyAmortizationCost;

        float totalBedMassKg = (columnArea * bedDepthL) * currentMesh.bulkDensity;
        float totalSorptionCapacityKg = totalBedMassKg * currentMesh.maximumSorptionCapacity;
        float serviceLifeDays = h2sMassFlowKgPerSec > 0 ? (totalSorptionCapacityKg / (h2sMassFlowKgPerSec * 86400f)) : 99f;

        UpdateUserInterfaceDisplay(cachedEfficiency, cachedDailyCost, cachedPressureDrop, cachedOutletPpm, serviceLifeDays);

        if (filterBedMeshRenderer != null)
        {
            Color baselineCleanColor = Color.gray;
            Color saturatedFouledColor = new Color(0.35f, 0.25f, 0.15f, 1f);

            float safetyAlertFactor = Mathf.InverseLerp(0f, 6.5f, cachedPressureDrop);
            Color baseColorState = Color.Lerp(baselineCleanColor, saturatedFouledColor, meshSaturationAccumulator);

            filterBedMeshRenderer.material.color = Color.Lerp(baseColorState, Color.red, safetyAlertFactor);
        }

        if (inletParticles != null)
        {
            var mainModule = inletParticles.main;
            var emissionModule = inletParticles.emission;

            mainModule.startSpeed = flowRateQ * 2.0f;
            emissionModule.rateOverTime = currentRunState == SimulationState.RUNNING ? Mathf.Lerp(20f, 120f, Mathf.InverseLerp(0f, 2000f, inletC0_ppm)) : 0f;

            float toxicityFactor = Mathf.InverseLerp(0f, 2000f, inletC0_ppm);
            mainModule.startColor = Color.Lerp(new Color(0.5f, 0.45f, 0.3f, 0.4f), new Color(0.75f, 0.55f, 0.1f, 0.75f), toxicityFactor);
        }

        if (outletParticles != null)
        {
            var mainModule = outletParticles.main;
            var emissionModule = outletParticles.emission;

            mainModule.startSpeed = flowRateQ * 2.5f;
            emissionModule.rateOverTime = (currentRunState == SimulationState.RUNNING && inletParticles != null) ? inletParticles.emission.rateOverTime.constant : 0f;

            float efficiencyRatio = cachedEfficiency / 100f;
            Color cleanAirColor = new Color(0.4f, 0.75f, 1.0f, 0.3f);
            Color bypassTaintedColor = new Color(0.65f, 0.5f, 0.15f, 0.6f);

            mainModule.startColor = Color.Lerp(bypassTaintedColor, cleanAirColor, efficiencyRatio);
        }
    }

    private void UpdateUserInterfaceDisplay(float eff, float cost, float pressDrop, float outPpm, float days)
    {
        if (rightSideEfficiencyText != null) rightSideEfficiencyText.text = $"{eff.ToString("F1")}%";
        if (rightSideCostText != null) rightSideCostText.text = $"€{cost.ToString("F0")} / day";

        if (scadaPressureDropText != null) scadaPressureDropText.text = $"Pressure Drop: {pressDrop.ToString("F2")} kPa";
        if (scadaOutletPpmText != null) scadaOutletPpmText.text = $"Outlet H2S: {outPpm.ToString("F2")} ppm";
        if (scadaServiceLifeText != null) scadaServiceLifeText.text = $"Service Life: {days.ToString("F1")} Days";

        if (scadaComplianceStatusText != null)
        {
            if (outPpm > 5.0f || pressDrop > 6.5f)
            {
                if (currentRunState == SimulationState.RUNNING)
                {
                    scadaComplianceStatusText.text = $"BREACH BUFFER ACTIVE: {(3.0f - dangerousConditionTimer).ToString("F1")}s!!";
                }
                else
                {
                    scadaComplianceStatusText.text = "Status: NON-COMPLIANT (CRITICAL)";
                }
                scadaComplianceStatusText.color = Color.red;
            }
            else
            {
                scadaComplianceStatusText.text = currentRunState == SimulationState.STANDBY ? "OFFLINE STANDBY" : "Status: OPERATIONAL (SECURE)";
                scadaComplianceStatusText.color = currentRunState == SimulationState.STANDBY ? Color.white : Color.green;
            }
        }

        if (graphBoundingBox != null && graphTrackingNode != null)
        {
            float minEfficiency = 0f, maxEfficiency = 100f;
            float minCost = 0f, maxCost = 8000f;

            float normalizedX = Mathf.InverseLerp(minEfficiency, maxEfficiency, eff);
            float normalizedY = Mathf.InverseLerp(minCost, maxCost, cost);

            float targetX = normalizedX * graphBoundingBox.rect.width;
            float targetY = normalizedY * graphBoundingBox.rect.height;

            float padding = 10f;
            float clampedX = Mathf.Clamp(targetX, padding, graphBoundingBox.rect.width - padding);
            float clampedY = Mathf.Clamp(targetY, padding, graphBoundingBox.rect.height - padding);

            graphTrackingNode.anchoredPosition = new Vector2(clampedX, clampedY);
        }
    }

    private void QuitRefinerySimulator()
    {
        Debug.Log("Application closing event sent successfully.");
        Application.Quit();
    }

    // ==========================================
    // REACTOR SIMULATION METHODS
    // ==========================================
    private void OnTemperatureChanged(float tempValue)
    {
        Color targetReactorColor = Color.Lerp(baseReactorColor, heatedReactorColor, tempValue);
        
        if (reactorMaterial != null)
        {
            if (reactorMaterial.color == targetReactorColor && currentRunState != SimulationState.RUNNING && tempValue == 15)
            {
                reactorMaterial.SetColor("_BaseColor", defaultReactorColor);
            }
            else
            {
                reactorMaterial.SetColor("_BaseColor", targetReactorColor);
            }
        }
    }

    private void HandleZoom()
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
                    studioCamera.transform.Translate(Vector3.forward * Input.mouseScrollDelta.y * zoomSpeed, Space.Self);
                    Vector3 pos = studioCamera.transform.localPosition;
                    pos.z = Mathf.Clamp(pos.z, -maxZoom, -minZoom);
                    studioCamera.transform.localPosition = pos;
                }
            }
        }
        catch (System.Exception) { }
    }
}