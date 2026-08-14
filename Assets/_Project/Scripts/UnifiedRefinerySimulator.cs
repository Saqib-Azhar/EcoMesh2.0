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

    // Updated to exactly 20 seconds for the presentation startup phase
    static private float runtimeCountdownClockStatic = 20.0f;
    private float runtimeCountdownClock = runtimeCountdownClockStatic;

    // Saturation accumulator kept purely for visual mesh darkening over time
    private float meshSaturationAccumulator = 0.0f;

    public enum SimulationState { STANDBY, RUNNING, CONCLUDED }
    private SimulationState currentRunState = SimulationState.STANDBY;

    // ==========================================
    // INSPECTOR ASSIGNMENTS (UI & 3D Objects)
    // ==========================================

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

    // ==========================================
    // BACKEND MATH VARIABLES
    // ==========================================
    private float cachedEfficiency = 0f;
    private float cachedDailyCost = 0f;
    private float cachedPressureDrop = 0f;
    private float cachedOutletPpm = 0f;

    private int cachedMaterialIndex = 0;
    private float cachedBedDepthL = 1.2f;
    private int cachedOpeningSizeIndex = 0;

    private Material[] instancedMaterials;
    private Material reactorMaterial;

    private void Start()
    {
        if (mainRunButton != null)
        {
            runButtonText = mainRunButton.GetComponentInChildren<TextMeshProUGUI>();
            mainRunButton.onClick.AddListener(OnMainRunButtonClicked);
        }

        if (btnGenerateModel != null) btnGenerateModel.onClick.AddListener(OnGenerateHardwareModelConfirmed);
        if (restartRunButton != null) restartRunButton.onClick.AddListener(ResetSimulationToStandby);
        if (quitApplicationButton != null) quitApplicationButton.onClick.AddListener(QuitRefinerySimulator);
        if (maximizeViewportButton != null) maximizeViewportButton.onClick.AddListener(() => SetFullscreenOverlayActive(true));
        if (closeFullscreenButton != null) closeFullscreenButton.onClick.AddListener(() => SetFullscreenOverlayActive(false));

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

        if (reactorMainBodyRenderer != null) reactorMaterial = reactorMainBodyRenderer.material;

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
        if (fullscreenOverlayPanel != null) fullscreenOverlayPanel.SetActive(isTrue);
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
            // Optional: You can remove this if you want to force users to wait the full 20s
            FinishAndEvaluateRun();
        }
    }

    private void StartActiveSimulationRun()
    {
        currentRunState = SimulationState.RUNNING;
        runtimeCountdownClock = runtimeCountdownClockStatic;
        meshSaturationAccumulator = 0.0f;

        ToggleStructuralUIInteractability(false);
        ToggleStreamUIInteractability(true);

        if (runButtonText != null) runButtonText.text = "STOP SIMULATION";
        if (mainRunButton != null) mainRunButton.interactable = true;

        if (inletParticles != null && inletParticles.isStopped) inletParticles.Play();
        if (outletParticles != null && outletParticles.isStopped) outletParticles.Play();

        SetFullscreenOverlayActive(true);
    }

    // Ticks down the clock WITHOUT interrupting early. Builds suspense.
    private void ProcessSimulationCountdown()
    {
        runtimeCountdownClock -= Time.deltaTime;
        meshSaturationAccumulator += Time.deltaTime / runtimeCountdownClockStatic;

        if (runtimeCountdownClock <= 0.0f)
        {
            FinishAndEvaluateRun(); // Timer finished, evaluate results
        }
        else
        {
            if (runButtonText != null) runButtonText.text = $"SIMULATING ({runtimeCountdownClock.ToString("F0")}s)";
        }
    }

    // Single unified method to handle end of simulation and grade the run
    private void FinishAndEvaluateRun()
    {
        currentRunState = SimulationState.CONCLUDED;
        EvaluateSystemPhysics(); // Guarantee final frame math is accurate

        // Force popup to the absolute front of the UI hierarchy (fixes fullscreen bug)
        if (evaluationOverlayPanel != null)
        {
            evaluationOverlayPanel.transform.SetAsLastSibling();
        }

        string popupTitle = "";
        string popupMessage = "";
        bool runSuccess = false;
        string grade = "F";

        // 1. Engineering Evaluation (Pressure)
        if (cachedPressureDrop > 6.5f)
        {
            popupTitle = "<color=red>CRITICAL PLANT DISASTER</color>";
            popupMessage = $"<b>RUN FAILED</b>\n\nCatastrophic structural failure! Pressure drop hit {cachedPressureDrop:F2} kPa, exceeding casing limits.";
        }
        // 2. Regulatory Evaluation (PPM)
        else if (cachedOutletPpm > 5.0f)
        {
            popupTitle = "<color=red>CRITICAL PLANT DISASTER</color>";
            popupMessage = $"<b>RUN FAILED</b>\n\nToxic venting breach! Outlet concentrations hit {cachedOutletPpm:F1} ppm, violating EPA standards.";
        }
        // 3. Financial Evaluation (Budget)
        else if (cachedDailyCost > 3000f)
        {
            popupTitle = "<color=yellow>BUDGET OVERRUN</color>";
            popupMessage = $"<b>RUN FAILED</b>\n\nSystem operates safely but exceeds daily operating budget.";
        }
        // 4. Success Condition
        else
        {
            runSuccess = true;
            grade = cachedDailyCost <= 2200f ? "A" : "B";
            popupTitle = $"<color=green>SHIFT SUCCESS - GRADE {grade}</color>";
            popupMessage = "<b>CONGRATULATIONS, OPERATOR!</b>\n\nReactor operates safely within all regulatory, structural, and financial limits.";
        }

        // Apply text and telemetry
        if (evaluationTitleText != null) evaluationTitleText.text = popupTitle;
        if (evaluationReportText != null)
        {
            evaluationReportText.text = $"{popupMessage}\n\n<b>Final Telemetry Snapshot:</b>\n" +
                                        $"Efficiency: {cachedEfficiency:F1}%\n" +
                                        $"Real-time Operational Cost: €{cachedDailyCost:F2}/day";
        }

        if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(true);

        ArchiveRunToHistoryLog(runSuccess, grade);
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
        while (simulationHistoryLog.Count > 10) simulationHistoryLog.RemoveAt(simulationHistoryLog.Count - 1);
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
            float targetScaleX = 100f;
            if (discreteBedDepthDropdown != null)
            {
                switch (discreteBedDepthDropdown.value)
                {
                    case 0: targetScaleX = 60f; break;
                    case 1: targetScaleX = 70f; break;
                    case 2: targetScaleX = 80f; break;
                    case 3: targetScaleX = 100f; break;
                    default: targetScaleX = 100f; break;
                }
            }

            for (int i = 0; i < catalystMeshes.Length; i++)
            {
                if (catalystMeshes[i] != null)
                {
                    catalystMeshes[i].SetActive(i <= cachedOpeningSizeIndex);
                    catalystMeshes[i].transform.localScale = new Vector3(targetScaleX, 100f, 100f);
                }
            }
        }
    }

    // ==========================================
    // CORE ENGINEERING MATH ENGINE (Streamlined Presentation Setup)
    // ==========================================
    private void EvaluateSystemPhysics()
    {
        // Setup Variables
        float columnArea = 2.0f;
        float baseKineticK = 1.29f;

        float gasFlowQ = gasVolumeSlider != null ? gasVolumeSlider.value : 750f;
        float inletH2S = h2sSlider != null ? h2sSlider.value : 850f;
        float tempC = temperatureSlider != null ? temperatureSlider.value : 25f;
        float bedDepthL = cachedBedDepthL;

        float superficialVelocity = (gasFlowQ / 3600f) / columnArea;

        // Simplified Pressure Drop
        cachedPressureDrop = (1.5f * superficialVelocity + 0.5f * Mathf.Pow(superficialVelocity, 2)) * bedDepthL;

        // Simplified First Order Kinetics for Efficiency
        float gasContactTime = bedDepthL / (superficialVelocity > 0 ? superficialVelocity : 0.0001f);
        float tempModifier = 1.0f + ((tempC - 25f) * 0.02f);
        float adjustedK = baseKineticK * tempModifier;

        cachedEfficiency = 100f * (1f - Mathf.Exp(-adjustedK * gasContactTime));
        cachedEfficiency = Mathf.Clamp(cachedEfficiency, 0f, 99.99f);

        // Resulting PPM Outlet
        cachedOutletPpm = inletH2S * (1f - (cachedEfficiency / 100f));

        // Simplified Daily Financial Cost (Amortization + Power + Chemicals)
        float blowerPowerKW = (gasFlowQ / 3600f * (cachedPressureDrop * 1000f)) / 0.75f / 1000f;
        float dailyEnergyCost = blowerPowerKW * 24f * 0.15f;
        float dailyCapturedH2SKg = (gasFlowQ * inletH2S * 1.2f * 34.08f) / 1e6f / 3600f * 86400f * (cachedEfficiency / 100f);
        float dailyRegenerationCost = dailyCapturedH2SKg * 1.80f;

        cachedDailyCost = dailyEnergyCost + dailyRegenerationCost + 450f;
        float simulatedServiceLife = 145f - (dailyCapturedH2SKg * 0.1f); // Abstract presentation lifespan metric

        UpdateUserInterfaceDisplay(cachedEfficiency, cachedDailyCost, cachedPressureDrop, cachedOutletPpm, simulatedServiceLife);
        UpdateUnifiedMeshAppearance();

        // --- PARTICLE SYSTEM LOGIC ---
        if (inletParticles != null)
        {
            var mainModule = inletParticles.main;
            var emissionModule = inletParticles.emission;
            mainModule.startSpeed = (gasFlowQ / 3600f) * 2.0f;
            emissionModule.rateOverTime = currentRunState == SimulationState.RUNNING ? Mathf.Lerp(20f, 120f, Mathf.InverseLerp(0f, 2000f, inletH2S)) : 0f;

            float toxicityFactor = Mathf.InverseLerp(0f, 2000f, inletH2S);
            mainModule.startColor = Color.Lerp(new Color(0.5f, 0.45f, 0.3f, 0.4f), new Color(0.75f, 0.55f, 0.1f, 0.75f), toxicityFactor);
        }

        if (outletParticles != null)
        {
            var mainModule = outletParticles.main;
            var emissionModule = outletParticles.emission;
            mainModule.startSpeed = (gasFlowQ / 3600f) * 2.5f;
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
            case 0: return new Color(0.75f, 0.75f, 0.78f);
            case 1: return new Color(0.65f, 0.70f, 0.65f);
            case 2: return new Color(0.95f, 0.95f, 0.95f);
            case 3: return new Color(0.60f, 0.60f, 0.60f);
            default: return Color.gray;
        }
    }

    private void UpdateUnifiedMeshAppearance()
    {
        Color baseMatColor = GetMaterialBaseColor(cachedMaterialIndex);

        float saturationFactor = Mathf.Clamp01(meshSaturationAccumulator);
        Color darkenedColor = Color.Lerp(baseMatColor, baseMatColor * 0.25f, saturationFactor);

        float currentTemp = temperatureSlider != null ? temperatureSlider.value : 35f;
        float tempNormalized = Mathf.Clamp01(Mathf.InverseLerp(35f, 200f, currentTemp));
        Color tempInfluencedColor = Color.Lerp(darkenedColor, heatedReactorColor, tempNormalized);

        float safetyAlertFactor = Mathf.Clamp01(Mathf.InverseLerp(0f, 6.5f, cachedPressureDrop));
        Color finalInnerMeshColor = Color.Lerp(tempInfluencedColor, Color.red, safetyAlertFactor);

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
        if (scadaServiceLifeText != null) scadaServiceLifeText.text = $"Service Life: {Mathf.Max(0, days).ToString("F0")} Days";

        if (scadaComplianceStatusText != null)
        {
            if (outPpm > 5.0f || pressDrop > 6.5f || cost > 3000f)
            {
                scadaComplianceStatusText.text = currentRunState == SimulationState.RUNNING ? "Status: SYSTEM UNDER DURESS" : "Status: NON-COMPLIANT";
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
            float normalizedX = Mathf.InverseLerp(0f, 100f, eff);
            float normalizedY = Mathf.InverseLerp(0f, 8000f, cost);

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