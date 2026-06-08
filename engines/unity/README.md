## Installation

1. Copy the `.dll` files and `ControllerBridge.cs` into your Unity project's `Assets/` directory (typically in `Assets/Plugins/` or `Assets/Scripts/`):
```
Assets/
├── Plugins/
│   ├── ControllerClient.dll
│   ├── Newtonsoft.Json.dll
│   └── (other dependency DLLs)
└── Scripts/
    └── ControllerBridge.cs
```

2. Ensure the following namespace is accessible in your scripts:
```csharp
using ControllerClient;
```

---

## Quick Start

### Option A: Use the ControllerBridge component (Recommended)

1. Attach the **ControllerBridge** component to an empty GameObject in your scene (e.g. named `ControllerBridge`).
2. Configure the connection URL and motion parameters in the Unity Inspector.
3. Reference the bridge from your player/gameplay script:

```csharp
using UnityEngine;
using ControllerClient;

public class PlayerController : MonoBehaviour
{
    private ControllerBridge bridge;
    private Rigidbody rb;
    public float moveSpeed = 6f;
    public float turnSpeed = 120f;

    void Start()
    {
        // Find the bridge instance
        bridge = ControllerBridge.Instance;
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Polling button actions
        if (bridge.IsActionPressed("jump"))
        {
            Jump();
        }
    }

    void FixedUpdate()
    {
        MotionIntent intent = bridge.LastMotion;

        if (intent.Move > 0.01f || Mathf.Abs(intent.Turn) > 0.01f)
        {
            // Apply turning
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, intent.Turn * turnSpeed * Time.fixedDeltaTime, 0));

            // Apply forward movement
            rb.MovePosition(rb.position + transform.forward * intent.Move * moveSpeed * Time.fixedDeltaTime);
        }
    }

    void Jump()
    {
        rb.AddForce(Vector3.up * 5f, ForceMode.Impulse);
    }
}
```

#### Inspector UnityEvents

You can also assign callback methods directly in the inspector using the exposed UnityEvents:
* `onMotionUpdated` (takes `MotionIntent`)
* `onCommandReceived` (takes `string`)
* `onConnectionStateChanged` (takes `bool`)
* `onScreenshotReceived` (takes `ScreenshotResult`)
* `onGpxExported` (takes `GpxExportResult`)

---

### Option B: Use the Controller class directly (Advanced)

If you prefer to manage the controller lifecycle and update loops manually within your own scripts:

1. Create and configure `MotionSettings`:
```csharp
var settings = new MotionSettings();
settings.StepImpulse = 0.4f;
settings.MaxMove = 1.5f;
settings.MoveDamping = 2.0f;
```

2. Instantiate and connect the controller:
```csharp
var client = new Controller(settings);

client.OnMotion += OnMotion;
client.OnCommand += OnCommand;
client.OnConnectionStateChanged += OnConnectionStateChanged;
client.Connect("ws://localhost:8765/sensor");
```

3. Call `Dispatch()` inside `Update()`:
```csharp
void Update()
{
    client.Dispatch();
}
```
> **Important:** You must call `client.Dispatch()` every frame in `Update()`, otherwise events will not fire and states will not update.

---

## Core Components

### ControllerBridge

Exposes configuration and state fields to the inspector and handles automatic connection lifecycle and event routing.

| Property | Default | Range | Description |
| :--- | :--- | :--- | :--- |
| `Url` | `ws://localhost:8765/sensor` | — | WebSocket URL of the sensor relay server |
| `MaxTilt` | `0.6` | `0.1 – 1.0` | Maximum tilt angle for steering |
| `DeadZone` | `0.05` | `0.0 – 0.5` | Tilt below this value is ignored |
| `SteeringSmoothing` | `0.12` | `0.01 – 1.0` | Interpolation factor for smooth steering |
| `TurnSpeedDeg` | `120.0` | `10.0 – 360.0` | Turn speed in degrees per second |
| `StepImpulse` | `0.4` | `0.05 – 2.0` | Velocity added per detected step |
| `MaxMove` | `1.5` | `0.5 – 5.0` | Maximum forward speed ceiling |
| `MoveDamping` | `2.0` | `0.1 – 10.0` | Damping applied to forward decay |

