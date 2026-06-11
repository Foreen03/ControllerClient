using Godot;
using ControllerClient;

/// <summary>
/// Godot 4 Node that wraps the engine-agnostic ControllerClient.
///
/// Add this node to your scene tree. It will:
///   1. Connect to the sensor WebSocket on <c>_Ready()</c>
///   2. Dispatch queued callbacks every frame in <c>_Process()</c>
///   3. Clean up on <c>_ExitTree()</c>
///
/// Access motion data via the <see cref="LastMotion"/> property, button states
/// via <see cref="IsActionPressed"/>, or subscribe to the Godot signals.
///
/// Example (from another script):
/// <code>
/// var bridge = GetNode&lt;ControllerBridge&gt;("ControllerBridge");
/// GD.Print($"Move: {bridge.LastMotion.Move}, Turn: {bridge.LastMotion.Turn}");
///
/// if (bridge.IsActionPressed("jump"))
///     Jump();
/// </code>
/// </summary>
[GlobalClass]
public partial class ControllerBridge : Node
{
    // ── Inspector Properties ──

    [ExportGroup("Connection")]
    [Export(PropertyHint.None, "WebSocket URL of the sensor relay server")]
    public string Url { get; set; } = "ws://localhost:8765/sensor";

    [ExportGroup("Steering")]
    [Export(PropertyHint.Range, "0.1,1.0,0.01")]
    public float MaxTilt { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0.0,0.5,0.01")]
    public float DeadZone { get; set; } = 0.05f;

    [Export(PropertyHint.Range, "0.01,1.0,0.01")]
    public float SteeringSmoothing { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "10,360,1")]
    public float TurnSpeedDeg { get; set; } = 120f;

