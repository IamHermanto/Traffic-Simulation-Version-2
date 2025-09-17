using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[System.Serializable]
public class TrafficLightInfo
{
    public string id;
    public string name;
    public TrafficSystemTrafficLight.Status status;
    public TrafficSystemTrafficLight.ControlMode controlMode;
    public Vector3 position;
    public string intersection;
    public float greenDuration;
    public float timeSinceGreen;
    public bool turnLeftAnytime;
    public bool manualOverride;
}

[System.Serializable]
public class TrafficSystemData
{
    public List<TrafficLightInfo> lights = new List<TrafficLightInfo>();
    public int totalLights;
    public string timestamp;
    public bool systemActive;
}

public class TrafficSystemLightManager : MonoBehaviour
{
    public static TrafficSystemLightManager Instance { get; private set; }
    
    [Header("API Communication Settings")]
    public bool enableAPIControl = true;
    public float statusUpdateInterval = 0.5f; // Update status every 500ms
    public float commandCheckInterval = 0.1f;  // Check commands every 100ms
    
    [Header("File Paths")]
    public string statusFileName = "traffic_system_status.json";
    public string commandsDirectory = "commands";
    
    // Internal state
    private List<TrafficSystemTrafficLight> allTrafficLights = new List<TrafficSystemTrafficLight>();
    private Dictionary<string, TrafficSystemTrafficLight> lightsByID = new Dictionary<string, TrafficSystemTrafficLight>();
    private string statusFilePath;
    private string commandsPath;
    private float lastStatusUpdate = 0f;
    private float lastCommandCheck = 0f;
    
    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Setup file paths
        SetupFilePaths();
        
        // Discover all traffic lights in scene
        DiscoverTrafficLights();
        