> [!TIP]
> **Runtime Settings Synchronization:** You can modify these variables (e.g., `bridge.maxTilt = 0.5f`) programmatically or tune them directly in the Unity Inspector at runtime during Play Mode. The bridge automatically propagates modifications to the underlying `MotionSettings` instance every frame.


### MotionIntent

Represents processed movement intent.

| Field | Description | Range |
| :--- | :--- | :--- |
| `intent.Move` | Forward movement intent | `0.0` to `1.0` |
| `intent.Turn` | Turning/steering direction | `-1.0` to `1.0` |
| `intent.rawSteps` | Cumulative raw steps count | `N/A` |
| `intent.StepsCadence` | Steps per minute | `0` to `200` |

---

## Dispatching Input (Required for Option B only)

`client.Dispatch()` **must be called** in `Update()`.

```csharp
void Update()
{
    if (gamePause) return;
    client.Dispatch();
}
```

> If `Dispatch()` is not called under Option B, motion, actions, and commands will not update.


---

## Motion Handling (Movement & Turning)

### Receiving Motion Intent

```csharp
void OnMotion(MotionIntent intent)
{
    currentIntent = intent;
}
```

### MotionIntent Field

|         Field         |    Description    |   Range   |
| :-------------------: | :---------------: | :-------: |
|     `intent.Move`     | Forward movement  |  `0 - 1`  |
|     `intent.Turn`     | Turning direction | `-1 - 1`  |
|   `intent.rawSteps`   |  Raw steps data   |   `NA`    |
| `intent.StepsCadence` | Steps per minute  | `0 - 200` |

---

### Applying Motion

Use `FixedUpdate()` with `Rigidbody` for smooth and stable motion.

```csharp
void FixedUpdate()
{

    if (currentIntent.Move > 0.01f || Mathf.Abs(currentIntent.Turn) > 0.01f)
    {
        rb.MoveRotation(
            rb.rotation *
            Quaternion.Euler(
                0,
                currentIntent.Turn * turnSpeed * Time.fixedDeltaTime,
                0
            )
        );

        rb.MovePosition(
            rb.position +
            transform.forward *
            currentIntent.Move *
            moveSpeed *
            Time.fixedDeltaTime
        );
    }
}
```

### Notes

- `Move` controls forward speed
- `Turn` controls tilt rotation
- Uses `Rigidbody.MovePosition()` and `MoveRotation()` for smooth physics interaction

---

## Actions

Actions are **Boolean states** exposed by the SDK.

### Reading Actions

```csharp
if (client.Actions.Get("fire"))
    Fire();

if (client.Actions.Get("jump"))
    Jump();
```

> The string parameter in `client.Actions.Get()` is customized by the developer while designing the game pad interface.

### Action Characteristics

- Polled every frame
- Represent button presses

---

## Commands (Pause & Resume)

Commands are **discrete events** sent via the controller.

### Receiving Commands

```csharp
void OnCommand(string command)
{
    switch (command)
    {
        case "pause":
            Pause();
            break;

        case "resume":
            Resume();
            break;
    }
}
```

### Command Characteristics

- Triggered once per command

---
## Connected State

Connection state will be updated according to the connection within Android controller app and game.

### Connection State

```csharp
void OnConnectionStateChanged(bool connected)
{
    if (connected)
    {
        Debug.Log("Controller connected.");
    }
    else
    {
        Debug.Log("Controller disconnected.");
    }
}
```

---

## Screenshot Capture

The SDK can request screenshots from the PC server. Screenshots are saved as JPEG files on the PC and the result is delivered via the `OnScreenshot` event.

> Screenshots are saved to disk and only a tiny notification is sent over WebSocket, so they have **zero impact** on the 60Hz movement channel.

### Capture Modes

```csharp
// Mode 1: Full monitor (includes taskbar, all windows)
client.CaptureScreen();

// Mode 2: Foreground window only (auto-detected, no taskbar)
client.CaptureScreen(windowMode: true);

// Mode 3: Find window by title (partial, case-insensitive match)
client.CaptureScreen("My Game");
```

