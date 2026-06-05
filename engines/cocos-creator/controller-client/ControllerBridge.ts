import { _decorator, Component } from 'cc';
import { Controller } from './Controller';
import { MotionIntent } from './MotionIntent';
import { MotionSettings } from './MotionSettings';

const { ccclass, property } = _decorator;

/**
 * Cocos Creator Component that wraps the engine-agnostic Controller.
 *
 * Drop this component onto any Node in your scene. It will:
 *   1. Connect to the sensor WebSocket on `onLoad()`
 *   2. Dispatch queued callbacks every frame in `update()`
 *   3. Clean up on `onDestroy()`
 *
 * Access the latest motion/command data via the public properties
 * or by assigning callbacks before `onLoad()`.
 *
 * Example (from another component):
 *   ```ts
 *   const bridge = this.node.getComponent(ControllerBridge)!;
 *   bridge.controller.onMotion = (intent) => {
 *       this.node.setPosition(intent.move, 0, 0);
 *   };
 *   ```
 */
@ccclass('ControllerBridge')
export class ControllerBridge extends Component {

    // ── Inspector Properties ──

    @property({ tooltip: 'WebSocket URL of the sensor relay server' })
    url: string = 'ws://localhost:8765/sensor';

    @property({ tooltip: 'Maximum tilt angle (normalized)' })
    maxTilt: number = 0.6;

    @property({ tooltip: 'Dead-zone threshold' })
    deadZone: number = 0.05;

    @property({ tooltip: 'Steering smoothing factor (0–1)' })
    steeringSmoothing: number = 0.12;

    @property({ tooltip: 'Turn speed in degrees per second' })
    turnSpeedDeg: number = 120;

    @property({ tooltip: 'Velocity impulse per step' })
    stepImpulse: number = 0.4;

    @property({ tooltip: 'Maximum forward velocity' })
    maxMove: number = 1.5;

    @property({ tooltip: 'Forward velocity damping factor' })
    moveDamping: number = 2.0;

    // ── Public State ──

    /** The underlying engine-agnostic controller instance. */
    public controller!: Controller;

    /** Last received motion intent (read in `update()` of other components). */
    public lastMotion: MotionIntent = { move: 0, turn: 0, timestamp: 0, rawSteps: 0, stepsCadence: 0 };

    /** Whether the controller WebSocket is currently connected. */
    public connected: boolean = false;

    // ── Lifecycle ──

    onLoad(): void {
        const settings = new MotionSettings();
        settings.maxTilt = this.maxTilt;
        settings.deadZone = this.deadZone;
        settings.steeringSmoothing = this.steeringSmoothing;
        settings.turnSpeedDeg = this.turnSpeedDeg;
        settings.stepImpulse = this.stepImpulse;
        settings.maxMove = this.maxMove;
        settings.moveDamping = this.moveDamping;

        this.controller = new Controller(settings);

        this.controller.onMotion = (intent: MotionIntent) => {
            this.lastMotion = intent;
        };

        this.controller.onConnectionStateChanged = (isConnected: boolean) => {
            this.connected = isConnected;
            console.log(`[ControllerBridge] Connection: ${isConnected ? 'OPEN' : 'CLOSED'}`);
        };

        this.controller.connect(this.url);
    }

    update(_dt: number): void {
        this.controller?.dispatch();
    }

    onDestroy(): void {
        this.controller?.dispose();
    }

    // ── Convenience API ──

    /** Check whether a named action button is currently pressed. */
    isActionPressed(action: string): boolean {
        return this.controller?.actions.get(action) ?? false;
    }
}
