using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
[ExecuteInEditMode]
public class TrafficSystem : MonoBehaviour 
{
	public  static TrafficSystem Instance                    { get; set; }
	
	public static bool enableDebugLogging = false;

	// BEGIN - Values used in Editor
	public enum TrafficSystemTooltip
	{
		ANCHOR = 0,
		EDIT = 1
	}

	public enum RoadQuality
	{
		//ORIGINAL         = 0,
		VERSION_2        = 0,
		VERSION_2_MOBILE = 1,
		MAX              = 2
	}

	public enum DriveSide
	{
		RIGHT = 0,
		LEFT  = 1
	}

	public enum RoadType
	{
		LANES_0            = 0,
		LANES_1            = 1,
		LANES_2            = 2,
		LANES_MULTI        = 3,
		OFFRAMP            = 4,
		CHANGE_LANE        = 5
	}

	public  bool                m_showGizmos                 = true;
	public  bool                m_showNodes                  = true;
	public  Texture2D           TextureIconAnchor            = null;
	public  Texture2D           TextureIconEdit              = null;
	public  Texture2D           TextureIconAnchorRevealSmall = null;
	public  Texture2D           TextureIconEditRevealSmall   = null;
	public  Texture2D           TextureIconAnchorSmall       = null;
	public  Texture2D           TextureIconEditSmall         = null;
	public  Texture2D           TextureIconRoadTypeAll       = null;
	public  Texture2D           TextureIconRoadType0Lanes    = null;
	public  Texture2D           TextureIconRoadType1Lane     = null;
	public  Texture2D           TextureIconRoadType2Lanes    = null;
	public  Texture2D           TextureIconRoadType4Lanes    = null;
	public  Texture2D           TextureIconRoadType6Lanes    = null;

	private RoadQuality         m_roadQualityVal             = RoadQuality.VERSION_2_MOBILE;
	public  RoadQuality         GetRoadQualityVal()          { return m_roadQualityVal; }
	public  void                SetRoadQualityVal( RoadQuality a_quality )          { m_roadQualityVal = a_quality; }
	public  int                 RoadQualityElementsIndex     { get; set; }
	public  float               m_roadScale                  = 1.0f;

	public  TrafficSystemPiece  AnchorTrafficSystemPiece     { get; set; }
	public  TrafficSystemPiece  EditTrafficSystemPiece       { get; set; }
	public  GameObject          TooltipAnchor                { get; set; }
	public  GameObject          TooltipEdit                  { get; set; }
	public  bool                m_autoLinkOnSpawn            = true;
	public  bool                m_quickSpawn                 = false;
	public  bool                m_autoReverseAnchorToEdit    = false;
	private List<Transform>     CLRevealObjectsFrom          = new List<Transform>();
	private List<Transform>     CLRevealObjectsTo            = new List<Transform>();

	public  int                 m_vehicleSpawnCountMax       = -1;                                              // if -1 then unlimited vehicles can spawn. If higher than only this amount will ever spawn using the Traffic System spawn options
	public  bool                m_randomVehicleSpawnPerNode  = false; 
	[Range(0, 1)]
	public  float               m_randomVehicleSpawnChancePerNode = 0.0f;
	public  float               m_randVehicleVelocityMin     = 1.0f;
	public  float               m_randVehicleVelocityMax     = 5.0f;

	public  float               m_globalSpeedLimit           = 5.0f;                                           // used for when individual Road Piece have a speed limit of -1 (which is the default), they will use this global limit.
	[Range(0.0f, 5.0f)]
	public  float               m_globalSpeedVariation       = 1.5f;                                            // used to generate a slight variation of speed each node a vehicle gets to

	[Range(0.0f, 0.4f)]
	public  float               m_globalLanePosVariation     = 0.0f;                                            // used to generate a slight variation of lane position for all vehicles

