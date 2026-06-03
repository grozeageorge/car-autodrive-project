using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CarAgent : Agent
{
    private CarController _carController;
    private Rigidbody _rb;
    private List<Transform> _checkpoints = new List<Transform>();
    private HashSet<Transform> _passedCheckpoints = new HashSet<Transform>();
    private float _closestDistanceToTarget;
    private float _lastSteerInput = 0f;

    private Vector3 _lastNodePosition;
    private float _totalCTEThisEpisode = 0f;
    private int _stepCountThisEpisode = 0;

    public float actionSmoothing = 15f;
    private float _smoothedSteerInput = 0f;

    public float wallPenalty = -15.0f;

    public override void Initialize()
    {
        _carController = GetComponent<CarController>();
        _rb = GetComponent<Rigidbody>();
    }

    public override void OnEpisodeBegin()
    {
        _carController.SpawnOnTrack();
        
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _checkpoints.Clear();
        _passedCheckpoints.Clear();
        
        Transform environmentParent = transform.parent;
        foreach (Transform child in environmentParent.GetComponentsInChildren<Transform>(true))
        {
            if (child.CompareTag("Checkpoint") || child.CompareTag("FinishLine"))
            {
                _checkpoints.Add(child);
            }
        }

        _lastSteerInput = 0f;
        _smoothedSteerInput = 0f;
        _totalCTEThisEpisode = 0f;
        _stepCountThisEpisode = 0;
        
        _lastNodePosition = transform.position; 
        _closestDistanceToTarget = 0f; 
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        _checkpoints.RemoveAll(t => t == null);
        _passedCheckpoints.RemoveWhere(t => t == null);

        Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
        sensor.AddObservation(localVelocity.z / 30f);

        float normalizedSteering = _carController.CurrentSteeringAngle / _carController.maxSteeringAngle;
        sensor.AddObservation(normalizedSteering);

        Transform targetCheckpoint = GetNextCheckpoint();
        if (targetCheckpoint != null)
        {
            Vector3 directionToTarget = (targetCheckpoint.position - transform.position).normalized;
            Vector3 localDirection = transform.InverseTransformDirection(directionToTarget);

            sensor.AddObservation(localDirection.x);
            sensor.AddObservation(localDirection.z);

            float distance = Vector3.Distance(transform.position, targetCheckpoint.position);
            sensor.AddObservation(Mathf.Clamp01(distance / 200f));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (IsTireOnSidewalk())
        {
            AddReward(wallPenalty);
            RecordEpisodeStats(false);
            EndEpisode();
            return;
        }

        AddReward(-0.001f);

        float motorInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float rawSteerInput = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        float cubicSteerInput = rawSteerInput * rawSteerInput * rawSteerInput;
        _smoothedSteerInput = Mathf.Lerp(_smoothedSteerInput, cubicSteerInput, Time.fixedDeltaTime * actionSmoothing);

        _carController.Drive(motorInput, _smoothedSteerInput, false);

        Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
        float currentSpeedKmH = localVelocity.z * 3.6f;

        float steerDelta = Mathf.Abs(_smoothedSteerInput - _lastSteerInput);
        AddReward(-0.005f * steerDelta);
        _lastSteerInput = _smoothedSteerInput;

        if (currentSpeedKmH >= 2f && currentSpeedKmH <= 60f)
        {
            AddReward(currentSpeedKmH * 0.001f);
        }

        Transform targetCheckpoint = GetNextCheckpoint();
        if (targetCheckpoint != null)
        {
            Vector3 lineDirection = (targetCheckpoint.position - _lastNodePosition).normalized;
            Vector3 vectorToCar = transform.position - _lastNodePosition;
            float currentCTE = Vector3.Cross(vectorToCar, lineDirection).magnitude;
            
            _totalCTEThisEpisode += currentCTE;
            _stepCountThisEpisode++;

            float currentDistance = Vector3.Distance(transform.position, targetCheckpoint.position);
            if (_closestDistanceToTarget == 0f || currentDistance < _closestDistanceToTarget)
            {
                float deltaDistance = (_closestDistanceToTarget == 0f) ? 0f : _closestDistanceToTarget - currentDistance;
                AddReward(deltaDistance * 0.2f);
                _closestDistanceToTarget = currentDistance;
            }
        }
    }

    private Transform GetNextCheckpoint()
    {
        Transform closest = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < _checkpoints.Count; i++)
        {
            Transform cp = _checkpoints[i];
            if (cp != null && !_passedCheckpoints.Contains(cp) && !cp.CompareTag("FinishLine"))
            {
                float dist = Vector3.Distance(transform.position, cp.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = cp;
                }
            }
        }

        if (closest == null)
        {
            for (int i = 0; i < _checkpoints.Count; i++)
            {
                Transform cp = _checkpoints[i];
                if (cp != null && cp.CompareTag("FinishLine"))
                {
                    return cp;
                }
            }
        }

        return closest;
    }

    private void RecordEpisodeStats(bool finishedTrack)
    {
        int totalCheckpoints = 0;
        for (int i = 0; i < _checkpoints.Count; i++)
        {
            if (_checkpoints[i] != null && _checkpoints[i].CompareTag("Checkpoint")) 
                totalCheckpoints++;
        }

        if (totalCheckpoints == 0) return;

        float completionPercentage = finishedTrack ? 100f : ((float)_passedCheckpoints.Count / totalCheckpoints) * 100f;
        Academy.Instance.StatsRecorder.Add("Track_Data/Completion_Percent", completionPercentage, StatAggregationMethod.Average);

        float successRate = finishedTrack ? 1.0f : 0.0f;
        Academy.Instance.StatsRecorder.Add("Track_Data/Success_Rate", successRate, StatAggregationMethod.Average);

        float avgCTE = _stepCountThisEpisode > 0 ? _totalCTEThisEpisode / _stepCountThisEpisode : 0f;
        Academy.Instance.StatsRecorder.Add("Track_Data/Cross_Track_Error", avgCTE, StatAggregationMethod.Average);
    }

    private bool IsTireOnSidewalk()
    {
        if (_carController == null) return false;

        WheelCollider[] wheels = {
            _carController.frontLeftCollider,
            _carController.frontRightCollider,
            _carController.rearLeftCollider,
            _carController.rearRightCollider
        };

        foreach (var wheel in wheels)
        {
            if (wheel != null && wheel.GetGroundHit(out WheelHit hit))
            {
                if (hit.collider != null && hit.collider.CompareTag("Sidewalk"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = 0f;
        continuousActions[1] = 0f;

        Keyboard currentKeyboard = Keyboard.current;
        if (currentKeyboard != null)
        {
            if (currentKeyboard.wKey.isPressed || currentKeyboard.upArrowKey.isPressed) continuousActions[0] = 1f;
            else if (currentKeyboard.sKey.isPressed || currentKeyboard.downArrowKey.isPressed) continuousActions[0] = -1f;

            if (currentKeyboard.dKey.isPressed || currentKeyboard.rightArrowKey.isPressed) continuousActions[1] = 1f;
            else if (currentKeyboard.aKey.isPressed || currentKeyboard.leftArrowKey.isPressed) continuousActions[1] = -1f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Sidewalk"))
        {
            AddReward(wallPenalty);
            RecordEpisodeStats(false);
            EndEpisode();
            return;
        }

        if (other.CompareTag("FinishLine"))
        {
            AddReward(50.0f);
            RecordEpisodeStats(true);
            EndEpisode();
            return;
        }

        if (other.CompareTag("Checkpoint"))
        {
            if (!_passedCheckpoints.Contains(other.transform))
            {
                _passedCheckpoints.Add(other.transform);
                AddReward(5.0f);
                
                _lastNodePosition = other.transform.position;
                _closestDistanceToTarget = 0f; 
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Sidewalk"))
        {
            AddReward(wallPenalty);
            RecordEpisodeStats(false);
            EndEpisode();
        }
    }
}