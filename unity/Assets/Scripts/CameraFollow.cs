using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Setup")]
    public Transform targetCar;

    [Header("Camera Positioning")]
    public Vector3 offset = new Vector3(0f, 4f, -8f);
    public float positionSmoothTime = 0.05f; 
    public float rotationSmoothSpeed = 15f;

    private Vector3 _velocity = Vector3.zero;

    void Start()
    {
        // 1. Force normal 1x speed
        Time.timeScale = 1f;

        // 2. Lock physics to 60 FPS
        Time.fixedDeltaTime = 1f / 60f; 

        // 3. Lock rendering to 60 FPS
        Application.targetFrameRate = 60; 
    }

    void LateUpdate()
    {
        if (targetCar == null) return;

        Vector3 desiredPosition = targetCar.position + targetCar.TransformDirection(offset);

        transform.position = Vector3.SmoothDamp(
            transform.position, 
            desiredPosition, 
            ref _velocity, 
            positionSmoothTime
        );

        Vector3 lookTarget = targetCar.position + (targetCar.forward * 5f);
        Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
    }
}