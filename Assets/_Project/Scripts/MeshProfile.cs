using UnityEngine;

[CreateAssetMenu(fileName = "NewMeshProfile", menuName = "EcoMesh/Mesh Profile", order = 1)]
public class MeshProfile : ScriptableObject
{
    [Header("Visual Properties")]
    [Tooltip("The human-readable name of this material for dropdown menus.")]
    public string materialDisplayName;

    [Header("Chemical Properties")]
    [Tooltip("Kinetic adsorption velocity coefficient (k_mesh). Higher means faster H2S trapping.")]
    public float kineticCoefficient;

    [Tooltip("Maximum saturation adsorption capacity (q_max) in kg of H2S per kg of bed material.")]
    public float maximumSorptionCapacity;

    [Tooltip("Material internal porosity (epsilon). Ratio of void space to total volume (0.0 to 1.0).")]
    [Range(0f, 1f)]
    public float porosity;

    [Tooltip("Bulk density of the packed material (rho_bulk) in kg/m³.")]
    public float bulkDensity;

    [Header("Financial Properties")]
    [Tooltip("Cost per kilogram of raw material in Euros (€/kg).")]
    public float unitCostPerKg;

    [Tooltip("Annual maintenance multiplier coefficient based on material corrosion susceptibility.")]
    public float maintenanceFactor;
}