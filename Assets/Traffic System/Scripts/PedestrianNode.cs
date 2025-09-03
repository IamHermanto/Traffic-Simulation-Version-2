using UnityEngine;
using System.Collections;

public class PedestrianNode : MonoBehaviour 
{
    [Header("Pedestrian Control")]
    public bool m_waitAtNode = false;  // Used by TrafficSystemIntersection to control pedestrian movement
    
    [Header("Node Settings")]
    public float m_nodeRadius = 1.0f;
    public Color m_gizmoColor = Color.blue;
    
    void Start()
    {
        // Initialize the node
    }
    
    void Update()
    {
        // Add any pedestrian logic here if needed
        // For now, this is just a placeholder that provides the required m_waitAtNode property
    }
    
    // Method to check if pedestrians should wait at this node
    public bool ShouldWait()
    {
        return m_waitAtNode;
    }
    
    // Method to allow pedestrians to proceed
    public void AllowMovement()
    {
        m_waitAtNode = false;
    }
    
    // Method to stop pedestrians at this node
    public void StopMovement()
    {
        m_waitAtNode = true;
    }
    
    // Draw gizmos in the scene view to visualize the pedestrian node
    void OnDrawGizmos()
    {
        Gizmos.color = m_waitAtNode ? Color.red : m_gizmoColor;
        Gizmos.DrawWireSphere(transform.position, m_nodeRadius);
        
        // Draw a small indicator showing the wait state
        if (m_waitAtNode)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.2f);
        }
    }
}