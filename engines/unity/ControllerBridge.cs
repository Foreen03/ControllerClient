using System;
using UnityEngine;
using UnityEngine.Events;
using ControllerClient;

/// <summary>
/// Unity MonoBehaviour component that wraps the engine-agnostic ControllerClient.
/// 
/// Add this script to a GameObject in your Unity scene. It will:
///   1. Connect to the sensor WebSocket on Start().
///   2. Dispatch queued callbacks every frame in Update().
///   3. Clean up on OnDestroy().
/// 
/// You can configure movement and steering parameters in the Unity Inspector,
/// query the latest motion intent via LastMotion, or subscribe to UnityEvents.
/// </summary>
[AddComponentMenu("Controller Client/Controller Bridge")]
public class ControllerBridge : MonoBehaviour
{
    public static ControllerBridge Instance { get; private set; }

    [Header("Connection")]
    [Tooltip("WebSocket URL of the sensor relay server")]
    public string url = "ws://localhost:8765/sensor";

    [Header("Steering Tuning")]
    [Range(0.1f, 1.0f)]
    public float maxTilt = 0.6f;
    [Range(0.0f, 0.5f)]
    public float deadZone = 0.05f;
    [Range(0.01f, 1.0f)]
    public float steeringSmoothing = 0.12f;
    [Range(10f, 360f)]
    public float turnSpeedDeg = 120f;

    [Header("Movement Tuning")]
    [Range(0.05f, 2.0f)]
    public float stepImpulse = 0.4f;
    [Range(0.5f, 5.0f)]
    public float maxMove = 1.5f;
    [Range(0.1f, 10.0f)]
    public float moveDamping = 2.0f;

    [Header("Unity Events (Inspector Hookups)")]
    public UnityEvent<MotionIntent> onMotionUpdated;
    public UnityEvent<string> onCommandReceived;
    public UnityEvent<bool> onConnectionStateChanged;
    public UnityEvent<ScreenshotResult> onScreenshotReceived;
    public UnityEvent<GpxStartedResult> onGpxStarted;
    public UnityEvent<GpxExportResult> onGpxExported;

    // ── C# Events (Code-only Hookups) ──
    public event Action<MotionIntent> OnMotionUpdated;
    public event Action<string> OnCommandReceived;
    public event Action<bool> OnConnectionStateChanged;
    public event Action<ScreenshotResult> OnScreenshotReceived;
    public event Action<GpxStartedResult> OnGpxStarted;
    public event Action<GpxExportResult> OnGpxExported;

    public Controller Controller { get; private set; }
    public MotionSettings Settings { get; private set; }
    public MotionIntent LastMotion { get; private set; }
    public bool IsConnected { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"[ControllerBridge] Multiple instances found. Destroying duplicate on {gameObject.name}.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Settings = new MotionSettings();
        SyncSettings();

        Controller = new Controller(Settings);

        // Bind events
        Controller.OnMotion += (intent) =>
        {
            LastMotion = intent;
            OnMotionUpdated?.Invoke(intent);
            onMotionUpdated?.Invoke(intent);
        };

        Controller.OnCommand += (cmd) =>
        {
            OnCommandReceived?.Invoke(cmd);
            onCommandReceived?.Invoke(cmd);
        };

        Controller.OnConnectionStateChanged += (connected) =>
        {
            IsConnected = connected;
            OnConnectionStateChanged?.Invoke(connected);
            onConnectionStateChanged?.Invoke(connected);
        };

        Controller.OnScreenshot += (result) =>
        {
            OnScreenshotReceived?.Invoke(result);
            onScreenshotReceived?.Invoke(result);
        };

        Controller.OnGpxStarted += (result) =>
        {
            OnGpxStarted?.Invoke(result);
            onGpxStarted?.Invoke(result);
        };

        Controller.OnGpxExported += (result) =>
        {
            OnGpxExported?.Invoke(result);
            onGpxExported?.Invoke(result);
        };

        Debug.Log($"[ControllerBridge] Connecting to sensor relay server at: {url}");
        Controller.Connect(url);
    }

    private void Update()
    {
        SyncSettings();
        Controller?.Dispatch();
    }

    /// <summary>
    /// Synchronizes the Inspector fields to the underlying MotionSettings instance.
    /// This allows changing parameters dynamically at runtime (e.g. from other scripts or the Inspector).
    /// </summary>
    public void SyncSettings()
    {
        if (Settings != null)
        {
            Settings.MaxTilt = maxTilt;
            Settings.DeadZone = deadZone;
            Settings.SteeringSmoothing = steeringSmoothing;
            Settings.TurnSpeedDeg = turnSpeedDeg;
            Settings.StepImpulse = stepImpulse;
            Settings.MaxMove = maxMove;
            Settings.MoveDamping = moveDamping;
        }
    }

    private void OnDestroy()
    {
        if (Controller != null)
        {
            Controller.Dispose();
            Controller = null;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ── Convenience API ──

    /// <summary>
    /// Check whether a named action button is currently pressed.
    /// </summary>
    public bool IsActionPressed(string actionName)
    {
        return Controller?.Actions.Get(actionName) ?? false;
    }

    /// <summary>
    /// Request a full-monitor screenshot from the PC server.
    /// </summary>
    public void CaptureScreen()
    {
        Controller?.CaptureScreen();
    }

    /// <summary>
    /// Request a window-only screenshot from the PC server (active window if true).
    /// </summary>
    public void CaptureScreen(bool windowMode)
    {
        Controller?.CaptureScreen(windowMode);
    }

    /// <summary>
    /// Request a screenshot of a specific window by its title.
    /// </summary>
    public void CaptureScreen(string windowTitle)
    {
        Controller?.CaptureScreen(windowTitle);
    }

    /// <summary>
    /// Request a vibration on the controller.
    /// </summary>
    /// <param name="durationMs">The duration of the vibration in milliseconds (default is 200).</param>
    public void Vibrate(int durationMs = 200)
    {
        Controller?.Vibrate(durationMs);
    }

    /// <summary>
    /// Start GPX recording with simulated route at server default origin.
    /// </summary>
    public void StartGpx()
    {
        Controller?.StartGpx();
    }

    /// <summary>
    /// Start GPX recording at server default origin.
    /// If manualLocation is true, character positions must be updated via UpdateGpxLocation.
    /// </summary>
    public void StartGpx(bool manualLocation)
    {
        Controller?.StartGpx(manualLocation);
    }

    /// <summary>
    /// Start GPX recording with simulated route at specified origin.
    /// </summary>
    public void StartGpx(double lat, double lon)
    {
        Controller?.StartGpx(lat, lon);
    }

    /// <summary>
    /// Start GPX recording at specified origin.
    /// If manualLocation is true, character positions must be updated via UpdateGpxLocation.
    /// </summary>
    public void StartGpx(double lat, double lon, bool manualLocation)
    {
        Controller?.StartGpx(lat, lon, manualLocation);
    }

    /// <summary>
    /// Updates the current GPX location in manual route mode. Automatically throttled.
    /// </summary>
    public void UpdateGpxLocation(double lat, double lon)
    {
        Controller?.UpdateGpxLocation(lat, lon);
    }

    /// <summary>
    /// Export the recorded GPX trail to the PC server.
    /// </summary>
    public void ExportGpx()
    {
        Controller?.ExportGpx();
    }
}
