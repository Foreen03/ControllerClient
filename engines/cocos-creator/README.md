# ControllerClient — Cocos Creator Plugin

A drop-in plugin that connects to a phone-sensor WebSocket relay and converts accelerometer + step-counter data into game-ready **MotionIntent** values (forward movement + steering).

> **Requires:** Cocos Creator 3.x with TypeScript support.

---

## Installation

1. Copy the entire `controller-client/` folder into your Cocos Creator project's `assets/` directory:

```
your-cocos-project/
└── assets/
    └── controller-client/    ← copy here
        ├── Controller.ts
        ├── ControllerBridge.ts
        ├── MotionIntent.ts
        ├── MotionPacket.ts
        ├── MotionProcessor.ts
        ├── MotionSettings.ts
        ├── ActionState.ts
        └── index.ts
```

2. Open your Cocos Creator project — the scripts will be compiled automatically.

---

## Quick Start

### Option A: Use the ControllerBridge component (recommended)

1. Create or select a Node in your scene.
2. Add the **ControllerBridge** component via the Inspector.
3. Configure the WebSocket URL and tuning parameters in the Inspector.
4. From any other component, read the motion data or hook up callbacks:

```ts
import { _decorator, Component, Vec3 } from 'cc';
import { ControllerBridge } from './controller-client/ControllerBridge';
import { MotionIntent } from './controller-client/MotionIntent';

const { ccclass } = _decorator;

@ccclass('Player')
export class Player extends Component {
    private bridge: ControllerBridge = null!;

    onLoad() {
        // Find the bridge component in the scene
        this.bridge = this.node.scene.getComponentInChildren(ControllerBridge)!;

        // Connect callbacks
        this.bridge.onMotionUpdated = (intent) => this.onMotionUpdated(intent);
        this.bridge.onCommandReceived = (cmd) => this.onCommandReceived(cmd);
        this.bridge.onScreenshotReceived = (res) => this.onScreenshotReceived(res);
        this.bridge.onGpxExported = (res) => this.onGpxExported(res);
    }

    update(dt: number) {
        // Direct polling approach
        const move = this.bridge.lastMotion.move;   // 0..1  forward intent
        const turn = this.bridge.lastMotion.turn;   // -1..1 steering intent
    }

    private onMotionUpdated(intent: MotionIntent) {
        console.log(`Move: ${intent.move}, Turn: ${intent.turn}`);
    }

    private onCommandReceived(cmd: string) {
        console.log(`Command: ${cmd}`);
    }

    private onScreenshotReceived(result: { filePath: string, width: number, height: number }) {
        console.log(`Screenshot saved: ${result.filePath} (${result.width}x${result.height})`);
    }

    private onGpxExported(result: { filePath: string, distanceKm: number, duration: string, error?: string }) {
        if (result.error) {
            console.error(`GPX Export failed: ${result.error}`);
            return;
        }
        console.log(`GPX saved: ${result.filePath} (${result.distanceKm} km, ${result.duration})`);
    }
}
```

### Option B: Use the Controller class directly

```ts
import { Controller, MotionIntent } from './controller-client';

const ctrl = new Controller();

ctrl.onMotion                = (intent: MotionIntent) => console.log(`Move: ${intent.move}, Turn: ${intent.turn}`);
ctrl.onCommand               = (cmd: string) => console.log(`Command: ${cmd}`);
ctrl.onConnectionStateChanged = (connected: boolean) => console.log(`Connected: ${connected}`);

ctrl.connect('ws://localhost:8765/sensor');

// In your frame loop update(dt):
ctrl.dispatch();

// On shutdown:
ctrl.dispose();
```

---

## Checking Button State

```ts
const bridge = this.node.getComponent(ControllerBridge)!;

if (bridge.isActionPressed("jump"))   { /* handle jump   */ }
if (bridge.isActionPressed("fire"))   { /* handle fire   */ }
if (bridge.isActionPressed("crouch")) { /* handle crouch */ }
```

Button names correspond to the `command` field in button configuration.

---

## Callbacks / Events

Exposed events on the `ControllerBridge` component:

| Callback / Event | Parameters | Description |
|-------------------|------------|-------------|
| `onMotionUpdated` | `intent: MotionIntent` | Triggered each frame with the latest motion intent |
| `onCommandReceived` | `command: string` | Triggered when a command string arrives |
| `onScreenshotReceived` | `result: ScreenshotResult` | Triggered when a screenshot is saved |
| `onGpxExported` | `result: GpxExportResult` | Triggered when a GPX trail is exported |

---

## Inspector Properties (Component Properties)

