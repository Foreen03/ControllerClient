## Installation

1. Copy the `.dll` files given into your Unity Project:
```
Assets/
└── Plugins
```

2. Ensure the following namespace are accessible:
```csharp
using ClientController
```

---
## Core Components

### Controller

Manages connection, input dispatching, and exposes events and action states.

```csharp
Controller client;
```

### MotionSettings

Controls how raw controller input is converted into motion intent.

```csharp
MotionSettings settings;
```

### MotionIntent

Represents processed movement intent.

```csharp
MotionIntent currentIntent;
```

---

## Initialization

### Create MotionSettings

```csharp
settings = new MotionSettings();
settings.StepImpulse = 0.4f;
settings.MaxMove = 1.5f;
settings.MoveDamping = 2.0f;
```

### MotionSettings Reference

`MotionSettings` controls how raw input is translated into movement intent.

|      Parameter      |               Description               | Default | Recommended Range |
| :-----------------: | :-------------------------------------: | :-----: | :---------------: |
|      `MaxTilt`      |  Maximum tilt angle used for steering   |  `0.6`  |    `0.4 – 0.8`    |
|     `DeadZone`      | Minimum input threshold to ignore noise | `0.05`  |   `0.03 – 0.08`   |
| `SteeringSmoothing` |      Steering interpolation factor      | `0.12`  |    `0.1 – 0.3`    |
|    `StepImpulse`    |         Movement added per step         |  `0.4`  |    `0.3 – 0.6`    |
|      `MaxMove`      |     Maximum forward movement intent     |  `1.5`  |    `1.0 – 2.0`    |
|    `MoveDamping`    |       How quickly movement decays       |  `2.0`  |    `1.5 – 3.0`    |

### Creating and Connecting the Controller

```csharp
client = new Controller(settings);

client.OnMotion += OnMotion;
client.OnCommand += OnCommand;
client.OnConnectionStateChanged += OnConnectionStateChanged;
client.OnScreenshot += OnScreenshot;

client.Connect();
```

- `OnMotion` receives movement intent
- `OnCommand` receives commands (pause / resume)
- `OnConnectionStateChanged` handles the connection state within Android controller app and game.
- `OnScreenshot` receives screenshot capture results
- `Connect()` starts the controller connection

---

## Dispatching Input

### Dispatch Loop (Required)

`client.Dispatch()` **must be called** in `Update()`.

```csharp
void Update()
{
    if (gamePause) return;
    client.Dispatch();
}
```

> If `Dispatch()` is not called, motion, actions, and commands will not update.

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

## Update Loop Responsibilities

|  Unity Method   |            Responsibility             |
| :-------------: | :-----------------------------------: |
|   `Update()`    |     Dispatch input & read actions     |
| `FixedUpdate()` |         Apply motion intent           |
| Event Callbacks | Handle motion, commands & screenshots |

---

## Full Example in Unity

```csharp
using ClientController;
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
        client.Connect();
    }

    void Update()
    {  
        client.Dispatch();

        if (client.Actions.Get("fire"))
            Fire();

        if (client.Actions.Get("jump"))
            Jump();
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
                Pause();
                break;
            case "resume":
                Resume();
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
}
```
