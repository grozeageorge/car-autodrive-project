using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;

public class CarAgent : Agent
{
    private Rigidbody _rigidbody;
    public float moveSpeed = 10f;
    public float turnSpeed = 100f;
    public Transform spawnPoint;
    
    // Stores all gates to reactivate them upon reset
    public GameObject[] checkpoints;
    public bool[] passedCheckpoints;
    
    // Runs once on agent wake up
    public override void Initialize()
    {
        _rigidbody = GetComponent<Rigidbody>();
        passedCheckpoints = new bool[checkpoints.Length];
    }
    
    // Runs every time the car crashes or finishes the track
    public override void OnEpisodeBegin()
    {
        // Kill the momentum
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        
        // Reset position and rotation
        transform.localPosition = spawnPoint.localPosition;
        transform.localRotation = spawnPoint.localRotation;
        
        // Reset the boolean passedCheckpoints
        for (var i = 0; i < passedCheckpoints.Length; i++)
        {
            passedCheckpoints[i] = false;
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Index 0: Steering (X-axis rotation)
        // Index 1: Acceleration (Z-axis force)
        var steering = actions.ContinuousActions[0];
        var acceleration = actions.ContinuousActions[1];
        
        _rigidbody.AddForce(transform.forward * acceleration * moveSpeed);
        transform.Rotate(transform.up * steering * turnSpeed * Time.fixedDeltaTime);
        
        // Encourage continuous forward movement
        if (acceleration > 0) AddReward(0.1f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Punish the agent for unsafe driving
        if (!collision.gameObject.CompareTag("Wall")) return;
        AddReward(-1f);
        EndEpisode();
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("FinishLine"))
        {
            AddReward(1f); // Massive reward for completing the track
            EndEpisode();
        }
        else if (other.gameObject.CompareTag("Checkpoint"))
        {
            var index = Array.IndexOf(checkpoints, other.gameObject);
            if (passedCheckpoints[index]) return;
            passedCheckpoints[index] = true; // Prevent reward farming
            AddReward(0.5f); // Reward for track progression
        }
    }
}