using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    public bool isManualMode = false;
    public ProceduralTrackGenerator trackGenerator;
    public Transform centerOfMass;
    
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    public float motorForce = 2500f; 
    public float brakeForce = 5000f;
    public float antiRollForce = 5000f; 
    public float downforceMultiplier = 50f; 

    public float maxSteeringAngle = 30f; 
    public float steeringSpeed = 60f;    
    public float pedalSpeed = 5f;
    
    private float _currentMotorInput = 0f;
    private Rigidbody _rb;
    private float _currentSteeringAngle = 0f;
    
    public float CurrentSteeringAngle => _currentSteeringAngle;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (centerOfMass != null)  
        {
            _rb.centerOfMass = centerOfMass.localPosition;
        }
    }

    public void SpawnOnTrack()
    {
        if (trackGenerator != null)
        {
            trackGenerator.RebuildTrack();
            
            transform.position = trackGenerator.startPosition;
            transform.rotation = trackGenerator.startRotation;
            
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            frontLeftCollider.motorTorque = 0f;
            frontRightCollider.motorTorque = 0f;
            rearLeftCollider.motorTorque = 0f;
            rearRightCollider.motorTorque = 0f;

            frontLeftCollider.brakeTorque = 0f;
            frontRightCollider.brakeTorque = 0f;
            rearLeftCollider.brakeTorque = 0f;
            rearRightCollider.brakeTorque = 0f;

            frontLeftCollider.steerAngle = 0f;
            frontRightCollider.steerAngle = 0f;

            _currentSteeringAngle = 0f; 

            Physics.SyncTransforms(); 
        }
    }

    void FixedUpdate()
    {
        ApplyAntiRollBars(frontLeftCollider, frontRightCollider);
        ApplyAntiRollBars(rearLeftCollider, rearRightCollider);
        ApplyDownforce();

        if (Mathf.Abs(_currentSteeringAngle) < 2f) 
        {
            Vector3 localAngularVel = transform.InverseTransformDirection(_rb.angularVelocity);
            localAngularVel.y *= 0.9f; 
            _rb.angularVelocity = transform.TransformDirection(localAngularVel);
        }
    }

    public void Drive(float motorInput, float steerInput, bool isBraking)
    {
        float targetSteeringAngle = maxSteeringAngle * steerInput;
        
        _currentMotorInput = Mathf.MoveTowards(_currentMotorInput, motorInput, pedalSpeed * Time.deltaTime);
        _currentSteeringAngle = Mathf.MoveTowards(_currentSteeringAngle, targetSteeringAngle, steeringSpeed * Time.deltaTime);

        float currentMotorForce = motorForce * motorInput;
        float currentBrakeForce = isBraking ? brakeForce : 0f;

        frontLeftCollider.steerAngle = _currentSteeringAngle;
        frontRightCollider.steerAngle = _currentSteeringAngle;

        rearLeftCollider.motorTorque = currentMotorForce;
        rearRightCollider.motorTorque = currentMotorForce;

        frontLeftCollider.brakeTorque = currentBrakeForce;
        frontRightCollider.brakeTorque = currentBrakeForce;
        rearLeftCollider.brakeTorque = currentBrakeForce;
        rearRightCollider.brakeTorque = currentBrakeForce;

        UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
        UpdateSingleWheel(frontRightCollider, frontRightMesh);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
        UpdateSingleWheel(rearRightCollider, rearRightMesh);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);
        wheelTransform.rotation = rot;
        wheelTransform.position = pos;
    }

    private void ApplyAntiRollBars(WheelCollider leftWheel, WheelCollider rightWheel)
    {
        float travelL = 1.0f;
        float travelR = 1.0f;

        bool groundedL = leftWheel.GetGroundHit(out WheelHit hitL);
        if (groundedL) travelL = (-leftWheel.transform.InverseTransformPoint(hitL.point).y - leftWheel.radius) / leftWheel.suspensionDistance;

        bool groundedR = rightWheel.GetGroundHit(out WheelHit hitR);
        if (groundedR) travelR = (-rightWheel.transform.InverseTransformPoint(hitR.point).y - rightWheel.radius) / rightWheel.suspensionDistance;

        float antiRollForceAmount = (travelL - travelR) * antiRollForce;

        if (groundedL) _rb.AddForceAtPosition(leftWheel.transform.up * -antiRollForceAmount, leftWheel.transform.position);
        if (groundedR) _rb.AddForceAtPosition(rightWheel.transform.up * antiRollForceAmount, rightWheel.transform.position);
    }

    private void ApplyDownforce()
    {
        _rb.AddForce(-transform.up * (downforceMultiplier * _rb.linearVelocity.magnitude));
    }
}