### Receiving Screenshot Results

```csharp
void OnScreenshot(ScreenshotResult result)
{
    Debug.Log($"Screenshot saved: {result.FilePath}");
    Debug.Log($"Size: {result.Width}x{result.Height}");

    // Read the file when ready (e.g., load as texture)
    byte[] fileData = System.IO.File.ReadAllBytes(result.FilePath);
    Texture2D tex = new Texture2D(result.Width, result.Height);
    tex.LoadImage(fileData);
}
```

### ScreenshotResult Fields

|    Field    |              Description               |       Type       |
| :---------: | :------------------------------------: | :--------------: |
| `FilePath`  | Full path to the saved JPEG file       |     `string`     |
|   `Width`   | Width of the captured image in pixels  |      `int`       |
|  `Height`   | Height of the captured image in pixels |      `int`       |

---

## GPX Recording

The SDK supports GPX trail recording, which is useful for fitness games, route simulation, and tracking walking trails. Recorded trails are saved as standard `.gpx` files on the PC server and can be imported directly into apps like Strava.

### Recording Modes

1. **Simulated / Random-Trail Mode (Default)**
   The server generates a random trail starting at the origin coordinates. The position advances automatically based on the steps cadence detected by the phone controller sensor.
   
2. **Manual-Location Mode**
   The game character's exact coordinates are forwarded dynamically. The trail is built point-by-point directly from the player's movement in the game world.

