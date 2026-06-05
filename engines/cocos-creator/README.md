# ControllerClient — Cocos Creator Plugin

A drop-in plugin that connects to a phone-sensor WebSocket relay and converts accelerometer + step-counter data into game-ready **MotionIntent** values (forward movement + steering).

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

## Quick Start

### Option A: Use the ControllerBridge component (recommended)

1. Create or select a Node in your scene.
2. Add the **ControllerBridge** component via the Inspector.
3. Configure the WebSocket URL and tuning parameters in the Inspector.
4. From any other component, read the motion data:

```ts
import { ControllerBridge } from './controller-client';

// In your player component:
update(dt: number) {
    const bridge = this.node.getComponent(ControllerBridge)!;

    const move = bridge.lastMotion.move;   // 0..1  forward intent
    const turn = bridge.lastMotion.turn;   // -1..1 steering intent

    // Use move/turn to drive your character...
}
```

### Option B: Use the Controller class directly

```ts
import { Controller, MotionIntent } from './controller-client';

const ctrl = new Controller();

ctrl.onMotion = (intent: MotionIntent) => {
    console.log(`Move: ${intent.move}, Turn: ${intent.turn}`);
};

ctrl.onCommand = (cmd: string) => {
    console.log(`Command: ${cmd}`);
};

ctrl.onConnectionStateChanged = (connected: boolean) => {
    console.log(`Connected: ${connected}`);
};

ctrl.connect('ws://localhost:8765/sensor');

// In your frame loop:
ctrl.dispatch();

// On shutdown:
ctrl.dispose();
```

## Checking Button State

```ts
const bridge = this.node.getComponent(ControllerBridge)!;

if (bridge.isActionPressed('jump')) {
    // Handle jump
}
```

## Tuning Parameters

| Parameter            | Default | Description                                     |
|----------------------|---------|-------------------------------------------------|
| `maxTilt`            | 0.6     | Maximum tilt angle (normalized). Beyond → ±1.   |
| `deadZone`           | 0.05    | Tilt below this threshold is ignored.           |
| `steeringSmoothing`  | 0.12    | Smoothing factor (0 = none, 1 = instant snap).  |
| `turnSpeedDeg`       | 120     | Turn speed in deg/s (used by your game logic).  |
| `stepImpulse`        | 0.4     | Velocity added per detected step.               |
| `maxMove`            | 1.5     | Forward velocity ceiling.                       |
| `moveDamping`        | 2.0     | Exponential damping applied each frame.         |

## Architecture

```
Phone Sensors → WebSocket Server → Controller.ts → MotionProcessor.ts → MotionIntent
                                       ↓
                              ControllerBridge.ts (Cocos Component)
                                       ↓
                              Your Game Components
```

The core files (`Controller.ts`, `MotionProcessor.ts`, etc.) are **engine-agnostic** and use only standard browser APIs (`WebSocket`, `performance.now()`). Only `ControllerBridge.ts` imports from `'cc'`.

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

Special command values `controller_connected` / `controller_disconnected` trigger the `onConnectionStateChanged` callback.
