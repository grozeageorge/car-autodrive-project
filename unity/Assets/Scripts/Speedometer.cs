using UnityEngine;
using TMPro; // Required for TextMeshPro UI

public class Speedometer : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("Drag the car you want to track here")]
    public Rigidbody targetCar;

    private TextMeshProUGUI _speedText;

    void Awake()
    {
        // Grab the Text component attached to this object
        _speedText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (targetCar == null || _speedText == null) return;

        // 1. Get the raw physics speed (meters per second)
        float speedMS = targetCar.linearVelocity.magnitude;

        // 2. Convert to km/h
        float speedKMH = speedMS * 3.6f;

        // 3. Update the UI text, rounding to a clean whole number
        _speedText.text = $"{Mathf.RoundToInt(speedKMH)} km/h";
    }
}