> [!IMPORTANT]
> When starting a GPX recording in **Manual-Location Mode**, you should pass your game's reference origin coordinates (`originLat` and `originLon`) to `client.StartGpx(lat, lon, true)`.
> 
> If you call `client.StartGpx(true)` (which uses the server's default origin coordinates `3.2206334, 101.9676587`) but your game coordinates translate to a different latitude and longitude, the exported GPX trail will contain a large teleportation spike from the default origin to the actual game coordinates.

---

### GPX API Methods

```csharp
// 1. Start GPX recording:
// Start random trail mode at starting point defined in pc server
client.StartGpx();

// Start random trail mode at custom starting point
client.StartGpx(originLat, originLon);

// Start manual mode at custom starting point (Recommended for Manual Mode)
client.StartGpx(originLat, originLon, manualLocation: true);

// Start manual mode at starting point defined in pc server
client.StartGpx(manualLocation: true);


// 2. Update player location in Manual Mode:
// Call this periodically (e.g. from Update/FixedUpdate). Throttled to ~2Hz automatically.
client.UpdateGpxLocation(lat, lon);


// 3. Export the recorded GPX trail to disk:
client.ExportGpx();
```

---

### Receiving GPX Export Results

Subscribe to `OnGpxExported` to receive notification of the saved file path, distance, and duration:

```csharp
client.OnGpxExported += OnGpxExported;

void OnGpxExported(GpxExportResult result)
{
    if (!string.IsNullOrEmpty(result.Error))
    {
        Debug.LogError($"GPX Export failed: {result.Error}");
        return;
    }

    Debug.Log($"GPX file saved at: {result.FilePath}");
    Debug.Log($"Distance walked: {result.DistanceKm} km");
    Debug.Log($"Duration: {result.Duration}");
}
```

### GpxExportResult Fields

| Field | Description | Type |
| :---: | :---: | :---: |
| `FilePath` | Full path to the exported `.gpx` file on PC disk | `string` |
| `DistanceKm` | Total distance walked in kilometres | `double` |
| `Duration` | Formatted walking duration (`hh:mm:ss`) | `string` |
| `Error` | Error message (if the export failed) | `string` |

---

## Update Loop Responsibilities

|  Unity Method   |            Responsibility             |
| :-------------: | :-----------------------------------: |
|   `Update()`    |     Dispatch input & read actions     |
| `FixedUpdate()` |         Apply motion intent           |
| Event Callbacks | Handle motion, commands & screenshots |

---

## Full Example in Unity

```csharp
using ControllerClient;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private bool gamePause;
    
    public float moveSpeed = 6f;
    public float turnSpeed = 120f;

    private Controller client;
    private MotionSettings settings;
    private MotionIntent currentIntent;
    private Rigidbody rb;

    // GPX origin coordinates (change to match your arena's real-world lat/lon)
    private const double originLat = 3.2206334;
    private const double originLon = 101.9676587;
    private float lastGpsUpdate;

    void Start()
    {
        settings = new MotionSettings();
        settings.StepImpulse = 0.4f;
        settings.MaxMove = 1.5f;
        settings.MoveDamping = 2.0f;
        
        client = new Controller(settings);
        
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.useGravity = true;

        client.OnMotion += OnMotion;
        client.OnCommand += OnCommand;
        client.OnConnectionStateChanged += OnConnectionStateChanged;
        client.OnScreenshot += OnScreenshot;
        client.OnGpxExported += OnGpxExported;
        
        client.Connect();
    }

    void Update()
    {  
        client.Dispatch();

        if (client.Actions.Get("fire"))
            Fire();

        if (client.Actions.Get("jump"))
            Jump();

        // Start GPX recording in manual-location mode using custom origin
        if (client.Actions.Get("startGpx"))
            client.StartGpx(originLat, originLon, manualLocation: true);

        // Export the GPX trail
        if (client.Actions.Get("exportGpx"))
            client.ExportGpx();

        // Update player coordinates in manual-location mode (throttled automatically to ~2Hz)
        if (Time.time - lastGpsUpdate >= 0.5f)
        {
            var gps = UnityToGps(transform.position);
            client.UpdateGpxLocation(gps.lat, gps.lon);
            lastGpsUpdate = Time.time;
        }
    }

    void FixedUpdate()
    {
        if (currentIntent.Move > 0.01f || Mathf.Abs(currentIntent.Turn) > 0.01f)
        {
            rb.MoveRotation(
                rb.rotation *
                Quaternion.Euler(
                    0,
                    currentIntent.Turn * turnSpeed * Time.fixedDeltaTime,
                    0
                )
            );

            rb.MovePosition(
                rb.position +
                transform.forward *
                currentIntent.Move *
                moveSpeed *
                Time.fixedDeltaTime
            );
        }
    }

    void OnMotion(MotionIntent intent)
    {
        currentIntent = intent;
    }

    void OnCommand(string command)
    {
        switch (command)
        {
            case "pause":
                gamePause = true;
                break;
            case "resume":
                gamePause = false;
                break;
        }
    }
    
    void OnConnectionStateChanged(bool connected)
    {
        if (connected)
        {
            Debug.Log("Controller connected.");
        }
        else
        {
            Debug.Log("Controller disconnected.");
        }
    }

    void OnScreenshot(ScreenshotResult result)
    {
        Debug.Log($"Screenshot saved: {result.FilePath} ({result.Width}x{result.Height})");

        // Example: Load screenshot as a Unity texture
        byte[] fileData = System.IO.File.ReadAllBytes(result.FilePath);
        Texture2D tex = new Texture2D(result.Width, result.Height);
        tex.LoadImage(fileData);
    }

    void OnGpxExported(GpxExportResult result)
    {
        if (!string.IsNullOrEmpty(result.Error))
        {
            Debug.LogError($"GPX Export failed: {result.Error}");
            return;
        }

        Debug.Log($"GPX file saved: {result.FilePath}");
        Debug.Log($"Distance walked: {result.DistanceKm} km");
        Debug.Log($"Duration: {result.Duration}");
    }

    (double lat, double lon) UnityToGps(Vector3 pos)
    {
        const double metersPerDegreeLat = 111000.0;
        double lat = originLat + (pos.z / metersPerDegreeLat);
        double metersPerDegreeLon = metersPerDegreeLat * Mathf.Cos((float)(originLat * Mathf.Deg2Rad));
        double lon = originLon + (pos.x / metersPerDegreeLon);
        return (lat, lon);
    }

    void Fire()
    {
        // Shooting logic here
    }

    void Jump()
    {
        // Jumping logic here
    }
}
```
