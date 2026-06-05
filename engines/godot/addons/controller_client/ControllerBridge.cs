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

    // ── Public State ──

    /// <summary>The underlying engine-agnostic controller instance.</summary>
    public Controller Controller { get; private set; }

    /// <summary>Last received motion intent. Read this in <c>_Process()</c> or <c>_PhysicsProcess()</c>.</summary>
    public MotionIntent LastMotion { get; private set; }

    /// <summary>Whether the controller WebSocket is currently connected.</summary>
    public bool Connected { get; private set; }

    // ── Lifecycle ──

    public override void _Ready()
    {
        var settings = new MotionSettings
        {
            MaxTilt = MaxTilt,
            DeadZone = DeadZone,
            SteeringSmoothing = SteeringSmoothing,
            TurnSpeedDeg = TurnSpeedDeg,
            StepImpulse = StepImpulse,
            MaxMove = MaxMove,
            MoveDamping = MoveDamping,
        };

        Controller = new Controller(settings);

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

        Controller.Connect(Url);
        GD.Print($"[ControllerBridge] Connecting to {Url}...");
    }

    public override void _Process(double delta)
    {
        Controller?.Dispatch();
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
}
