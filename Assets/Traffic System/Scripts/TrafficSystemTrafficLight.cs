using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TrafficSystemTrafficLight : MonoBehaviour 
{
	public enum Status
	{
		RED    = 0,
		YELLOW = 1,
		GREEN  = 2
	}

	// Control mode enum
	public enum ControlMode
	{
		AUTOMATIC = 0,
		MANUAL = 1,
		API_CONTROLLED = 2
	}

	public  TrafficSystemIntersection  m_intersection                 = null;
	public  Status                     m_status                       = Status.RED;
	public  float                      m_greenDuration                = 5.0f;
	public  float                      m_timeSinceGreen               = 0.0f;
	public  bool                       m_turnLeftAnytime              = false;

	// Control mode and manual overrides
	[Header("API Control Settings")]
	public  ControlMode               m_controlMode                  = ControlMode.AUTOMATIC;
	public  bool                      m_manualOverride               = false;
	public  Status                    m_manualStatus                 = Status.RED;
	
	// API command file path
	private string                    m_commandFilePath              = "";
	private float                     m_lastCommandCheck             = 0.0f;
	private float                     m_commandCheckInterval         = 0.1f; // Check every 100ms

	public  Transform                 m_lightRed                     = null;
	public  Transform                 m_lightYellow                  = null;
	public  Transform                 m_lightGreen                   = null;
	public  Transform                 m_lightRedArrow                = null;
	public  Transform                 m_lightYellowArrow             = null;
	public  Transform                 m_lightGreenArrow              = null;

	public  GameObject                m_meshObject                   = null;

	private List<TrafficSystemVehicle> m_vehiclesStopped             = new List<TrafficSystemVehicle>();

	void Awake()
	{
		// Setup command file path
		#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		m_commandFilePath = "C:/temp/unity-traffic/commands/" + gameObject.name + "_command.json";
		#else
		m_commandFilePath = "/tmp/unity-traffic/commands/" + gameObject.name + "_command.json";
		#endif

		// Create command directory
		string commandDir = System.IO.Path.GetDirectoryName(m_commandFilePath);
		if (!System.IO.Directory.Exists(commandDir))
		{
			System.IO.Directory.CreateDirectory(commandDir);
		}
	}

	void Start()
	{
		if(TrafficSystem.enableDebugLogging)
		{
			Debug.Log($"TRAFFIC LIGHT INITIALIZED: {gameObject.name} - Control Mode: {m_controlMode}");
		}
	}

	void FixedUpdate()
	{
		// *** FIX: ALWAYS check for API commands regardless of mode ***
		CheckForAPICommands();

		// Handle different control modes
		switch(m_controlMode)
		{
			case ControlMode.AUTOMATIC:
				// Original automatic behavior (handled by intersection)
				break;
				
			case ControlMode.MANUAL:
				// Manual override mode
				if(m_manualOverride)
				{
					SetStatus(m_manualStatus, false);
				}
				break;
				
			case ControlMode.API_CONTROLLED:
				// API controlled mode - commands already checked above
				break;
		}

		// Original culling logic
		if(TrafficSystem.Instance && m_meshObject)
		{
			if (TrafficSystem.Instance.IsInView (transform.position, false))
				m_meshObject.SetActive (true);
			else
				m_meshObject.SetActive (false);
		}
	}

	void Update()
	{
		if(m_status == Status.GREEN)
		{
			m_timeSinceGreen += Time.deltaTime;
		}
		else
		{
			m_timeSinceGreen = 0.0f;
		}
	}

	// FIXED: Check for API commands from Flask
	void CheckForAPICommands()
	{
		if(Time.time - m_lastCommandCheck < m_commandCheckInterval)
			return;
			
		m_lastCommandCheck = Time.time;
		
		try
		{
			if(System.IO.File.Exists(m_commandFilePath))
			{
				string commandJson = System.IO.File.ReadAllText(m_commandFilePath);
				
				if(!string.IsNullOrEmpty(commandJson))
				{
					// Parse the JSON command
					var command = JsonUtility.FromJson<TrafficLightCommand>(commandJson);
					
					if(command != null && command.timestamp > GetLastProcessedTimestamp())
					{
						ProcessAPICommand(command);
						SetLastProcessedTimestamp(command.timestamp);
						
						if(TrafficSystem.enableDebugLogging)
						{
							Debug.Log($"API COMMAND PROCESSED: {gameObject.name} - Action: {command.action}");
						}
					}
				}
			}
		}
		catch(System.Exception e)
		{
			if(TrafficSystem.enableDebugLogging)
			{
				Debug.LogWarning($"Failed to process API command for {gameObject.name}: {e.Message}");
			}
		}
	}

	// FIXED: Process API commands
	void ProcessAPICommand(TrafficLightCommand command)
	{
		if(TrafficSystem.enableDebugLogging)
		{
			Debug.Log($"PROCESSING COMMAND: {gameObject.name} - Action: {command.action}, Mode: {command.mode}, Status: {command.status}");
		}

		switch(command.action.ToLower())
		{
			case "set_status":
				// *** FIX: Switch to API_CONTROLLED mode when setting status ***
				if(m_controlMode == ControlMode.AUTOMATIC)
				{
					SetControlMode(ControlMode.API_CONTROLLED);
				}
				
				Status newStatus = Status.RED;
				if(System.Enum.TryParse(command.status, true, out newStatus))
				{
					SetStatus(newStatus, false);
				}
				break;
				
			case "set_mode":
				ControlMode newMode = ControlMode.AUTOMATIC;
				if(System.Enum.TryParse(command.mode, true, out newMode))
				{
					SetControlMode(newMode);
				}
				break;
				
			case "set_duration":
				if(command.duration > 0)
				{
					m_greenDuration = command.duration;
				}
				break;
		}
	}

	// Public API methods for external control
	public void SetControlMode(ControlMode mode)
	{
		m_controlMode = mode;
		
		if(TrafficSystem.enableDebugLogging)
		{
			Debug.Log($"CONTROL MODE CHANGED: {gameObject.name} - {m_controlMode} → {mode}");
		}
		
		WriteStatusToFile(); // Update status file
	}

	public void SetManualStatus(Status status)
	{
		m_manualStatus = status;
		m_manualOverride = true;
		
		if(m_controlMode == ControlMode.MANUAL)
		{
			SetStatus(status, false);
		}
		
		if(TrafficSystem.enableDebugLogging)
		{
			Debug.Log($"MANUAL STATUS SET: {gameObject.name} - Manual Status: {status}, Override: {m_manualOverride}");
		}
	}

	// Get current control state
	public ControlMode GetControlMode()
	{
		return m_controlMode;
	}

	// Helper methods for timestamp tracking
	private long GetLastProcessedTimestamp()
	{
		string timestampFile = m_commandFilePath.Replace("_command.json", "_timestamp.txt");
		if(System.IO.File.Exists(timestampFile))
		{
			string timestampStr = System.IO.File.ReadAllText(timestampFile);
			if(long.TryParse(timestampStr, out long timestamp))
			{
				return timestamp;
			}
		}
		return 0;
	}

	private void SetLastProcessedTimestamp(long timestamp)
	{
		string timestampFile = m_commandFilePath.Replace("_command.json", "_timestamp.txt");
		System.IO.File.WriteAllText(timestampFile, timestamp.ToString());
	}

	void WriteStatusToFile()
{
    try 
    {
        string directoryPath = "/tmp/unity-traffic/";
        string lightName = gameObject.name.Replace(" ", "_").Replace("(", "").Replace(")", "").Replace("-", "_");
        string fileName = $"light_{lightName}.json";
        string filePath = $"{directoryPath}{fileName}";
        string tempPath = $"{filePath}.tmp";
        
        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        directoryPath = "C:/temp/unity-traffic/";
        filePath = $"{directoryPath}{fileName}";
        tempPath = $"{filePath}.tmp";
        #endif
        
        // Create directory if it doesn't exist
        if (!System.IO.Directory.Exists(directoryPath))
        {
            System.IO.Directory.CreateDirectory(directoryPath);
        }
        
        // Create JSON content
        string jsonContent = string.Format(@"{{
    ""id"": ""{0}"",
    ""status"": ""{1}"",
    ""timestamp"": ""{2}"",
    ""greenDuration"": {3},
    ""timeSinceGreen"": {4},
    ""position"": {{
        ""x"": {5},
        ""y"": {6},
        ""z"": {7}
    }},
    ""intersection"": ""{8}"",
    ""turnLeftAnytime"": {9}
}}",
            gameObject.name,
            m_status.ToString().ToLower(),
            System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
            m_greenDuration,
            m_timeSinceGreen,
            transform.position.x,
            transform.position.y,
            transform.position.z,
            m_intersection ? m_intersection.name : "none",
            m_turnLeftAnytime.ToString().ToLower()
        );
        
        // ATOMIC WRITE: Write to temp file first
        System.IO.File.WriteAllText(tempPath, jsonContent);
        
        // ATOMIC RENAME: Move temp file to final location
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
        System.IO.File.Move(tempPath, filePath);
        
        if(TrafficSystem.enableDebugLogging)
        {
            Debug.Log($"WROTE TRAFFIC LIGHT STATUS TO FILE: {filePath} - {m_status}");
        }
    }
    catch (System.Exception e)
    {
        Debug.LogWarning("Failed to write traffic light status: " + e.Message);
    }
}

	public void SetStatus( Status a_status, bool a_useLightArrows = false )
	{
		// Only allow status changes if in proper control mode
		if(m_controlMode == ControlMode.AUTOMATIC || 
		   (m_controlMode == ControlMode.MANUAL && m_manualOverride) ||
		   m_controlMode == ControlMode.API_CONTROLLED)
		{
			// DEBUG LOGGING FOR STATUS CHANGES
			if(TrafficSystem.enableDebugLogging)
			{
				Debug.Log($"TRAFFIC LIGHT CHANGE: {gameObject.name} - {m_status} → {a_status} | Mode: {m_controlMode} | Location: {transform.position}");
			}

			m_status = a_status;

			// Write status to file for Flask API integration
			WriteStatusToFile();

			if(m_lightRed && m_lightYellow && m_lightGreen)
			{
				switch(m_status)
				{
				case Status.GREEN:
				{
					// DEBUG LOGGING FOR GREEN ACTIVATION
					if(TrafficSystem.enableDebugLogging)
					{
						Debug.Log($"LIGHT ACTIVATED: {gameObject.name} - GREEN LIGHT ON | Duration: {m_greenDuration}s");
					}

					m_lightRed.gameObject   .SetActive(false);
					m_lightYellow.gameObject.SetActive(false);
					m_lightGreen.gameObject .SetActive(true);

					if(a_useLightArrows)
					{
						if(m_lightRedArrow)
							m_lightRedArrow.gameObject   .SetActive(false);
						if(m_lightYellowArrow)
							m_lightYellowArrow.gameObject.SetActive(false);
						if(m_lightGreenArrow)
							m_lightGreenArrow.gameObject .SetActive(true);
					}
					break;
				}
				case Status.YELLOW:
				{
					if(TrafficSystem.enableDebugLogging)
					{
						Debug.Log($"LIGHT ACTIVATED: {gameObject.name} - YELLOW LIGHT ON");
					}

					m_lightRed.gameObject   .SetActive(false);
					m_lightYellow.gameObject.SetActive(true);
					m_lightGreen.gameObject .SetActive(false);

					if(a_useLightArrows)
					{
						if(m_lightRedArrow)
							m_lightRedArrow.gameObject   .SetActive(false);
						if(m_lightYellowArrow)
							m_lightYellowArrow.gameObject.SetActive(true);
						if(m_lightGreenArrow)
							m_lightGreenArrow.gameObject .SetActive(false);
					}
					break;
				}
				case Status.RED:
				{
					if(TrafficSystem.enableDebugLogging)
					{
						Debug.Log($"LIGHT ACTIVATED: {gameObject.name} - RED LIGHT ON");
					}

					m_lightRed.gameObject   .SetActive(true);
					m_lightYellow.gameObject.SetActive(false);
					m_lightGreen.gameObject .SetActive(false);

					if(a_useLightArrows)
					{
						if(m_lightRedArrow)
							m_lightRedArrow.gameObject   .SetActive(true);
						if(m_lightYellowArrow)
							m_lightYellowArrow.gameObject.SetActive(false);
						if(m_lightGreenArrow)
							m_lightGreenArrow.gameObject .SetActive(false);
					}
					break;
				}
				}
			}
		}
	}

	// REQUIRED: Method used by TrafficSystemVehicle
	public bool IgnoreCanFitAcrossIntersectionCheck()
	{
		if(m_intersection && m_intersection.m_ignoreCanFitAcrossIntersectionCheck)
			return true;

		return false;
	}

	// REQUIRED: Trigger handling for vehicles
	void OnTriggerEnter( Collider a_obj )
	{
		TrafficSystemVehicle vehicle = null;

		if(a_obj.transform.GetComponent<TrafficSystemVehicle>())
			vehicle = a_obj.transform.GetComponent<TrafficSystemVehicle>();

		if(vehicle)
		{
			// DEBUG LOGGING FOR VEHICLE-LIGHT INTERACTION
			if(TrafficSystem.enableDebugLogging)
			{
				Debug.Log($"VEHICLE HIT LIGHT: {vehicle.name} hit {gameObject.name} - Light Status: {m_status} | Vehicle Position: {vehicle.transform.position}");
			}

			vehicle.AssignTrafficLight(this);

			// *** FIX: Only process intersection logic if intersection exists ***
			if(m_intersection != null)
			{
				if(vehicle.IsTurningIntoIncomingTraffic() && !m_turnLeftAnytime)
				{
					// DEBUG LOGGING FOR PRIORITY QUEUE
					if(TrafficSystem.enableDebugLogging)
					{
						Debug.Log($"PRIORITY VEHICLE: {vehicle.name} turning into incoming traffic - Added to priority queue");
					}
					m_intersection.AddToPriorityLightQueue( this );
				}
			}
		}

		// Handle player vehicles separately
		if(a_obj.transform.GetComponent<TrafficSystemVehiclePlayer>())
		{
			TrafficSystemVehiclePlayer playerVehicle = a_obj.transform.GetComponent<TrafficSystemVehiclePlayer>();

			if(playerVehicle)
			{
				// DEBUG LOGGING FOR PLAYER VEHICLE
				if(TrafficSystem.enableDebugLogging)
				{
					Debug.Log($"PLAYER VEHICLE HIT LIGHT: {playerVehicle.name} hit {gameObject.name} - Light Status: {m_status}");
				}
				
				// *** FIX: Only process intersection logic if intersection exists ***
				if(m_intersection != null)
				{
					if(playerVehicle.IsTurningIntoIncomingTraffic() && !m_turnLeftAnytime)
						m_intersection.AddToPriorityLightQueue( this );
				}

				playerVehicle.ProcessHasEnteredTrafficLightTrigger( this );
			}
		}
	}
}

// FIXED: Command structure for API communication
[System.Serializable]
public class TrafficLightCommand
{
    public string action;    // "set_status", "set_mode", "set_duration"
    public string status;    // "red", "yellow", "green"
    public string mode;      // "automatic", "manual", "api_controlled"
    public float duration;   // Green light duration
    public long timestamp;   // Unix timestamp for command ordering
}