	public  List<TrafficSystemVehicle>        m_vehiclePrefabs      = new List<TrafficSystemVehicle>();
	private List<TrafficSystemVehicleSpawner> m_vehicleSpawners     = new List<TrafficSystemVehicleSpawner>();
	private List<TrafficSystemVehicle>        m_spawnedVehicles     = new List<TrafficSystemVehicle>();
	public  List<TrafficSystemVehicle>        GetSpawnedVehicles()  { return m_spawnedVehicles; }

	[HideInInspector]
	public bool                              m_swapAnchorDimensions = false;
	[HideInInspector]
	public bool                              m_swapEditDimensions   = false;
	[HideInInspector]
	public bool                              m_swapOffsetDir        = false;
	[HideInInspector]
	public bool                              m_swapOffsetSize       = false;
	[HideInInspector]
	public bool                              m_negateOffsetSize     = false;

	public  TrafficSystemFollowCamera        m_followCameraScript   = null;

	public  Camera                           m_mainGameCamera       = null;                                   // assign this so all traffic and roads know when to cull themselves out of view.
	public  float                            m_distanceToCullRoad   = 200.0f;                                 // this is the distance from the camera to cull roads.
	public  float                            m_distanceToCullTraffic = 200.0f;                                // this is the distance from the camera to cull traffic.

	public  bool                m_spawnWithRoadQuality       = true;

	// NEW API CONTROL SETTINGS
	[Header("API Control Settings")]
	public  bool                enableAPIControl            = true;
	public  float               apiStatusUpdateInterval     = 0.5f;
	public  float               apiCommandCheckInterval     = 0.1f;

	// Private API variables
	private List<TrafficSystemTrafficLight> discoveredLights = new List<TrafficSystemTrafficLight>();
	private Dictionary<string, TrafficSystemTrafficLight> lightsByID = new Dictionary<string, TrafficSystemTrafficLight>();
	private string apiStatusFilePath;
	private string apiCommandsPath;
	private float lastAPIStatusUpdate = 0f;
	private float lastAPICommandCheck = 0f;

	void Awake()
	{
		if(Instance != null)
		{
			Destroy(this);
			return;
		}
		
		Instance = this;
		
		// Check command line arguments for debug flag
		string[] args = System.Environment.GetCommandLineArgs();
		for(int i = 0; i < args.Length; i++)
		{
			if(args[i] == "--debug" || args[i] == "-d")
			{
				enableDebugLogging = true;
				Debug.Log("=== DEBUG LOGGING ENABLED VIA COMMAND LINE ===");
				break;
			}
		}
		
		// Initialize API control if enabled
		if(enableAPIControl)
		{
			SetupAPIPaths();
			DiscoverAllTrafficLights();
		}
		
		Debug.Log("=== TRAFFIC SYSTEM INITIALIZED ===");
		Debug.Log($"Vehicle spawn max: {m_vehicleSpawnCountMax}");
		Debug.Log($"Global speed limit: {m_globalSpeedLimit}");
		if(enableAPIControl)
		{
			Debug.Log($"API Control: ENABLED - Found {discoveredLights.Count} traffic lights");
		}
	}

	void Start () 
	{
		if(Instance != this)
			return;

		if(m_randomVehicleSpawnPerNode && Application.isPlaying)
		{
			TrafficSystemPiece[] roadPieces = GameObject.FindObjectsOfType<TrafficSystemPiece>();

			for(int rIndex = 0; rIndex < roadPieces.Length; rIndex++)
			{
				float rand = Random.Range(0.0f, 1.0f);
				if(rand <= m_randomVehicleSpawnChancePerNode && CanSpawn())
				{
					int randVehicleIndex = Random.Range(0, m_vehiclePrefabs.Count);
					roadPieces[rIndex].SpawnRandomVehicle( m_vehiclePrefabs[randVehicleIndex] );
				}
			}
		}

		if(!m_followCameraScript)
			if(transform.GetComponent<TrafficSystemFollowCamera>())
				m_followCameraScript = transform.GetComponent<TrafficSystemFollowCamera>();

		// Initial API status write
		if(enableAPIControl)
		{
			WriteAPISystemStatus();
		}
	}

