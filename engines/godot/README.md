# ControllerClient — Godot 4 (.NET) Plugin

A drop-in plugin that connects to a phone-sensor WebSocket relay and converts accelerometer + step-counter data into game-ready **MotionIntent** values (forward movement + steering).

> **Requires:** Godot 4.x with .NET (C#) support.

## Installation

1. Copy the `addons/controller_client/` folder into your Godot project's `addons/` directory.

2. Add the **ControllerClient DLL** to your project:
   - Either reference the `ControllerClient.dll` in your `.csproj`:
     ```xml
     <ItemGroup>
       <Reference Include="ControllerClient">
         <HintPath>path/to/ControllerClient.dll</HintPath>
       </Reference>
     </ItemGroup>
     ```
   - Or add the ControllerClient NuGet package / project reference:
     ```xml
     <ItemGroup>
       <ProjectReference Include="path/to/ControllerClient.csproj" />
     </ItemGroup>
     ```

3. Build the C# solution (`dotnet build` or via the Godot editor's **Build** button).

Your project structure should look like:

```
your-godot-project/
├── addons/
│   └── controller_client/
│       ├── plugin.cfg
│       └── ControllerBridge.cs
├── project.godot
└── YourProject.csproj   ← reference ControllerClient.dll here
```

## Quick Start

### Option A: Use the ControllerBridge node (recommended)

1. Add a **ControllerBridge** node to your scene tree.
2. Configure the WebSocket URL and tuning parameters in the Inspector.
3. From any other script, read the motion data:

```csharp
// In your player script:
public partial class Player : CharacterBody3D
{
    private ControllerBridge _bridge;

    public override void _Ready()
    {
        _bridge = GetNode<ControllerBridge>("../ControllerBridge");

        // Option 1: Read state directly in _Process
        // Option 2: Connect to signals
        _bridge.MotionUpdated += OnMotionUpdated;
        _bridge.ConnectionStateChanged += OnConnectionChanged;
    }

    public override void _Process(double delta)
    {
        // Direct polling approach:
        float move = _bridge.LastMotion.Move;   // 0..1  forward intent
        float turn = _bridge.LastMotion.Turn;   // -1..1 steering intent

        // Use move/turn to drive your character...
    }

    private void OnMotionUpdated(float move, float turn, long timestamp, int rawSteps, float stepsCadence)
    {
        GD.Print($"Move: {move}, Turn: {turn}");
    }

    private void OnConnectionChanged(bool connected)
    {
        GD.Print($"Controller: {(connected ? "connected" : "disconnected")}");
    }
}
```

### Option B: Use the Controller class directly

```csharp
using ControllerClient;

var ctrl = new Controller();

ctrl.OnMotion += (intent) =>
{
    GD.Print($"Move: {intent.Move}, Turn: {intent.Turn}");
};

ctrl.OnCommand += (cmd) =>
{
    GD.Print($"Command: {cmd}");
};

ctrl.OnConnectionStateChanged += (connected) =>
{
    GD.Print($"Connected: {connected}");
};

ctrl.Connect("ws://localhost:8765/sensor");

// In your _Process:
ctrl.Dispatch();

// On cleanup:
ctrl.Dispose();
```

## Checking Button State

```csharp
var bridge = GetNode<ControllerBridge>("ControllerBridge");

if (bridge.IsActionPressed("jump"))
{
    // Handle jump
}
```

## Signals

The `ControllerBridge` node emits the following Godot signals:

| Signal                   | Parameters                                                    | Description                              |
|--------------------------|---------------------------------------------------------------|------------------------------------------|
| `MotionUpdated`          | `float move, float turn, long timestamp, int rawSteps, float stepsCadence` | Emitted each frame with motion data      |
| `CommandReceived`        | `string command`                                              | Emitted when a command string arrives     |
| `ConnectionStateChanged` | `bool connected`                                              | Emitted when connection state changes     |

## Inspector Properties (Export Parameters)

| Parameter            | Default | Range       | Description                                     |
|----------------------|---------|-------------|-------------------------------------------------|
| `Url`                | `ws://localhost:8765/sensor` | — | WebSocket URL of the sensor relay server |
| `MaxTilt`            | 0.6     | 0.1–1.0     | Maximum tilt angle (normalized). Beyond → ±1.   |
| `DeadZone`           | 0.05    | 0.0–0.5     | Tilt below this threshold is ignored.           |
| `SteeringSmoothing`  | 0.12    | 0.01–1.0    | Smoothing factor (0 = none, 1 = instant snap).  |
| `TurnSpeedDeg`       | 120     | 10–360      | Turn speed in deg/s (used by your game logic).  |
| `StepImpulse`        | 0.4     | 0.05–2.0    | Velocity added per detected step.               |
| `MaxMove`            | 1.5     | 0.5–5.0     | Forward velocity ceiling.                       |
| `MoveDamping`        | 2.0     | 0.1–10.0    | Exponential damping applied each frame.         |

## Architecture

```
Phone Sensors → WebSocket Server → Controller (DLL) → MotionProcessor → MotionIntent
                                       ↓
                              ControllerBridge.cs (Godot Node)
                                       ↓
                               Your Game Scripts
```

The core DLL (`ControllerClient.dll`) is **engine-agnostic**. Only `ControllerBridge.cs` imports from Godot.

## Protocol

The WebSocket server sends JSON messages with a `type` discriminator:

**Movement packet:**
```json
{
    "type": "movement",
    "x": 0.12,
    "y": 9.7,
    "z": 1.2,
    "steps": 42,
    "stepsCadence": 1.8,
    "timestamp": 1700000000000,
    "buttons": { "jump": true, "crouch": false }
}
```

**Command packet:**
```json
{
    "type": "command",
    "value": "controller_connected",
    "timestamp": 1700000000000
}
```

Special command values `controller_connected` / `controller_disconnected` trigger the `ConnectionStateChanged` signal.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `ControllerBridge` doesn't appear in Add Node | Rebuild the C# solution (Build button or `dotnet build`) |
| `FileNotFoundException: ControllerClient` | Ensure the DLL reference is added to your `.csproj` |
| No motion data | Check the WebSocket URL and ensure the sensor relay server is running |
| Connection keeps reconnecting | The controller auto-reconnects every 2 seconds — verify the server address/port |