    [ExportGroup("Movement")]
    [Export(PropertyHint.Range, "0.05,2.0,0.05")]
    public float StepImpulse { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0.5,5.0,0.1")]
    public float MaxMove { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0.1,10.0,0.1")]
    public float MoveDamping { get; set; } = 2.0f;

    // ── Signals ──

    /// <summary>Emitted each frame with the latest processed motion intent.</summary>
    [Signal]
    public delegate void MotionUpdatedEventHandler(float move, float turn, long timestamp, int rawSteps, float stepsCadence);

    /// <summary>Emitted when a command string is received from the controller.</summary>
    [Signal]
    public delegate void CommandReceivedEventHandler(string command);

    /// <summary>Emitted when the WebSocket connection state changes.</summary>
    [Signal]
    public delegate void ConnectionStateChangedEventHandler(bool connected);

    /// <summary>Emitted when a screenshot result is received.</summary>
    [Signal]
    public delegate void ScreenshotReceivedEventHandler(string filePath, int width, int height);

    /// <summary>Emitted when a GPX export result is received.</summary>
    [Signal]
    public delegate void GpxExportedEventHandler(string filePath, double distanceKm, string duration, string error);

    // ── Public State ──

    /// <summary>The underlying engine-agnostic controller instance.</summary>
    public Controller Controller { get; private set; }

    /// <summary>Exposes the active MotionSettings instance.</summary>
    public MotionSettings Settings { get; private set; }

    /// <summary>Last received motion intent. Read this in <c>_Process()</c> or <c>_PhysicsProcess()</c>.</summary>
    public MotionIntent LastMotion { get; private set; }

    /// <summary>Whether the controller WebSocket is currently connected.</summary>
    public bool Connected { get; private set; }

    // ── Lifecycle ──

    public override void _Ready()
    {
        Settings = new MotionSettings();
        SyncSettings();

        Controller = new Controller(Settings);

        Controller.OnMotion += (intent) =>
        {
            LastMotion = intent;
            EmitSignal(SignalName.MotionUpdated,
                intent.Move, intent.Turn, intent.Timestamp,
                intent.rawSteps, intent.StepsCadence);
        };

        Controller.OnCommand += (command) =>
        {
            EmitSignal(SignalName.CommandReceived, command);
        };

        Controller.OnConnectionStateChanged += (isConnected) =>
        {
            Connected = isConnected;
            GD.Print($"[ControllerBridge] Connection: {(isConnected ? "OPEN" : "CLOSED")}");
            EmitSignal(SignalName.ConnectionStateChanged, isConnected);
        };

        Controller.OnScreenshot += (result) =>
        {
            EmitSignal(SignalName.ScreenshotReceived, result.FilePath, result.Width, result.Height);
        };

        Controller.OnGpxExported += (result) =>
        {
            EmitSignal(SignalName.GpxExported, result.FilePath, result.DistanceKm, result.Duration, result.Error);
        };

        Controller.Connect(Url);
        GD.Print($"[ControllerBridge] Connecting to {Url}...");
    }

    public override void _Process(double delta)
    {
        SyncSettings();
        Controller?.Dispatch();
    }

    /// <summary>
    /// Synchronizes the Godot Node properties to the underlying MotionSettings instance.
    /// This allows changing parameters dynamically at runtime (e.g. from the Inspector or scripts).
    /// </summary>
    public void SyncSettings()
    {
        if (Settings != null)
        {
            Settings.MaxTilt = MaxTilt;
            Settings.DeadZone = DeadZone;
            Settings.SteeringSmoothing = SteeringSmoothing;
            Settings.TurnSpeedDeg = TurnSpeedDeg;
            Settings.StepImpulse = StepImpulse;
            Settings.MaxMove = MaxMove;
            Settings.MoveDamping = MoveDamping;
        }
    }

    public override void _ExitTree()
    {
        Controller?.Dispose();
        Controller = null;
    }

    // ── Convenience API ──

    /// <summary>Check whether a named action button is currently pressed.</summary>
    public bool IsActionPressed(string action)
    {
        return Controller?.Actions.Get(action) ?? false;
    }

    /// <summary>Request a full-monitor screenshot from the PC server.</summary>
    public void CaptureScreen()
    {
        Controller?.CaptureScreen();
    }

    /// <summary>Request a window-only screenshot from the PC server (active window if true).</summary>
    public void CaptureScreen(bool windowMode)
    {
        Controller?.CaptureScreen(windowMode);
    }

    /// <summary>Request a screenshot of a specific window by its title.</summary>
    public void CaptureScreen(string windowTitle)
    {
        Controller?.CaptureScreen(windowTitle);
    }

    /// <summary>Request a vibration on the controller.</summary>
    /// <param name="durationMs">The duration of the vibration in milliseconds (default is 200).</param>
    public void Vibrate(int durationMs = 200)
    {
        Controller?.Vibrate(durationMs);
    }

    /// <summary>Start GPX recording with simulated route at server default origin.</summary>
    public void StartGpx()
    {
        Controller?.StartGpx();
    }

    /// <summary>Start GPX recording at server default origin. If manualLocation is true, character positions must be updated via UpdateGpxLocation.</summary>
    public void StartGpx(bool manualLocation)
    {
        Controller?.StartGpx(manualLocation);
    }

    /// <summary>Start GPX recording with simulated route at specified origin.</summary>
    public void StartGpx(double lat, double lon)
    {
        Controller?.StartGpx(lat, lon);
    }

    /// <summary>Start GPX recording at specified origin. If manualLocation is true, character positions must be updated via UpdateGpxLocation.</summary>
    public void StartGpx(double lat, double lon, bool manualLocation)
    {
        Controller?.StartGpx(lat, lon, manualLocation);
    }

    /// <summary>Updates the current GPX location in manual route mode. Automatically throttled.</summary>
    public void UpdateGpxLocation(double lat, double lon)
    {
        Controller?.UpdateGpxLocation(lat, lon);
    }

    /// <summary>Export the recorded GPX trail to the PC server.</summary>
    public void ExportGpx()
    {
        Controller?.ExportGpx();
    }
}