	void Update()
	{
		#if UNITY_EDITOR
		if(Instance == null)
			Instance = this;
		#endif
		
		// Toggle debug logging with 'D' key
		if(Input.GetKeyDown(KeyCode.D))
		{
			enableDebugLogging = !enableDebugLogging;
			Debug.Log($"=== DEBUG LOGGING {(enableDebugLogging ? "ENABLED" : "DISABLED")} ===");
		}

		// API Control Updates
		if(enableAPIControl)
		{
			// Update status file periodically
			if(Time.time - lastAPIStatusUpdate >= apiStatusUpdateInterval)
			{
				WriteAPISystemStatus();
				lastAPIStatusUpdate = Time.time;
			}
			
			// Check for API commands
			if(Time.time - lastAPICommandCheck >= apiCommandCheckInterval)
			{
				CheckForAPICommands();
				lastAPICommandCheck = Time.time;
			}
		}
	}

	// EXISTING METHODS
	public bool IsInView(Vector3 a_worldPos, bool a_isTraffic)
	{
		if(!m_mainGameCamera)
			return true;

		Vector3 dir = a_worldPos - m_mainGameCamera.transform.position;

		if (a_isTraffic) {
			if (dir.magnitude > m_distanceToCullTraffic)
				return false;
		} else {
			if (dir.magnitude > m_distanceToCullRoad)
				return false;
		}
		return true;
	}

	public bool CanSpawn()
	{
		if(m_vehicleSpawnCountMax == -1)
			return true;

		if(m_spawnedVehicles.Count < m_vehicleSpawnCountMax)
			return true;

		return false;
	}

	public TrafficSystemVehicle GetVehiclePrefabToSpawn()
	{
		if(m_vehiclePrefabs.Count <= 0)
			return null;

		int randIndex = Random.Range(0, m_vehiclePrefabs.Count);
		return m_vehiclePrefabs[randIndex];
	}

	public void RegisterVehicle( TrafficSystemVehicle a_vehicle )
	{
		if(TrafficSystemUI.Instance)
			TrafficSystemUI.Instance.AssignVehicleToFollow( a_vehicle );

		m_spawnedVehicles.Add( a_vehicle );
	}

	public void UnRegisterVehicle( TrafficSystemVehicle a_vehicle )
	{
		m_spawnedVehicles.Remove( a_vehicle );
		RespawnVehicle();
	}

	public void RegisterVehicleSpawner( TrafficSystemVehicleSpawner a_spawner )
	{
		m_vehicleSpawners.Add( a_spawner );
	}

	public void RespawnVehicle()
	{
		if(m_vehicleSpawners.Count <= 0)
			return;

		TrafficSystemVehicleSpawner spawners = m_vehicleSpawners[Random.Range(0, m_vehicleSpawners.Count)];
		spawners.RespawnVehicle();
	}

	public void SetAllVehicleFrontLights( bool a_enabled )
	{
		for(int vIndex = 0; vIndex < m_spawnedVehicles.Count; vIndex++)
			m_spawnedVehicles[vIndex].FrontLights( a_enabled );
	}

	public void SetTrafficSystemPiece( TrafficSystemTooltip a_tooltip, TrafficSystemPiece a_obj )
	{
		switch(a_tooltip)
		{
		case TrafficSystemTooltip.ANCHOR:
		{
			AnchorTrafficSystemPiece = a_obj;
			if(AnchorTrafficSystemPiece)
			{
				PositionTooltip(TrafficSystem.TrafficSystemTooltip.ANCHOR, AnchorTrafficSystemPiece);
			}
		}
			break;
		case TrafficSystemTooltip.EDIT:
		{
			EditTrafficSystemPiece = a_obj;
			if(EditTrafficSystemPiece)
			{
				PositionTooltip(TrafficSystem.TrafficSystemTooltip.EDIT, EditTrafficSystemPiece);
			}
		}
			break;
		}
	}

	public void ShowTooltip( TrafficSystemTooltip a_tooltip, bool a_show )
	{
		switch(a_tooltip)
		{
		case TrafficSystemTooltip.ANCHOR:
		{
			if(TooltipAnchor)
			{
				TooltipAnchor.SetActive( a_show );
			}
		}
			break;
		case TrafficSystemTooltip.EDIT:
		{
			if(TooltipEdit)
			{
				TooltipEdit.SetActive( a_show );
			}
		}
			break;
		}
	}