        if(TrafficSystem.enableDebugLogging)
        {
            Debug.Log($"TRAFFIC LIGHT MANAGER INITIALIZED: Found {allTrafficLights.Count} traffic lights");
        }
    }
    
    void Start()
    {
        // Initial status write
        if(enableAPIControl)
        {
            WriteSystemStatus();
        }
    }
    
    void Update()
    {
        if(!enableAPIControl) return;
        
        // Update status file periodically
        if(Time.time - lastStatusUpdate >= statusUpdateInterval)
        {
            WriteSystemStatus();
            lastStatusUpdate = Time.time;
        }
        
        // Check for API commands
        if(Time.time - lastCommandCheck >= commandCheckInterval)
        {
            CheckForAPICommands();
            lastCommandCheck = Time.time;
        }
    }
    
    void SetupFilePaths()
    {
        string baseDirectory;
        
        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        baseDirectory = "C:/temp/unity-traffic/";
        #else
        baseDirectory = "/tmp/unity-traffic/";
        #endif
        
        statusFilePath = System.IO.Path.Combine(baseDirectory, statusFileName);
        commandsPath = System.IO.Path.Combine(baseDirectory, commandsDirectory);
        
        // Create directories
        System.IO.Directory.CreateDirectory(baseDirectory);
        System.IO.Directory.CreateDirectory(commandsPath);
        
        if(TrafficSystem.enableDebugLogging)
        {
            Debug.Log($"MANAGER PATHS: Status={statusFilePath}, Commands={commandsPath}");
        }
    }
    
    void DiscoverTrafficLights()
    {
        allTrafficLights.Clear();
        lightsByID.Clear();
        
        // Find all traffic lights in the scene
        TrafficSystemTrafficLight[] foundLights = FindObjectsOfType<TrafficSystemTrafficLight>();
        
        for(int i = 0; i < foundLights.Length; i++)
        {
            TrafficSystemTrafficLight light = foundLights[i];
            string lightID = GenerateUniqueLightID(light, i);
            
            allTrafficLights.Add(light);
            lightsByID[lightID] = light;
            
            // Set the light's ID for API reference
            light.name = lightID; // This ensures the light knows its API ID
            
            if(TrafficSystem.enableDebugLogging)
            {
                Debug.Log($"DISCOVERED LIGHT: {lightID} at position {light.transform.position}");
            }
        }
    }
    
    string GenerateUniqueLightID(TrafficSystemTrafficLight light, int index)
    {
        // Try to use intersection name if available
        if(light.m_intersection != null && !string.IsNullOrEmpty(light.m_intersection.name))
        {
            return $"light_{light.m_intersection.name}_{index}";
        }
        
        // Fallback to position-based ID
        Vector3 pos = light.transform.position;
        return $"light_{pos.x:F0}_{pos.z:F0}_{index}";
    }
    
    void WriteSystemStatus()
    {
        try
        {
            TrafficSystemData systemData = new TrafficSystemData();
            systemData.lights = new List<TrafficLightInfo>();
            systemData.totalLights = allTrafficLights.Count;
            systemData.timestamp = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff");
            systemData.systemActive = true;
            
            // Gather data from all traffic lights
            foreach(var kvp in lightsByID)
            {
                string lightID = kvp.Key;
                TrafficSystemTrafficLight light = kvp.Value;
                
                if(light != null)
                {
                    TrafficLightInfo info = new TrafficLightInfo();
                    info.id = lightID;
                    info.name = light.gameObject.name;
                    info.status = light.m_status;
                    info.controlMode = light.GetControlMode();
                    info.position = light.transform.position;
                    info.intersection = light.m_intersection ? light.m_intersection.name : "none";
                    info.greenDuration = light.m_greenDuration;
                    info.timeSinceGreen = light.m_timeSinceGreen;
                    info.turnLeftAnytime = light.m_turnLeftAnytime;
                    info.manualOverride = light.m_manualOverride;
                    
                    systemData.lights.Add(info);
                }
            }
            
            // Write to JSON file
            string jsonData = JsonUtility.ToJson(systemData, true);
            System.IO.File.WriteAllText(statusFilePath, jsonData);
            
            if(TrafficSystem.enableDebugLogging && Time.time % 5f < Time.deltaTime)
            {
                Debug.Log($"MANAGER STATUS UPDATE: {systemData.totalLights} lights updated");
            }
        }
        catch(System.Exception e)
        {
            Debug.LogError($"Failed to write system status: {e.Message}");
        }
    }
    
    void CheckForAPICommands()
    {
        try
        {
            if(!System.IO.Directory.Exists(commandsPath)) return;
            
            string[] commandFiles = System.IO.Directory.GetFiles(commandsPath, "*_command.json");
            
            foreach(string commandFile in commandFiles)
            {
                ProcessCommandFile(commandFile);
            }
        }
        catch(System.Exception e)
        {
            Debug.LogError($"Failed to check API commands: {e.Message}");
        }
    }
    
    void ProcessCommandFile(string commandFile)
    {
        try
        {
            string commandJson = System.IO.File.ReadAllText(commandFile);
            if(string.IsNullOrEmpty(commandJson)) return;
            
            TrafficLightCommand command = JsonUtility.FromJson<TrafficLightCommand>(commandJson);
            if(command == null) return;
            
            // Extract light ID from filename
            string fileName = System.IO.Path.GetFileNameWithoutExtension(commandFile);
            string lightID = fileName.Replace("_command", "");
            
            // Check if we've already processed this command
            if(HasProcessedCommand(lightID, command.timestamp)) return;
            
            // Find the target light
            if(lightsByID.ContainsKey(lightID))
            {
                TrafficSystemTrafficLight light = lightsByID[lightID];
                ProcessLightCommand(light, lightID, command);
                
                // Mark command as processed
                MarkCommandProcessed(lightID, command.timestamp);
                
                if(TrafficSystem.enableDebugLogging)
                {
                    Debug.Log($"MANAGER COMMAND PROCESSED: {lightID} - {command.action} - {command.status}");
                }
            }
            else
            {
                Debug.LogWarning($"MANAGER: Unknown light ID in command: {lightID}");
            }
        }
        catch(System.Exception e)
        {
            Debug.LogError($"Failed to process command file {commandFile}: {e.Message}");
        }
    }
    
    void ProcessLightCommand(TrafficSystemTrafficLight light, string lightID, TrafficLightCommand command)
    {
        switch(command.action.ToLower())
        {
            case "set_status":
                TrafficSystemTrafficLight.Status newStatus;
                if(System.Enum.TryParse(command.status, true, out newStatus))
                {
                    light.SetStatus(newStatus, false);
                }
                break;
                
            case "set_mode":
                TrafficSystemTrafficLight.ControlMode newMode;
                if(System.Enum.TryParse(command.mode, true, out newMode))
                {
                    light.SetControlMode(newMode);
                }
                break;
                
            case "set_duration":
                if(command.duration > 0)
                {
                    light.m_greenDuration = command.duration;
                }
                break;
                
            case "set_manual_status":
                TrafficSystemTrafficLight.Status manualStatus;
                if(System.Enum.TryParse(command.status, true, out manualStatus))
                {
                    light.SetManualStatus(manualStatus);
                }
                break;
        }
    }
    
    bool HasProcessedCommand(string lightID, long timestamp)
    {
        string timestampFile = System.IO.Path.Combine(commandsPath, $"{lightID}_last_timestamp.txt");
        if(System.IO.File.Exists(timestampFile))
        {
            string lastTimestampStr = System.IO.File.ReadAllText(timestampFile);
            if(long.TryParse(lastTimestampStr, out long lastTimestamp))
            {
                return timestamp <= lastTimestamp;
            }
        }
        return false;
    }
    
    void MarkCommandProcessed(string lightID, long timestamp)
    {
        string timestampFile = System.IO.Path.Combine(commandsPath, $"{lightID}_last_timestamp.txt");
        System.IO.File.WriteAllText(timestampFile, timestamp.ToString());
    }
    
    // Public API for other scripts
    public List<TrafficSystemTrafficLight> GetAllTrafficLights()
    {
        return new List<TrafficSystemTrafficLight>(allTrafficLights);
    }
    
    public TrafficSystemTrafficLight GetLightByID(string lightID)
    {
        return lightsByID.ContainsKey(lightID) ? lightsByID[lightID] : null;
    }
    
    public string[] GetAllLightIDs()
    {
        string[] ids = new string[lightsByID.Count];
        lightsByID.Keys.CopyTo(ids, 0);
        return ids;
    }
    
    public void RefreshTrafficLights()
    {
        DiscoverTrafficLights();
        WriteSystemStatus();
    }
    
    // Emergency functions for cybersecurity demos
    public void SetAllLightsToRed()
    {
        foreach(var light in allTrafficLights)
        {
            if(light != null)
            {
                light.SetControlMode(TrafficSystemTrafficLight.ControlMode.API_CONTROLLED);
                light.SetStatus(TrafficSystemTrafficLight.Status.RED, false);
            }
        }
    }
    
    public void RestoreAllLightsToAutomatic()
    {
        foreach(var light in allTrafficLights)
        {
            if(light != null)
            {
                light.SetControlMode(TrafficSystemTrafficLight.ControlMode.AUTOMATIC);
            }
        }
    }
    
    public void ChaosMode(bool enabled)
    {
        if(enabled)
        {
            StartCoroutine(ChaosRoutine());
        }
    }
    
    IEnumerator ChaosRoutine()
    {
        TrafficSystemTrafficLight.Status[] statuses = {
            TrafficSystemTrafficLight.Status.RED,
            TrafficSystemTrafficLight.Status.YELLOW,
            TrafficSystemTrafficLight.Status.GREEN
        };
        
        // Run chaos for 30 seconds
        float endTime = Time.time + 30f;
        
        while(Time.time < endTime)
        {
            foreach(var light in allTrafficLights)
            {
                if(light != null)
                {
                    light.SetControlMode(TrafficSystemTrafficLight.ControlMode.API_CONTROLLED);
                    TrafficSystemTrafficLight.Status randomStatus = statuses[Random.Range(0, statuses.Length)];
                    light.SetStatus(randomStatus, false);
                }
            }
            
            yield return new WaitForSeconds(Random.Range(0.5f, 2f));
        }
        
        // Restore to automatic after chaos
        RestoreAllLightsToAutomatic();
    }
}
