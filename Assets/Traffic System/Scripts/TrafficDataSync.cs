using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class TrafficDataSync : MonoBehaviour
{
    public static TrafficDataSync Instance { get; private set; }

    [Header("Settings")]
    public bool debugLog = true;

    // Cache of traffic lights by ID for quick lookup
    private Dictionary<string, TrafficSystemTrafficLight> trafficLightsCache = new Dictionary<string, TrafficSystemTrafficLight>();
    
    // Cache of vehicles by ID
    private Dictionary<string, TrafficSystemVehicle> vehiclesCache = new Dictionary<string, TrafficSystemVehicle>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // Build cache of all traffic lights in scene
        BuildTrafficLightsCache();
    }

    void BuildTrafficLightsCache()
    {
        trafficLightsCache.Clear();
        
        TrafficSystemTrafficLight[] allLights = FindObjectsOfType<TrafficSystemTrafficLight>();
        
        foreach (var light in allLights)
        {
            // Use the light's name or unique ID as the key
            string lightId = light.name;
            trafficLightsCache[lightId] = light;
        }

        if (debugLog)
            Debug.Log($"TrafficDataSync: Cached {trafficLightsCache.Count} traffic lights");
    }

    public void UpdateTrafficLights(string jsonData)
    {
        try
        {
            TrafficLightsResponse response = JsonUtility.FromJson<TrafficLightsResponse>(jsonData);
            
            if (response == null || response.lights == null)
            {
                Debug.LogWarning("TrafficDataSync: Received null or empty lights data");
                return;
            }

            foreach (var lightData in response.lights)
            {
                UpdateSingleTrafficLight(lightData);
            }

            if (debugLog)
                Debug.Log($"TrafficDataSync: Updated {response.lights.Length} traffic lights");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"TrafficDataSync: Failed to parse traffic lights JSON: {e.Message}");
        }
    }

    void UpdateSingleTrafficLight(TrafficLightData lightData)
    {
        if (trafficLightsCache.TryGetValue(lightData.id, out TrafficSystemTrafficLight light))
        {
            // Convert string status to enum
            TrafficSystemTrafficLight.Status status = TrafficSystemTrafficLight.Status.RED;
            
            switch (lightData.status.ToLower())
            {
                case "green":
                    status = TrafficSystemTrafficLight.Status.GREEN;
                    break;
                case "yellow":
                    status = TrafficSystemTrafficLight.Status.YELLOW;
                    break;
                case "red":
                    status = TrafficSystemTrafficLight.Status.RED;
                    break;
            }

            // Update the traffic light status
            light.SetStatus(status, false);

            // Update control mode if available
            if (!string.IsNullOrEmpty(lightData.mode))
            {
                TrafficSystemTrafficLight.ControlMode mode = TrafficSystemTrafficLight.ControlMode.AUTOMATIC;
                
                switch (lightData.mode.ToLower())
                {
                    case "manual":
                        mode = TrafficSystemTrafficLight.ControlMode.MANUAL;
                        break;
                    case "automatic":
                        mode = TrafficSystemTrafficLight.ControlMode.AUTOMATIC;
                        break;
                    case "api_controlled":
                        mode = TrafficSystemTrafficLight.ControlMode.API_CONTROLLED;
                        break;
                }
                
                light.SetControlMode(mode);
            }
        }
        else
        {
            if (debugLog)
                Debug.LogWarning($"TrafficDataSync: Traffic light '{lightData.id}' not found in scene");
        }
    }

    public void UpdateVehicles(string jsonData)
    {
        try
        {
            VehiclesResponse response = JsonUtility.FromJson<VehiclesResponse>(jsonData);
            
            if (response == null || response.vehicles == null)
            {
                return; // Vehicles endpoint might not be implemented yet
            }

            foreach (var vehicleData in response.vehicles)
            {
                UpdateSingleVehicle(vehicleData);
            }

            if (debugLog)
                Debug.Log($"TrafficDataSync: Updated {response.vehicles.Length} vehicles");
        }
        catch (System.Exception e)
        {
            // Don't spam errors if vehicles endpoint doesn't exist yet
            // Debug.LogWarning($"TrafficDataSync: Failed to parse vehicles JSON: {e.Message}");
        }
    }

    void UpdateSingleVehicle(VehicleData vehicleData)
    {
        TrafficSystemVehicle vehicle;

        // Check if we already have this vehicle cached
        if (!vehiclesCache.TryGetValue(vehicleData.id, out vehicle))
        {
            // Try to find it in the scene
            TrafficSystemVehicle[] allVehicles = FindObjectsOfType<TrafficSystemVehicle>();
            vehicle = allVehicles.FirstOrDefault(v => v.name == vehicleData.id);

            if (vehicle != null)
            {
                vehiclesCache[vehicleData.id] = vehicle;
            }
            else
            {
                // Vehicle doesn't exist yet - could spawn it here if needed
                if (debugLog)
                    Debug.LogWarning($"TrafficDataSync: Vehicle '{vehicleData.id}' not found in scene");
                return;
            }
        }

        // Update vehicle position and rotation
        vehicle.transform.position = new Vector3(vehicleData.position.x, vehicleData.position.y, vehicleData.position.z);
        vehicle.transform.rotation = Quaternion.Euler(vehicleData.rotation.x, vehicleData.rotation.y, vehicleData.rotation.z);
        
        // Update velocity if the vehicle has this property
        if (vehicleData.speed > 0)
        {
            vehicle.m_velocity = vehicleData.speed;
        }
    }

    // Call this if you want to force a cache rebuild (e.g., after spawning new objects)
    public void RefreshCaches()
    {
        BuildTrafficLightsCache();
        vehiclesCache.Clear();
    }
}

// Data classes for JSON deserialization
[System.Serializable]
public class TrafficLightsResponse
{
    public TrafficLightData[] lights;
}

[System.Serializable]
public class TrafficLightData
{
    public string id;
    public string status;  // "green", "yellow", "red"
    public string mode;    // "manual", "automatic", "api_controlled"
    public float timer;
}

[System.Serializable]
public class VehiclesResponse
{
    public VehicleData[] vehicles;
}

[System.Serializable]
public class VehicleData
{
    public string id;
    public Vector3Data position;
    public Vector3Data rotation;
    public float speed;
}

[System.Serializable]
public class Vector3Data
{
    public float x;
    public float y;
    public float z;
}