import { _decorator, Component } from 'cc';
import { Controller } from './Controller';
import { MotionIntent, ScreenshotResult, GpxExportResult } from './MotionIntent';
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

    /** The active motion settings instance. */
    public settings!: MotionSettings;

    /** Last received motion intent (read in `update()` of other components). */
    public lastMotion: MotionIntent = { move: 0, turn: 0, timestamp: 0, rawSteps: 0, stepsCadence: 0 };

    /** Whether the controller WebSocket is currently connected. */
    public connected: boolean = false;

    /** Fired when the motion intent is updated. */
    public onMotionUpdated: ((intent: MotionIntent) => void) | null = null;
    /** Fired when a command is received. */
    public onCommandReceived: ((command: string) => void) | null = null;
    /** Fired when the WebSocket connection state changes. */
    public onConnectionStateChanged: ((connected: boolean) => void) | null = null;
    /** Fired when a screenshot is saved. */
    public onScreenshotReceived: ((result: ScreenshotResult) => void) | null = null;
    /** Fired when a GPX trail is exported. */
    public onGpxExported: ((result: GpxExportResult) => void) | null = null;

    // ── Lifecycle ──

    onLoad(): void {
        this.settings = new MotionSettings();
        this.syncSettings();

        this.controller = new Controller(this.settings);

        this.controller.onMotion = (intent: MotionIntent) => {
            this.lastMotion = intent;
            this.onMotionUpdated?.(intent);
        };

        this.controller.onConnectionStateChanged = (isConnected: boolean) => {
            this.connected = isConnected;
            console.log(`[ControllerBridge] Connection: ${isConnected ? 'OPEN' : 'CLOSED'}`);
            this.onConnectionStateChanged?.(isConnected);
        };

        this.controller.onCommand = (command: string) => {
            this.onCommandReceived?.(command);
        };

        this.controller.onScreenshot = (result: ScreenshotResult) => {
            this.onScreenshotReceived?.(result);
        };

        this.controller.onGpxExported = (result: GpxExportResult) => {
            this.onGpxExported?.(result);
        };

        this.controller.connect(this.url);
    }

    update(_dt: number): void {
        this.syncSettings();
        this.controller?.dispatch();
    }

    /**
     * Synchronizes the Inspector properties to the underlying MotionSettings instance.
     * This allows changing parameters dynamically at runtime (e.g. from other scripts or the Inspector).
     */
    syncSettings(): void {
        if (this.settings) {
            this.settings.maxTilt = this.maxTilt;
            this.settings.deadZone = this.deadZone;
            this.settings.steeringSmoothing = this.steeringSmoothing;
            this.settings.turnSpeedDeg = this.turnSpeedDeg;
            this.settings.stepImpulse = this.stepImpulse;
            this.settings.maxMove = this.maxMove;
            this.settings.moveDamping = this.moveDamping;
        }
    }

    onDestroy(): void {
        this.controller?.dispose();
    }

    // ── Convenience API ──

    /** Check whether a named action button is currently pressed. */
    isActionPressed(action: string): boolean {
        return this.controller?.actions.get(action) ?? false;
    }

    /** Request a full-monitor screenshot from the PC server. */
    captureScreen(): void;
    /** Request a window-only screenshot from the PC server (active window if true). */
    captureScreen(windowMode: boolean): void;
    /** Request a screenshot of a specific window by its title. */
    captureScreen(windowTitle: string): void;
    captureScreen(arg?: boolean | string): void {
        this.controller?.captureScreen(arg as any);
    }

    /** Request a vibration on the controller. */
    vibrate(durationMs: number = 200): void {
        this.controller?.vibrate(durationMs);
    }

    /** Start GPX recording with simulated route at server default origin. */
    startGpx(): void;
    /** Start GPX recording at server default origin. If manualLocation is true, character positions must be updated via UpdateGpxLocation. */
    startGpx(manualLocation: boolean): void;
    /** Start GPX recording with simulated route at specified origin. */
    startGpx(lat: number, lon: number): void;
    /** Start GPX recording at specified origin. If manualLocation is true, character positions must be updated via UpdateGpxLocation. */
    startGpx(lat: number, lon: number, manualLocation: boolean): void;
    startGpx(arg1?: boolean | number, arg2?: number, arg3?: boolean): void {
        this.controller?.startGpx(arg1 as any, arg2 as any, arg3 as any);
    }

    /** Updates the current GPX location in manual route mode. Automatically throttled. */
    updateGpxLocation(lat: number, lon: number): void {
        this.controller?.updateGpxLocation(lat, lon);
    }

    /** Export the recorded GPX trail to the PC server. */
    exportGpx(): void {
        this.controller?.exportGpx();
    }
}