| Parameter | Default | Description |
|-----------|---------|-------------|
| `url` | `ws://localhost:8765/sensor` | WebSocket URL of the sensor relay server |
| `maxTilt` | `0.6` | Maximum tilt angle (normalized). Beyond this threshold → clamped to ±1 |
| `deadZone` | `0.05` | Tilt below this threshold is ignored (eliminates idle drift) |
| `steeringSmoothing` | `0.12` | Smoothing factor for steering (0 = none, 1 = instant snap) |
| `turnSpeedDeg` | `120` | Turn speed in degrees/second, used by your game logic |
| `stepImpulse` | `0.4` | Forward velocity added per detected step |
| `maxMove` | `1.5` | Forward velocity ceiling |
| `moveDamping` | `2.0` | Exponential damping applied to forward velocity each frame |

> **Runtime Tuning:** All component properties can be modified from the Inspector during Play Mode or via script at runtime. `ControllerBridge` calls `syncSettings()` every frame, so changes propagate to the underlying `MotionSettings` instance automatically.

---

## Public Properties

| Property | Type | Description |
|----------|------|-------------|
| `controller` | `Controller` | The underlying engine-agnostic controller instance |
| `settings` | `MotionSettings` | The active motion settings instance (synced from Inspector properties)|
| `lastMotion` | `MotionIntent` | Last received motion intent — read in `update` loop |
| `connected` | `boolean` | Whether the WebSocket is currently connected |

---

## Screenshot Capture

```ts
const bridge = this.node.getComponent(ControllerBridge)!;

// Connect callback to handle result
bridge.onScreenshotReceived = (result) => {
    console.log(`Screenshot saved to: ${result.filePath} (${result.width}x${result.height})`);
};

// Capture full monitor
bridge.captureScreen();

// Capture active window only
bridge.captureScreen(true);

// Capture a specific window by title
bridge.captureScreen("My Cocos Game");
```

---

## GPX Recording

GPX recording has two modes:

- **Simulated mode** — the server generates a route automatically.
- **Manual mode** — you push the player's world position each frame, converted to real-world lat/lon.

### Overloads

```ts
bridge.startGpx();                                        // Simulated, server default origin
bridge.startGpx(manualLocation = true);                   // Manual, server default origin
bridge.startGpx(lat, lon);                                // Simulated, custom origin
bridge.startGpx(lat, lon, manualLocation = true);         // Manual, custom origin

bridge.updateGpxLocation(lat, lon);                       // Push current position (manual mode only)
bridge.exportGpx();                                       // Export trail to PC server disk
```

### Manual mode example

Convert the player's in-game `worldPosition` to real-world coordinates and call `updateGpxLocation` every frame:

```ts
private const originLat       = 3.2206334;
private const originLon       = 101.9676587;
private const metersPerDegLat = 111320.0;

// Call once to begin recording
bridge.startGpx(originLat, originLon, true);

// Call in update loop
const gps = this.worldToLatLon(this.node.worldPosition);
bridge.updateGpxLocation(gps.lat, gps.lon);

// Convert Cocos world position → real-world lat/lon
private worldToLatLon(worldPos: Vec3)
{
    const deltaLat = worldPos.z / metersPerDegLat;
    const deltaLon = worldPos.x / (metersPerDegLat * Math.cos(originLat * Math.PI / 180.0));
    return { lat: originLat + deltaLat, lon: originLon + deltaLon };
}
```

The player's **Z axis** maps to latitude (north/south) and **X axis** maps to longitude (east/west). To scale the world (e.g. 1 unit = 10 real metres), divide `worldPos` components by a scale factor before converting.

### Listening for export result

```ts
bridge.onGpxExported = (result) => {
    if (result.error) {
        console.error(`GPX Export failed: ${result.error}`);
        return;
    }
    console.log(`GPX saved: ${result.filePath} (${result.distanceKm} km, ${result.duration})`);
};

bridge.exportGpx();
```

---

## Architecture

```
Phone Sensors → WebSocket Server → Controller.ts → MotionProcessor.ts → MotionIntent
                                        ↓
                               ControllerBridge.ts (Cocos Component)
                                        ↓
                               Your Game Components
```

The core files (`Controller.ts`, `MotionProcessor.ts`, etc.) are **engine-agnostic** and use only standard browser APIs (`WebSocket`, `performance.now()`). Only `ControllerBridge.ts` imports from `'cc'`.

### Lifecycle

| Cocos callback | What ControllerBridge does |
|----------------|----------------------------|
| `onLoad()` | Creates `MotionSettings`, creates `Controller`, connects to server |
| `update()` | Calls `syncSettings()` then `controller.dispatch()` every frame |
| `onDestroy()` | Calls `controller.dispose()` and nulls reference |

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

The special command values `controller_connected` and `controller_disconnected` trigger the connection state callback.

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| No motion data received | Check the WebSocket URL and verify the sensor relay server is running |
| Connection keeps reconnecting | The controller auto-reconnects every 2 seconds — verify the server address/port |
| GPX positions are wildly off | Ensure `startGpx` is called with `manualLocation = true` and the correct origin |
| Screenshot saves but is blank | Use `captureScreen("Window Title")` to target the correct window |
