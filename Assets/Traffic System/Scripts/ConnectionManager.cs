using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine.Networking;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    [Header("UI References - Required")]
    public TMP_InputField serverUrlInput;
    public Button selectPemButton;
    public TextMeshProUGUI pemFileNameText;  // NEW - shows the selected filename
    public TextMeshProUGUI statusText;
    public Button connectButton;

    [Header("Connection Settings")]
    public float pollInterval = 0.5f;

    private string serverUrl;
    private string pemKeyPath;
    private string pemKeyContent;
    private bool isConnected = false;
    private Coroutine pollCoroutine;

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
        selectPemButton.onClick.AddListener(OnSelectPemFile);
        connectButton.onClick.AddListener(OnConnectButtonClicked);
        serverUrlInput.onValueChanged.AddListener(OnServerUrlChanged);
        
        UpdateStatusText("NOT CONNECTED", Color.yellow);
        connectButton.interactable = false;
        
        // Set initial filename text
        if (pemFileNameText != null)
        {
            pemFileNameText.text = "No File Selected";
            pemFileNameText.color = Color.gray;
        }
    }

    public void OnSelectPemFile()
    {
        #if UNITY_EDITOR
        pemKeyPath = UnityEditor.EditorUtility.OpenFilePanel("Select PEM Key File", "", "pem");
        
        if (!string.IsNullOrEmpty(pemKeyPath))
        {
            try
            {
                pemKeyContent = File.ReadAllText(pemKeyPath);
                string fileName = Path.GetFileName(pemKeyPath);
                
                // Update the filename display
                if (pemFileNameText != null)
                {
                    pemFileNameText.text = fileName;
                    pemFileNameText.color = Color.white;
                }
                
                UpdateStatusText($"PEM KEY LOADED: {fileName}", Color.green);
                CheckCanConnect();
            }
            catch (Exception e)
            {
                UpdateStatusText($"FAILED TO LOAD PEM: {e.Message}", Color.red);
                pemKeyContent = null;
                
                if (pemFileNameText != null)
                {
                    pemFileNameText.text = "Failed to load file";
                    pemFileNameText.color = Color.red;
                }
            }
        }
        #else
        UpdateStatusText("FILE PICKER ONLY WORKS IN EDITOR", Color.red);
        #endif
    }

    public void OnServerUrlChanged(string url)
    {
        serverUrl = url;
        CheckCanConnect();
    }

    void CheckCanConnect()
    {
        bool canConnect = !string.IsNullOrEmpty(serverUrl) && !string.IsNullOrEmpty(pemKeyContent);
        connectButton.interactable = canConnect && !isConnected;
    }

    public void OnConnectButtonClicked()
    {
        if (isConnected)
        {
            Disconnect();
        }
        else
        {
            Connect();
        }
    }

    void Connect()
    {
        serverUrl = serverUrlInput.text.Trim();
        
        if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(pemKeyContent))
        {
            UpdateStatusText("MISSING SERVER URL OR PEM KEY", Color.red);
            return;
        }

        UpdateStatusText("CONNECTING...", new Color(1f, 0.65f, 0f)); // Orange
        StartCoroutine(TestConnection());
    }

    IEnumerator TestConnection()
    {
        string testUrl = $"{serverUrl}/api/status";
        
        UnityWebRequest request = UnityWebRequest.Get(testUrl);
        request.SetRequestHeader("X-PEM-Key", Convert.ToBase64String(Encoding.UTF8.GetBytes(pemKeyContent)));
        
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            isConnected = true;
            UpdateStatusText("CONNECTED", Color.green);
            
            // Update button text
            TextMeshProUGUI buttonText = connectButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "Disconnect";
            }
            
            pollCoroutine = StartCoroutine(PollServerData());
        }
        else
        {
            UpdateStatusText($"CONNECTION FAILED: {request.error}", Color.red);
            isConnected = false;
        }
    }

    void Disconnect()
    {
        if (pollCoroutine != null)
        {
            StopCoroutine(pollCoroutine);
            pollCoroutine = null;
        }

        isConnected = false;
        UpdateStatusText("DISCONNECTED", Color.yellow);
        
        TextMeshProUGUI buttonText = connectButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = "Connect";
        }
        
        connectButton.interactable = true;
    }

    IEnumerator PollServerData()
    {
        while (isConnected)
        {
            yield return StartCoroutine(FetchTrafficLights());
            yield return StartCoroutine(FetchVehicles());
            yield return new WaitForSeconds(pollInterval);
        }
    }

    IEnumerator FetchTrafficLights()
    {
        string url = $"{serverUrl}/api/traffic/lights";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("X-PEM-Key", Convert.ToBase64String(Encoding.UTF8.GetBytes(pemKeyContent)));
        
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            TrafficDataSync.Instance?.UpdateTrafficLights(json);
        }
        else
        {
            Debug.LogError($"Failed to fetch traffic lights: {request.error}");
        }
    }

    IEnumerator FetchVehicles()
    {
        string url = $"{serverUrl}/api/vehicles";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("X-PEM-Key", Convert.ToBase64String(Encoding.UTF8.GetBytes(pemKeyContent)));
        
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            TrafficDataSync.Instance?.UpdateVehicles(json);
        }
    }

    void UpdateStatusText(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        Debug.Log($"Connection Status: {message}");
    }

    public bool IsConnected()
    {
        return isConnected;
    }

    void OnDestroy()
    {
        if (pollCoroutine != null)
        {
            StopCoroutine(pollCoroutine);
        }
    }
}