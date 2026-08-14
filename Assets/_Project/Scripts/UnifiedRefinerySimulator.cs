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

    static private float runtimeCountdownClockStatic = 20.0f;
    private float runtimeCountdownClock = runtimeCountdownClockStatic;
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

    [Header("Stream Controls (Main UI)")]
    public Slider gasVolumeSlider;
    public Slider h2sSlider;
    public Slider temperatureSlider;

    [Header("Fullscreen Mirrored Controls")]
    public Slider fullscreenGasVolumeSlider;
    public Slider fullscreenH2SSlider;
    public Slider fullscreenTemperatureSlider;
    public Button fullscreenMainRunButton;
    private TextMeshProUGUI fullscreenRunButtonText;

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

    // --- NEW ALARM UI VARIABLES ---
    [Header("Live Alarm & Warning System")]
    public TextMeshProUGUI alarmHeaderStatusText; // The title that turns Red/Green
    public TextMeshProUGUI alarmLogText;          // The 3-line rolling text box
    private List<string> alarmLogs = new List<string>();
    private float alarmUpdateTimer = 0f;
    private bool wasInAlarmState = false;

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
    // BACKEND MATH CACHE
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

        if (fullscreenMainRunButton != null)
        {
            fullscreenRunButtonText = fullscreenMainRunButton.GetComponentInChildren<TextMeshProUGUI>();
            fullscreenMainRunButton.onClick.AddListener(OnMainRunButtonClicked);
        }

        if (btnGenerateModel != null) btnGenerateModel.onClick.AddListener(OnGenerateHardwareModelConfirmed);
        if (restartRunButton != null) restartRunButton.onClick.AddListener(ResetSimulationToStandby);
        if (quitApplicationButton != null) quitApplicationButton.onClick.AddListener(QuitRefinerySimulator);
        if (maximizeViewportButton != null) maximizeViewportButton.onClick.AddListener(() => SetFullscreenOverlayActive(true));
        if (closeFullscreenButton != null) closeFullscreenButton.onClick.AddListener(() => SetFullscreenOverlayActive(false));

        SyncSliders(gasVolumeSlider, fullscreenGasVolumeSlider);
        SyncSliders(h2sSlider, fullscreenH2SSlider);
        SyncSliders(temperatureSlider, fullscreenTemperatureSlider);

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
            ProcessLiveAlarms(); // NEW: Triggers the 5-second polling loop
        }

        if (fullscreenOverlayPanel != null && fullscreenOverlayPanel.activeSelf)
        {
            HandleFullscreen3DClicking();
        }

        HandleZoom();
    }

    // ==========================================
    // LIVE ALARM & WARNING LOGIC
    // ==========================================
    private void ProcessLiveAlarms()
    {
        alarmUpdateTimer += Time.deltaTime;

        // Execute polling exactly every 5 seconds
        if (alarmUpdateTimer >= 5.0f)
        {
            alarmUpdateTimer = 0f;
            EvaluateCurrentAlarms();
        }
    }

    private void EvaluateCurrentAlarms()
    {
        List<string> activeWarnings = new List<string>();

        // Check against the predefined failure thresholds
        if (cachedPressureDrop > 6.5f) activeWarnings.Add($"Pressure Critical ({cachedPressureDrop:F1} kPa)");
        if (cachedOutletPpm > 5.0f) activeWarnings.Add($"Toxic Leak ({cachedOutletPpm:F1} ppm)");
        if (cachedDailyCost > 3000f) activeWarnings.Add($"Budget Overflow (€{cachedDailyCost:F0})");

        string timeStamp = System.DateTime.Now.ToString("HH:mm:ss");
        bool isInAlarmState = activeWarnings.Count > 0;

        if (isInAlarmState)
        {
            // Combine all current issues into one string
            string combinedWarnings = string.Join(" | ", activeWarnings);
            AddMessageToAlarmLog($"[{timeStamp}] <color=red>WARNING: {combinedWarnings}</color>");
            UpdateAlarmHeaderUI(true);
        }
        else if (wasInAlarmState)
        {
            // If there are no issues now, BUT there were issues 5 seconds ago -> System Recovered
            AddMessageToAlarmLog($"[{timeStamp}] <color=green>STABILIZED: All metrics within safe parameters.</color>");
            UpdateAlarmHeaderUI(false);
        }
        // If there are no issues now, and there were no issues before, it remains silent.

        wasInAlarmState = isInAlarmState;
    }

    private void AddMessageToAlarmLog(string message)
    {
        // Insert newest entry at the very top (Index 0)
        alarmLogs.Insert(0, message);

        // Remove the oldest entries from the bottom if the list exceeds 3 items
        if (alarmLogs.Count > 3)
        {
            alarmLogs.RemoveAt(alarmLogs.Count - 1);
        }

        // Apply string to the UI text box
        if (alarmLogText != null)
        {
            alarmLogText.text = string.Join("\n", alarmLogs);
        }
    }

    private void UpdateAlarmHeaderUI(bool hasActiveAlarm)
    {
        if (alarmHeaderStatusText == null) return;

        if (hasActiveAlarm)
        {
            alarmHeaderStatusText.text = "<color=red>ACTIVE WARNINGS</color>";
        }
        else
        {
            alarmHeaderStatusText.text = "<color=green>NO ACTIVE ALARMS</color>";
        }
    }

    // ==========================================
    // UI SYNC & CORE SIMULATION LIFECYCLE
    // ==========================================
    private void SyncSliders(Slider mainSlider, Slider fullScreenSlider)
    {
        if (mainSlider == null || fullScreenSlider == null) return;

        fullScreenSlider.value = mainSlider.value;

        mainSlider.onValueChanged.AddListener((val) =>
        {
            if (fullScreenSlider.value != val) fullScreenSlider.value = val;
        });

        fullScreenSlider.onValueChanged.AddListener((val) =>
        {
            if (mainSlider.value != val) mainSlider.value = val;
        });
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
    }

    private void StartActiveSimulationRun()
    {
        currentRunState = SimulationState.RUNNING;
        runtimeCountdownClock = runtimeCountdownClockStatic;
        meshSaturationAccumulator = 0.0f;

        // Reset alarm state for fresh run
        alarmUpdateTimer = 0f;
        wasInAlarmState = false;
        AddMessageToAlarmLog($"[{System.DateTime.Now.ToString("HH:mm:ss")}] <color=white>Shift Initiated. SCADA polling active.</color>");
        UpdateAlarmHeaderUI(false);

        ToggleStructuralUIInteractability(false);
        ToggleStreamUIInteractability(true);

        SyncRunButtonState("SIMULATING", true);

        if (inletParticles != null && inletParticles.isStopped) inletParticles.Play();
        if (outletParticles != null && outletParticles.isStopped) outletParticles.Play();

        SetFullscreenOverlayActive(true);
    }

    private void ProcessSimulationCountdown()
    {
        runtimeCountdownClock -= Time.deltaTime;
        meshSaturationAccumulator += Time.deltaTime / runtimeCountdownClockStatic;

        if (runtimeCountdownClock <= 0.0f)
        {
            FinishAndEvaluateRun();
        }
        else
        {
            SyncRunButtonState($"SIMULATING ({runtimeCountdownClock.ToString("F0")}s)", true);
        }
    }

    private void FinishAndEvaluateRun()
    {
        currentRunState = SimulationState.CONCLUDED;
        EvaluateSystemPhysics();

        if (evaluationOverlayPanel != null)
        {
            evaluationOverlayPanel.transform.SetAsLastSibling();
        }

        string popupTitle = "";
        string popupMessage = "";
        bool runSuccess = false;
        string grade = "F";

        if (cachedPressureDrop > 6.5f)
        {
            popupTitle = "<color=red>CRITICAL PLANT DISASTER</color>";
            popupMessage = $"<b>RUN FAILED</b>\n\nCatastrophic structural failure! Pressure drop hit {cachedPressureDrop:F2} kPa, exceeding casing limits.";
        }
        else if (cachedOutletPpm > 5.0f)
        {
            popupTitle = "<color=red>CRITICAL PLANT DISASTER</color>";
            popupMessage = $"<b>RUN FAILED</b>\n\nToxic venting breach! Outlet concentrations hit {cachedOutletPpm:F1} ppm, violating EPA standards.";
        }
        else if (cachedDailyCost > 3000f)
        {
            popupTitle = "<color=yellow>BUDGET OVERRUN</color>";
            popupMessage = $"<b>RUN FAILED</b>\n\nSystem operates safely but exceeds daily operating budget.";
        }
        else
        {
            runSuccess = true;
            grade = cachedDailyCost <= 2200f ? "A" : "B";
            popupTitle = $"<color=green>SHIFT SUCCESS - GRADE {grade}</color>";
            popupMessage = "<b>CONGRATULATIONS, OPERATOR!</b>\n\nReactor operates safely within all regulatory, structural, and financial limits.";
        }

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

        // Clear live alarm UI
        alarmLogs.Clear();
        alarmUpdateTimer = 0f;
        wasInAlarmState = false;
        if (alarmLogText != null) alarmLogText.text = "<i>Reactor offline. SCADA monitoring standing by...</i>";
        UpdateAlarmHeaderUI(false);

        if (evaluationOverlayPanel != null) evaluationOverlayPanel.SetActive(false);
        if (fullscreenOverlayPanel != null) fullscreenOverlayPanel.SetActive(false);

        ToggleStructuralUIInteractability(true);
        ToggleStreamUIInteractability(true);

        SyncRunButtonState("ENGAGE REACTOR", true);

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

        if (fullscreenGasVolumeSlider != null) fullscreenGasVolumeSlider.interactable = state;
        if (fullscreenH2SSlider != null) fullscreenH2SSlider.interactable = state;
        if (fullscreenTemperatureSlider != null) fullscreenTemperatureSlider.interactable = state;
    }

    private void SyncRunButtonState(string text, bool isInteractable)
    {
        if (runButtonText != null) runButtonText.text = text;
        if (fullscreenRunButtonText != null) fullscreenRunButtonText.text = text;

        if (mainRunButton != null) mainRunButton.interactable = isInteractable;
        if (fullscreenMainRunButton != null) fullscreenMainRunButton.interactable = isInteractable;
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

    private void EvaluateSystemPhysics()
    {
        float columnArea = 2.0f;
        float baseKineticK = 1.29f;

        float gasFlowQ = gasVolumeSlider != null ? gasVolumeSlider.value : 750f;
        float inletH2S = h2sSlider != null ? h2sSlider.value : 850f;
        float tempC = temperatureSlider != null ? temperatureSlider.value : 25f;
        float bedDepthL = cachedBedDepthL;

        float superficialVelocity = (gasFlowQ / 3600f) / columnArea;

        cachedPressureDrop = (1.5f * superficialVelocity + 0.5f * Mathf.Pow(superficialVelocity, 2)) * bedDepthL;

        float gasContactTime = bedDepthL / (superficialVelocity > 0 ? superficialVelocity : 0.0001f);
        float tempModifier = 1.0f + ((tempC - 25f) * 0.02f);
        float adjustedK = baseKineticK * tempModifier;

        cachedEfficiency = 100f * (1f - Mathf.Exp(-adjustedK * gasContactTime));
        cachedEfficiency = Mathf.Clamp(cachedEfficiency, 0f, 99.99f);

        cachedOutletPpm = inletH2S * (1f - (cachedEfficiency / 100f));

        float blowerPowerKW = (gasFlowQ / 3600f * (cachedPressureDrop * 1000f)) / 0.75f / 1000f;
        float dailyEnergyCost = blowerPowerKW * 24f * 0.15f;
        float dailyCapturedH2SKg = (gasFlowQ * inletH2S * 1.2f * 34.08f) / 1e6f / 3600f * 86400f * (cachedEfficiency / 100f);
        float dailyRegenerationCost = dailyCapturedH2SKg * 1.80f;

        cachedDailyCost = dailyEnergyCost + dailyRegenerationCost + 450f;
        float simulatedServiceLife = 145f - (dailyCapturedH2SKg * 0.1f);

        UpdateUserInterfaceDisplay(cachedEfficiency, cachedDailyCost, cachedPressureDrop, cachedOutletPpm, simulatedServiceLife);
        UpdateUnifiedMeshAppearance();

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