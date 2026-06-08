# ControllerClient — Godot 4 (.NET) Plugin

A drop-in plugin that connects to a phone-sensor WebSocket relay and converts accelerometer + step-counter data into game-ready **MotionIntent** values (forward movement + steering).

> **Requires:** Godot 4.x with .NET (C#) support.

---

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

---

## Quick Start

### Option A: Use the ControllerBridge node (recommended)

1. Add a **ControllerBridge** node to your scene tree.
2. Configure the WebSocket URL and tuning parameters in the Inspector.
3. From any other script, read the motion data:

```csharp
public partial class Player : CharacterBody3D
{
    private ControllerBridge _bridge;

    public override void _Ready()
    {
        _bridge = GetNode<ControllerBridge>("../ControllerBridge");

        // Connect to signals
        _bridge.MotionUpdated          += OnMotionUpdated;
        _bridge.ConnectionStateChanged += OnConnectionChanged;
        _bridge.ScreenshotReceived     += OnScreenshotReceived;
        _bridge.GpxExported            += OnGpxExported;
    }

    public override void _Process(double delta)
    {
        // Direct polling approach
        float move = _bridge.LastMotion.Move;   // 0..1  forward intent
        float turn = _bridge.LastMotion.Turn;   // -1..1 steering intent
    }

    private void OnMotionUpdated(float move, float turn, long timestamp, int rawSteps, float stepsCadence)
        => GD.Print($"Move: {move}, Turn: {turn}");

    private void OnConnectionChanged(bool connected)
        => GD.Print($"Controller: {(connected ? "connected" : "disconnected")}");

    private void OnScreenshotReceived(string filePath, int width, int height)
        => GD.Print($"Screenshot saved: {filePath} ({width}x{height})");

    private void OnGpxExported(string filePath, double distanceKm, string duration, string error)
    {
        if (!string.IsNullOrEmpty(error)) { GD.PrintErr($"GPX Export failed: {error}"); return; }
        GD.Print($"GPX saved: {filePath} ({distanceKm} km, {duration})");
    }
}
```

### Option B: Use the Controller class directly

```csharp
using ControllerClient;

var ctrl = new Controller();

ctrl.OnMotion                += (intent) => GD.Print($"Move: {intent.Move}, Turn: {intent.Turn}");
ctrl.OnCommand               += (cmd)    => GD.Print($"Command: {cmd}");
ctrl.OnConnectionStateChanged += (connected) => GD.Print($"Connected: {connected}");

ctrl.Connect("ws://localhost:8765/sensor");

// In your _Process:
ctrl.Dispatch();

// On cleanup:
ctrl.Dispose();
```

---

## Checking Button State

```csharp
var bridge = GetNode<ControllerBridge>("../ControllerBridge");

if (bridge.IsActionPressed("jump"))   { /* handle jump   */ }
if (bridge.IsActionPressed("fire"))   { /* handle fire   */ }
if (bridge.IsActionPressed("crouch")) { /* handle crouch */ }
```

Button names correspond to the `buttons` object in the WebSocket movement packet.

---

## Signals

The `ControllerBridge` node emits the following Godot signals:

| Signal                    | Parameters                                                                      | Description                                        |
|---------------------------|---------------------------------------------------------------------------------|----------------------------------------------------|
| `MotionUpdated`           | `float move, float turn, long timestamp, int rawSteps, float stepsCadence`      | Emitted each frame with the latest motion intent   |
| `CommandReceived`         | `string command`                                                                | Emitted when a command string arrives              |
| `ConnectionStateChanged`  | `bool connected`                                                                | Emitted when the WebSocket connection state changes|
| `ScreenshotReceived`      | `string filePath, int width, int height`                                        | Emitted when a screenshot is saved                 |
| `GpxExported`             | `string filePath, double distanceKm, string duration, string error`             | Emitted when a GPX trail is exported               |

---

## Inspector Properties (Export Parameters)

| Parameter            | Default                        | Range       | Description                                                              |
|----------------------|--------------------------------|-------------|--------------------------------------------------------------------------|
| `Url`                | `ws://localhost:8765/sensor`   | —           | WebSocket URL of the sensor relay server                                 |
| `MaxTilt`            | `0.6`                          | 0.1 – 1.0   | Maximum tilt angle (normalized). Beyond this threshold → clamped to ±1  |
| `DeadZone`           | `0.05`                         | 0.0 – 0.5   | Tilt below this threshold is ignored (eliminates idle drift)             |
| `SteeringSmoothing`  | `0.12`                         | 0.01 – 1.0  | Smoothing factor for steering (0 = none, 1 = instant snap)               |
| `TurnSpeedDeg`       | `120`                          | 10 – 360    | Turn speed in degrees/second, used by your game logic                   |
| `StepImpulse`        | `0.4`                          | 0.05 – 2.0  | Forward velocity added per detected step                                 |
| `MaxMove`            | `1.5`                          | 0.5 – 5.0   | Forward velocity ceiling                                                 |
| `MoveDamping`        | `2.0`                          | 0.1 – 10.0  | Exponential damping applied to forward velocity each frame               |

> **Runtime Tuning:** All export properties can be modified from the Inspector during Play Mode or via script at runtime. `ControllerBridge` calls `SyncSettings()` every frame, so changes propagate to the underlying `MotionSettings` instance automatically.

---

## Public Properties

| Property       | Type           | Description                                                           |
|----------------|----------------|-----------------------------------------------------------------------|
| `Controller`   | `Controller`   | The underlying engine-agnostic controller instance                    |
| `Settings`     | `MotionSettings` | The active motion settings instance (synced from Inspector properties)|
| `LastMotion`   | `MotionIntent` | Last received motion intent — read in `_Process` or `_PhysicsProcess` |
| `Connected`    | `bool`         | Whether the WebSocket is currently connected                          |

---

## Screenshot Capture

```csharp
var bridge = GetNode<ControllerBridge>("../ControllerBridge");

// Connect signal to handle result
bridge.ScreenshotReceived += (filePath, width, height) =>
{
    GD.Print($"Screenshot saved to: {filePath} ({width}x{height})");
};

// Capture full monitor
bridge.CaptureScreen();

// Capture active window only
bridge.CaptureScreen(true);

// Capture a specific window by title
bridge.CaptureScreen("My Godot Game");
```

---

## GPX Recording

GPX recording has two modes:

- **Simulated mode** — the server generates a route automatically.
- **Manual mode** — you push the player's world position each frame, converted to real-world lat/lon.

### Overloads

```csharp
bridge.StartGpx();                                        // Simulated, server default origin
bridge.StartGpx(manualLocation: true);                    // Manual, server default origin
bridge.StartGpx(lat, lon);                                // Simulated, custom origin
bridge.StartGpx(lat, lon, manualLocation: true);          // Manual, custom origin

bridge.UpdateGpxLocation(lat, lon);                       // Push current position (manual mode only)
bridge.ExportGpx();                                       // Export trail to PC server disk
```

### Manual mode example

Convert the player's in-game `GlobalPosition` to real-world coordinates and call `UpdateGpxLocation` every physics frame:

```csharp
private const double OriginLat       = 3.2206334;
private const double OriginLon       = 101.9676587;
private const double MetersPerDegLat = 111320.0;

// Call once to begin recording
bridge.StartGpx(OriginLat, OriginLon, manualLocation: true);

// Call every _PhysicsProcess frame
var (lat, lon) = WorldToLatLon(GlobalPosition);
bridge.UpdateGpxLocation(lat, lon);

// Convert Godot world position → real-world lat/lon
private (double lat, double lon) WorldToLatLon(Vector3 worldPos)
{
    double deltaLat = worldPos.Z / MetersPerDegLat;
    double deltaLon = worldPos.X / (MetersPerDegLat * Math.Cos(OriginLat * Math.PI / 180.0));
    return (OriginLat + deltaLat, OriginLon + deltaLon);
}
```

The player's **Z axis** maps to latitude (north/south) and **X axis** maps to longitude (east/west). To scale the world (e.g. 1 unit = 10 real metres), divide `worldPos` components by a scale factor before converting.

### Listening for export result

```csharp
bridge.GpxExported += (filePath, distanceKm, duration, error) =>
{
    if (!string.IsNullOrEmpty(error))
    {
        GD.PrintErr($"GPX Export failed: {error}");
        return;
    }
    GD.Print($"GPX saved: {filePath} ({distanceKm} km, {duration})");
};

bridge.ExportGpx();
```

---

## Architecture

```
Phone Sensors → WebSocket Server → Controller (DLL) → MotionProcessor → MotionIntent
                                        ↓
                               ControllerBridge.cs (Godot Node)
                                        ↓
                                Your Game Scripts
```

The core DLL (`ControllerClient.dll`) is **engine-agnostic**. Only `ControllerBridge.cs` imports from Godot.

### Lifecycle

| Godot callback  | What ControllerBridge does                                          |
|-----------------|---------------------------------------------------------------------|
| `_Ready()`      | Creates `MotionSettings`, creates `Controller`, connects to server  |
| `_Process()`    | Calls `SyncSettings()` then `Controller.Dispatch()` every frame     |
| `_ExitTree()`   | Calls `Controller.Dispose()` and nulls the reference               |

---

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
    "buttons": { "jump": true, "fire": false, "crouch": false }
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

The special command values `controller_connected` and `controller_disconnected` trigger the `ConnectionStateChanged` signal.

---

## Troubleshooting

| Issue                                        | Solution                                                                          |
|----------------------------------------------|-----------------------------------------------------------------------------------|
| `ControllerBridge` doesn't appear in Add Node | Rebuild the C# solution (Build button or `dotnet build`)                         |
| `FileNotFoundException: ControllerClient`    | Ensure the DLL reference or project reference is added to your `.csproj`          |
| No motion data received                      | Check the WebSocket URL and verify the sensor relay server is running             |
| Connection keeps reconnecting                | The controller auto-reconnects every 2 seconds — verify the server address/port   |
| GPX positions are wildly off                 | Ensure `StartGpx` is called with `manualLocation: true` and the correct origin    |
| Screenshot saves but is blank                | Use `CaptureScreen("Window Title")` to target the correct window                  |