	public void PositionTooltip( TrafficSystemTooltip a_tooltip, TrafficSystemPiece a_obj )
	{
		switch(a_tooltip)
		{
		case TrafficSystemTooltip.ANCHOR:
		{
			if(TooltipAnchor)
			{
				TooltipAnchor.transform.position = new Vector3(a_obj.transform.position.x, a_obj.transform.position.y + a_obj.m_renderer.bounds.extents.y + 2.0f, a_obj.transform.position.z);
			}
		}
			break;
		case TrafficSystemTooltip.EDIT:
		{
			if(TooltipEdit)
			{
				TooltipEdit.transform.position = new Vector3(a_obj.transform.position.x, a_obj.transform.position.y + a_obj.m_renderer.bounds.extents.y + 2.4f, a_obj.transform.position.z);
			}
		}
			break;
		}
	}

	public void AddToCLRevealObjsFrom( Transform a_obj )
	{
		bool foundObj = false;
		if(!foundObj)
			CLRevealObjectsFrom.Add(a_obj);
	}

	public void ClearCLRevealObjsFrom()
	{
		CLRevealObjectsFrom.Clear();
	}

	public void AddToCLRevealObjsTo( Transform a_obj )
	{
		bool foundObj = false;
		if(!foundObj)
			CLRevealObjectsTo.Add(a_obj);
	}
	
	public void ClearCLRevealObjsTo()
	{
		CLRevealObjectsTo.Clear();
	}

	public bool VehicleHasFocus(TrafficSystemVehicle vehicle)
	{
		// Simple implementation - you can expand this based on your needs
		return false;
	}

	// NEW API CONTROL METHODS
	void SetupAPIPaths()
	{
		try
		{
			string baseDirectory;
			
			#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			baseDirectory = "C:/temp/unity-traffic/";
			#else
			baseDirectory = "/tmp/unity-traffic/";
			#endif
			
			if(string.IsNullOrEmpty(baseDirectory))
			{
				Debug.LogError("Base directory is null - cannot setup API paths");
				enableAPIControl = false;
				return;
			}
			
			apiStatusFilePath = System.IO.Path.Combine(baseDirectory, "traffic_system_status.json");
			apiCommandsPath = System.IO.Path.Combine(baseDirectory, "commands");
			
			// Create directories
			System.IO.Directory.CreateDirectory(baseDirectory);
			System.IO.Directory.CreateDirectory(apiCommandsPath);
			
			if(enableDebugLogging)
			{
				Debug.Log($"API PATHS SETUP: Status={apiStatusFilePath}, Commands={apiCommandsPath}");
			}
		}
		catch(System.Exception e)
		{
			Debug.LogError($"Failed to setup API paths: {e.Message}");
			enableAPIControl = false;
		}
	}

	void DiscoverAllTrafficLights()
	{
		discoveredLights.Clear();
		lightsByID.Clear();
		
		// Find all traffic lights in the scene
		TrafficSystemTrafficLight[] foundLights = FindObjectsOfType<TrafficSystemTrafficLight>();
		
		for(int i = 0; i < foundLights.Length; i++)
		{
			TrafficSystemTrafficLight light = foundLights[i];
			string lightID = GenerateAPILightID(light, i);
			
			discoveredLights.Add(light);
			lightsByID[lightID] = light;
			
			if(enableDebugLogging)
			{
				Debug.Log($"API DISCOVERED LIGHT: {lightID} at position {light.transform.position}");
			}
		}
	}

	string GenerateAPILightID(TrafficSystemTrafficLight light, int index)
	{
		// Try to use intersection name if available
		if(light.m_intersection != null && !string.IsNullOrEmpty(light.m_intersection.name))
		{
			return $"light_{light.m_intersection.name}_{index}".Replace(" ", "_");
		}
		
		// Fallback to position-based ID
		Vector3 pos = light.transform.position;
		return $"light_{pos.x:F0}_{pos.z:F0}_{index}".Replace("-", "neg");
	}

