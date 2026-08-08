using UnityEngine;
using UnityEngine.EventSystems;

public class SpinModel : MonoBehaviour, IDragHandler
{
    [Header("Assign the 3D Model Here")]
    public Transform modelToSpin;

    [Header("Spin Settings")]
    public float spinSpeed = 0.5f;

    [Header("Vertical Tilt Limits")]
    [Tooltip("How far you can tilt the model down")]
    public float minTilt = 0f;
    [Tooltip("How far you can tilt the model up")]
    public float maxTilt = 0f;

    // We store the current angles here to keep track of them
    private float currentSpinAngle = 0f;
    private float currentTiltAngle = 0f;

    private void Start()
    {
        // When the game starts, grab the model's current rotation so it doesn't snap.
        // I remember your model starts at Y: 180 degrees!
        if (modelToSpin != null)
        {
            Vector3 startRotation = modelToSpin.eulerAngles;
            currentSpinAngle = startRotation.y;
            currentTiltAngle = startRotation.x;

            // Unity wraps angles around 360. This normalizes it so clamping works properly.
            if (currentTiltAngle > 180f) currentTiltAngle -= 360f;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (modelToSpin != null)
        {
            // Horizontal mouse movement (Mouse X) spins the model infinitely around its Y-axis
            currentSpinAngle -= eventData.delta.x * spinSpeed;

            // Vertical mouse movement (Mouse Y) tilts the model up/down around its X-axis
            currentTiltAngle += eventData.delta.y * spinSpeed;

            // Clamp the vertical tilt so it cannot go past your set limits
            currentTiltAngle = Mathf.Clamp(currentTiltAngle, minTilt, maxTilt);

            // Apply the new clamped rotation back to the model
            modelToSpin.rotation = Quaternion.Euler(currentTiltAngle, currentSpinAngle, 0f);
        }
        else
        {
            Debug.LogWarning("SpinModel script is missing the target model! Please assign it in the Inspector.");
        }
    }
}