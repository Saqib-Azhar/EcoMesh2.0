using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UnifiedRefinerySimulator : MonoBehaviour
{
    // ==========================================
    // REFINERY ENGINE VARIABLES
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

    [Header("Hardware Configuration & Sprint Gates")]
    public CanvasGroup panel1HardwareCanvasGroup;
    public TMP_Dropdown meshMaterialDropdown;
    public TMP_Dropdown discreteBedDepthDropdown;
    public TMP_Dropdown meshOpeningSizeDropdown;
    public Button btnGenerateModel;

    [Header("Hardware Meshes")]
    public GameObject[] catalystMeshes;
    public Renderer reactorMainBodyRenderer;

    [Header("Stream Controls")]
    public Slider gasVolumeSlider;
    public Slider h2sSlider;
    public Slider temperatureSlider;

    [Header("User Estimation Inputs")]
    public Slider expectedEfficiencySlider;
    public Slider estimatedCostSlider;

    [Header("Persistent Viewport Telemetry")]
    public TextMeshProUGUI rightSideEfficiencyText;
    public TextMeshProUGUI rightSideCostText;
    public Button mainRunButton;
    private TextMeshProUGUI runButtonText;

    [Header("Detailed SCADA Readouts")]
    public TextMeshProUGUI scadaPressureDropText;
    public TextMeshProUGUI scadaOutletPpmText;
    public TextMeshProUGUI scadaServiceLifeText;
    public TextMeshProUGUI scadaComplianceStatusText;

    [Header("Historical Reports & Graphing")]
    public TextMeshProUGUI historyLogDisplayTexbox;
    public RectTransform graphBoundingBox;
    public RectTransform graphTrackingNode;

    [Header("Evaluation Popup Windows")]
    public GameObject evaluationOverlayPanel;
    public TextMeshProUGUI evaluationTitleText;
    public TextMeshProUGUI evaluationReportText;
    public Button restartRunButton;

    [Header("System Application Controls")]
    public Button quitApplicationButton;
    public Button maximizeViewportButton;
    public Button closeFullscreenButton;
    public GameObject fullscreenOverlayPanel;

    [Header("Particle Process Simulation")]
    public ParticleSystem inletParticles;
    public ParticleSystem outletParticles;

    [Header("Camera & Zoom Settings")]
    public Camera studioCamera;
    public float zoomSpeed = 2f;
    public float minZoom = 3f;
    public float maxZoom = 15f;

    [Header("Color Configurations")]
    public Color baseReactorColor = new Color(0.48f, 0.48f, 0.48f, 1f);
    public Color heatedReactorColor = new Color(0.85f, 0.25f, 0.15f, 1f);

    // Fixed Engineering Refinery Parameters
    private const float columnArea = 2.0f;
    private const float gasViscosity = 1.8e-5f;
    private const float gasDensity = 1.2f;
    private const float electricityCostKwH = 0.15f; // €0.15 per kWh
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

    // Materials Array
    private Material[] instancedMaterials;
    private Material reactorMaterial;

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

        // --- Visual System Initialization ---
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
        }

        ClearParticles();
        ResetSimulationToStandby();
        UpdateHistoryLogDisplayUI();
        UpdateUnifiedMeshAppearance();
    }

    private void Update()
    {
        EvaluateSystemPhysics();

        if (currentRunState == SimulationState.RUNNING)
        {
            ProcessSimulationCountdown();
        }

        if (fullscreenOverlayPanel != null && fullscreenOverlayPanel.activeSelf)
        {
            HandleFullscreen3DClicking();
        }

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
        UpdateUnifiedMeshAppearance();
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
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log($"[SCADA Diagnostics] Operator clicked on: {hit.transform.name}");
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

        ToggleStructuralUIInteractability(false);
        ToggleStreamUIInteractability(true);

        if (runButtonText != null) runButtonText.text = "STOP SIMULATION";
        if (mainRunButton != null) mainRunButton.interactable = true;

        if (inletParticles != null && inletParticles.isStopped) inletParticles.Play();
        if (outletParticles != null && outletParticles.isStopped) outletParticles.Play();
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
                $"• Total System Pressure Drop: {cachedPressureDrop.ToString("F2")} kPa\n" +
                $"• Operational Cost: €{cachedDailyCost.ToString("F2")}/day";
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

        if (runButtonText != null) runButtonText.text = "ENGAGE REACTOR";
        if (mainRunButton != null) mainRunButton.interactable = true;

        ClearParticles();
        Update3DModelStructure();
        UpdateUnifiedMeshAppearance();
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

        if (expectedEfficiencySlider != null) expectedEfficiencySlider.interactable = state;
        if (estimatedCostSlider != null) estimatedCostSlider.interactable = state;
    }

    private void ToggleStreamUIInteractability(bool state)
    {
        if (gasVolumeSlider != null) gasVolumeSlider.interactable = state;
        if (h2sSlider != null) h2sSlider.interactable = state;
        if (temperatureSlider != null) temperatureSlider.interactable = state;
    }

    private void Update3DModelStructure()
    {
        if (catalystMeshes != null && catalystMeshes.Length > 0)
        {
            // Map Bed Depth Dropdown index directly to target Scale X
            float targetScaleX = 100f;
            if (discreteBedDepthDropdown != null)
            {
                switch (discreteBedDepthDropdown.value)
                {
                    case 0: targetScaleX = 60f; break;  // 0.5 meters
                    case 1: targetScaleX = 70f; break;  // 1.0 meters
                    case 2: targetScaleX = 80f; break;  // 1.5 meters
                    case 3: targetScaleX = 100f; break; // 2.0 meters
                    default: targetScaleX = 100f; break;
                }
            }

            for (int i = 0; i < catalystMeshes.Length; i++)
            {
                if (catalystMeshes[i] != null)
                {
                    // Cumulative Activation: Enable all meshes up to the currently selected opening size index
                    catalystMeshes[i].SetActive(i <= cachedOpeningSizeIndex);

                    // Update ONLY Scale X while strictly maintaining default Scale Y = 100, Scale Z = 100
                    catalystMeshes[i].transform.localScale = new Vector3(targetScaleX, 100f, 100f);
                }
            }
        }
    }

    private void EvaluateSystemPhysics()
    {
        if (materialProfiles == null || materialProfiles.Length == 0) return;

        int selectedIndex = Mathf.Clamp(cachedMaterialIndex, 0, materialProfiles.Length - 1);
        MeshProfile currentMesh = materialProfiles[selectedIndex];
        float bedDepthL = cachedBedDepthL;

        float volumeLoad = gasVolumeSlider != null ? gasVolumeSlider.value : 5000f;
        float inletC0_ppm = h2sSlider != null ? h2sSlider.value : 1000f;
        float tempC = temperatureSlider != null ? temperatureSlider.value : 25f;

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
        UpdateUnifiedMeshAppearance();

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

    private Color GetMaterialBaseColor(int materialIndex)
    {
        switch (materialIndex)
        {
            case 0: return new Color(0.75f, 0.75f, 0.78f); // Stainless Steel 
            case 1: return new Color(0.65f, 0.70f, 0.65f); // Monel Alloy 400 
            case 2: return new Color(0.95f, 0.95f, 0.95f); // PTFE 
            case 3: return new Color(0.60f, 0.60f, 0.60f); // Titanium Grade 2 
            default: return Color.gray;
        }
    }

    private void UpdateUnifiedMeshAppearance()
    {
        // --- 1. INNER CATALYST MESHES (The 4 Objects: 2mm, 4mm, 6mm, 8mm) ---
        Color baseMatColor = GetMaterialBaseColor(cachedMaterialIndex);

        // Progressively darken inner mesh depending on gas processing accumulation
        float saturationFactor = Mathf.Clamp01(meshSaturationAccumulator);
        Color darkenedColor = Color.Lerp(baseMatColor, baseMatColor * 0.25f, saturationFactor);

        // Continuously influence color via Current Temperature Slider
        float currentTemp = temperatureSlider != null ? temperatureSlider.value : 15f;
        float tempNormalized = Mathf.Clamp01(Mathf.InverseLerp(15f, 200f, currentTemp));
        Color tempInfluencedColor = Color.Lerp(darkenedColor, heatedReactorColor, tempNormalized);

        // Overwrite with safety alert if mechanical housing limits are breached
        float safetyAlertFactor = Mathf.Clamp01(Mathf.InverseLerp(0f, 6.5f, cachedPressureDrop));
        Color finalInnerMeshColor = Color.Lerp(tempInfluencedColor, Color.red, safetyAlertFactor);

        // Apply final color directly to the array of 4 Catalyst Meshes
        if (instancedMaterials != null)
        {
            for (int i = 0; i < instancedMaterials.Length; i++)
            {
                if (instancedMaterials[i] != null)
                {
                    instancedMaterials[i].color = finalInnerMeshColor;
                    instancedMaterials[i].SetColor("_BaseColor", finalInnerMeshColor);
                }
            }
        }

        // --- 2. OUTER REACTOR CYLINDER ---
        // The outer cylinder should ONLY react to temperature, unaffected by the catalyst material selected.
        if (reactorMaterial != null)
        {
            Color targetReactorColor = Color.Lerp(baseReactorColor, heatedReactorColor, tempNormalized);
            reactorMaterial.color = targetReactorColor;
            reactorMaterial.SetColor("_BaseColor", targetReactorColor);
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

    private void HandleZoom()
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
}