	void WriteAPISystemStatus()
	{
		try
		{
			// Safety check - make sure paths are set up
			if(string.IsNullOrEmpty(apiStatusFilePath))
			{
				SetupAPIPaths();
			}
			
			if(string.IsNullOrEmpty(apiStatusFilePath))
			{
				Debug.LogWarning("API status file path is still null after setup - API control disabled");
				enableAPIControl = false;
				return;
			}

			// Manually create JSON to avoid JsonUtility issues
			System.Text.StringBuilder jsonBuilder = new System.Text.StringBuilder();
			jsonBuilder.Append("{\n");
			jsonBuilder.Append("  \"lights\": [\n");
			
			for(int i = 0; i < discoveredLights.Count; i++)
			{
				TrafficSystemTrafficLight light = discoveredLights[i];
				if(light == null) continue;
				
				string lightID = "";
				foreach(var kvp in lightsByID)
				{
					if(kvp.Value == light)
					{
						lightID = kvp.Key;
						break;
					}
				}
				
				jsonBuilder.Append("    {\n");
				jsonBuilder.AppendFormat("      \"id\": \"{0}\",\n", lightID);
				jsonBuilder.AppendFormat("      \"name\": \"{0}\",\n", light.gameObject.name);
				jsonBuilder.AppendFormat("      \"status\": \"{0}\",\n", light.m_status.ToString().ToLower());
				jsonBuilder.AppendFormat("      \"controlMode\": \"{0}\",\n", light.GetControlMode().ToString().ToLower());
				jsonBuilder.Append("      \"position\": {\n");
				jsonBuilder.AppendFormat("        \"x\": {0},\n", light.transform.position.x);
				jsonBuilder.AppendFormat("        \"y\": {0},\n", light.transform.position.y);
				jsonBuilder.AppendFormat("        \"z\": {0}\n", light.transform.position.z);
				jsonBuilder.Append("      },\n");
				jsonBuilder.AppendFormat("      \"intersection\": \"{0}\",\n", light.m_intersection ? light.m_intersection.name : "none");
				jsonBuilder.AppendFormat("      \"greenDuration\": {0},\n", light.m_greenDuration);
				jsonBuilder.AppendFormat("      \"timeSinceGreen\": {0},\n", light.m_timeSinceGreen);
				jsonBuilder.AppendFormat("      \"turnLeftAnytime\": {0},\n", light.m_turnLeftAnytime.ToString().ToLower());
				jsonBuilder.AppendFormat("      \"manualOverride\": {0}\n", light.m_manualOverride.ToString().ToLower());
				jsonBuilder.Append("    }");
				
				if(i < discoveredLights.Count - 1) jsonBuilder.Append(",");
				jsonBuilder.Append("\n");
			}
			
			jsonBuilder.Append("  ],\n");
			jsonBuilder.AppendFormat("  \"totalLights\": {0},\n", discoveredLights.Count);
			jsonBuilder.AppendFormat("  \"timestamp\": \"{0}\",\n", System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
			jsonBuilder.Append("  \"systemActive\": true\n");
			jsonBuilder.Append("}");
			
			// Write to file
			System.IO.File.WriteAllText(apiStatusFilePath, jsonBuilder.ToString());
			
			if(enableDebugLogging && Time.time % 5f < Time.deltaTime)
			{
				Debug.Log($"API STATUS UPDATE: {discoveredLights.Count} lights updated");
			}
		}
		catch(System.Exception e)
		{
			Debug.LogError($"Failed to write API system status: {e.Message}");
		}
	}

	void CheckForAPICommands()
	{
		try
		{
			// Safety check - make sure paths are set up
			if(string.IsNullOrEmpty(apiCommandsPath))
			{
				return;
			}
			
			if(!System.IO.Directory.Exists(apiCommandsPath)) return;
			
			string[] commandFiles = System.IO.Directory.GetFiles(apiCommandsPath, "*_command.json");
			
			foreach(string commandFile in commandFiles)
			{
				ProcessAPICommandFile(commandFile);
			}
		}
		catch(System.Exception e)
		{
			Debug.LogError($"Failed to check API commands: {e.Message}");
		}
	}

	void ProcessAPICommandFile(string commandFile)
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
			if(HasProcessedAPICommand(lightID, command.timestamp)) return;
			
			// Find the target light
			if(lightsByID.ContainsKey(lightID))
			{
				TrafficSystemTrafficLight light = lightsByID[lightID];
				ProcessAPILightCommand(light, lightID, command);
				
				// Mark command as processed
				MarkAPICommandProcessed(lightID, command.timestamp);
				
				if(enableDebugLogging)
				{
					Debug.Log($"API COMMAND PROCESSED: {lightID} - {command.action} - {command.status}");
				}
			}
			else if(lightID == "manager")
			{
				// Handle special manager commands
				ProcessManagerCommand(command);
				MarkAPICommandProcessed(lightID, command.timestamp);
			}
			else
			{
				Debug.LogWarning($"API: Unknown light ID in command: {lightID}");
			}
		}
		catch(System.Exception e)
		{
			Debug.LogError($"Failed to process API command file {commandFile}: {e.Message}");
		}
	}

	void ProcessAPILightCommand(TrafficSystemTrafficLight light, string lightID, TrafficLightCommand command)
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
		}
	}

	void ProcessManagerCommand(TrafficLightCommand command)
	{
		switch(command.action.ToLower())
		{
			case "chaos_mode":
				// Check if chaos should be enabled (duration > 0 means enable)
				if(command.duration > 0)
				{
					StartCoroutine(APIChaosRoutine(command.duration));
				}
				break;
				
			case "all_red":
				foreach(var light in discoveredLights)
				{
					if(light != null)
					{
						light.SetControlMode(TrafficSystemTrafficLight.ControlMode.API_CONTROLLED);
						light.SetStatus(TrafficSystemTrafficLight.Status.RED, false);
					}
				}
				break;
				
			case "restore_all":
				foreach(var light in discoveredLights)
				{
					if(light != null)
					{
						light.SetControlMode(TrafficSystemTrafficLight.ControlMode.AUTOMATIC);
					}
				}
				break;
		}
	}

	IEnumerator APIChaosRoutine(float duration)
	{
		TrafficSystemTrafficLight.Status[] statuses = {
			TrafficSystemTrafficLight.Status.RED,
			TrafficSystemTrafficLight.Status.YELLOW,
			TrafficSystemTrafficLight.Status.GREEN
		};
		
		float endTime = Time.time + duration;
		
		if(enableDebugLogging)
		{
			Debug.Log($"API CHAOS MODE STARTED: Duration {duration}s affecting {discoveredLights.Count} lights");
		}
		
		while(Time.time < endTime)
		{
			foreach(var light in discoveredLights)
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
		foreach(var light in discoveredLights)
		{
			if(light != null)
			{
				light.SetControlMode(TrafficSystemTrafficLight.ControlMode.AUTOMATIC);
			}
		}
		
		if(enableDebugLogging)
		{
			Debug.Log("API CHAOS MODE ENDED: Restored all lights to automatic");
		}
	}

	bool HasProcessedAPICommand(string lightID, long timestamp)
	{
		string timestampFile = System.IO.Path.Combine(apiCommandsPath, $"{lightID}_last_timestamp.txt");
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

	void MarkAPICommandProcessed(string lightID, long timestamp)
	{
		string timestampFile = System.IO.Path.Combine(apiCommandsPath, $"{lightID}_last_timestamp.txt");
		System.IO.File.WriteAllText(timestampFile, timestamp.ToString());
	}

	// Public API methods for external access
	public List<TrafficSystemTrafficLight> GetAllDiscoveredLights()
	{
		return new List<TrafficSystemTrafficLight>(discoveredLights);
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

	public void RefreshAPITrafficLights()
	{
		DiscoverAllTrafficLights();
		WriteAPISystemStatus();
	}
}