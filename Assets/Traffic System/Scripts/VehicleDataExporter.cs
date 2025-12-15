using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class VehicleDataExporter : MonoBehaviour
{
    [Header("Settings")]
    public float exportInterval = 0.5f;
    public bool enableExport = true;
    
    private string vehiclesFilePath;
    private float lastExportTime;

    void Start()
    {
        SetupPaths();
    }

    void SetupPaths()
    {
        string baseDirectory;
        
        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        baseDirectory = "C:/temp/unity-traffic/";
        #else
        baseDirectory = "/tmp/unity-traffic/";
        #endif
        
        Directory.CreateDirectory(baseDirectory);
        vehiclesFilePath = Path.Combine(baseDirectory, "vehicles.json");
        
        Debug.Log($"Vehicle export path: {vehiclesFilePath}");
    }

    void Update()
    {
        if (!enableExport) return;
        
        if (Time.time - lastExportTime >= exportInterval)
        {
            ExportVehicleData();
            lastExportTime = Time.time;
        }
    }

    void ExportVehicleData()
    {
        TrafficSystemVehicle[] allVehicles = FindObjectsOfType<TrafficSystemVehicle>();
        
        VehicleExportData exportData = new VehicleExportData();
        exportData.vehicles = new List<VehicleInfo>();
        
        foreach (var vehicle in allVehicles)
        {
            VehicleInfo info = new VehicleInfo();
            info.id = vehicle.name;
            info.position = new Vector3Export(vehicle.transform.position);
            info.rotation = new Vector3Export(vehicle.transform.eulerAngles);
            info.speed = vehicle.m_velocity;
            
            exportData.vehicles.Add(info);
        }
        
        exportData.total_vehicles = exportData.vehicles.Count;
        exportData.timestamp = System.DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff");
        
        try
        {
            string json = JsonUtility.ToJson(exportData, true);
            File.WriteAllText(vehiclesFilePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to export vehicle data: {e.Message}");
        }
    }
}

[System.Serializable]
public class VehicleExportData
{
    public List<VehicleInfo> vehicles;
    public int total_vehicles;
    public string timestamp;
}

[System.Serializable]
public class VehicleInfo
{
    public string id;
    public Vector3Export position;
    public Vector3Export rotation;
    public float speed;
}

[System.Serializable]
public class Vector3Export
{
    public float x;
    public float y;
    public float z;
    
    public Vector3Export(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }
}
