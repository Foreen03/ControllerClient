import { MotionIntent, MOTION_INTENT_ZERO, ScreenshotResult, GpxExportResult } from './MotionIntent';
import { MotionSettings } from './MotionSettings';
import { MotionProcessor } from './MotionProcessor';
import { ActionState } from './ActionState';
import { MovementPacket, CommandPacket, TypedPacket, ScreenshotPacket, GpxStartedPacket, GpxExportedPacket } from './MotionPacket';

/**
 * Engine-agnostic controller client.
 *
 * Connects to a WebSocket sensor relay, deserializes packets, processes
 * motion data, and queues callbacks for main-thread dispatch.
 *
 * Usage:
 *   const ctrl = new Controller();
 *   ctrl.onMotion = (intent) => { ... };
 *   ctrl.connect();
 *   // In your frame loop:
 *   ctrl.dispatch();
 *   // On shutdown:
 *   ctrl.dispose();
 *
 * Direct port of the C# Controller class.
 */
export class Controller {
    private ws: WebSocket | null = null;
    private disposed: boolean = false;
    private reconnectTimer: number | null = null;

    private readonly mainThreadQueue: Array<() => void> = [];

    private readonly motion: MotionProcessor;
    public readonly actions: ActionState = new ActionState();

    private lastTimestamp: number = 0;
    private lastReceiveTime: number = 0;

    private lastIntent: MotionIntent = { ...MOTION_INTENT_ZERO };
    private lastMotionTime: number = 0;

    // ── Events (assign callbacks) ──
    /** Fired each time a new MotionIntent is produced. */
    onMotion: ((intent: MotionIntent) => void) | null = null;
    /** Fired when a command string is received. */
    onCommand: ((command: string) => void) | null = null;
    /** Fired when the connection state changes. */
    onConnectionStateChanged: ((connected: boolean) => void) | null = null;
    /** Fired when a screenshot is received. */
    onScreenshot: ((result: ScreenshotResult) => void) | null = null;
    /** Fired when a GPX export is completed. */
    onGpxExported: ((result: GpxExportResult) => void) | null = null;

    /** Whether the WebSocket is currently open. */
    get isConnected(): boolean {
        return this.ws !== null && this.ws.readyState === WebSocket.OPEN;
    }

    constructor(settings?: MotionSettings) {
        this.motion = new MotionProcessor(settings);
    }

    // ── Connection ──

    /**
     * Begin connecting (with auto-reconnect) to the sensor relay.
     */
    connect(url: string = 'ws://localhost:8765/sensor'): void {
        this.disposed = false;
        this._connectOnce(url);
    }

    private _connectOnce(url: string): void {
        if (this.disposed) return;

        this._closeSocket();

        const ws = new WebSocket(url);
        this.ws = ws;

        ws.onopen = () => {
            this._enqueue(() => this.onConnectionStateChanged?.(true));
        };

        ws.onmessage = (ev: MessageEvent) => {
            this.lastReceiveTime = performance.now();
            this._handleMessage(typeof ev.data === 'string' ? ev.data : '');
        };

        ws.onerror = () => {
            // error is followed by onclose — reconnect handled there
        };

        ws.onclose = () => {
            this._enqueue(() => this.onConnectionStateChanged?.(false));
            if (!this.disposed) {
                this.reconnectTimer = setTimeout(() => this._connectOnce(url), 2000) as unknown as number;
            }
        };
    }

    private _closeSocket(): void {
        if (this.reconnectTimer !== null) {
            clearTimeout(this.reconnectTimer);
            this.reconnectTimer = null;
        }
        if (this.ws) {
            // Remove handlers to avoid stale callbacks
            this.ws.onopen = null;
            this.ws.onmessage = null;
            this.ws.onerror = null;
            this.ws.onclose = null;
            try { this.ws.close(); } catch { /* ignore */ }
            this.ws = null;
        }
    }

    // ── Message handling ──

    private _handleMessage(json: string): void {
        try {
            const typed: TypedPacket = JSON.parse(json);

            switch (typed.type) {
                case 'movement': {
                    const pkt = typed as unknown as MovementPacket;
                    this._emitMotion(
                        pkt.x, pkt.y, pkt.z,
                        pkt.steps, pkt.stepsCadence,
                        pkt.timestamp,
                    );
                    if (pkt.buttons) {
                        this._enqueue(() => this.actions.update(pkt.buttons!, pkt.timestamp));
                    }
                    break;
                }
                case 'command': {
                    const pkt = typed as unknown as CommandPacket;
                    if (pkt.value === 'controller_disconnected') {
                        this._enqueue(() => this.onConnectionStateChanged?.(false));
                    } else if (pkt.value === 'controller_connected') {
                        this._enqueue(() => this.onConnectionStateChanged?.(true));
                    } else {
                        this._enqueue(() => this.onCommand?.(pkt.value));
                    }
                    break;
                }
                case 'screenshot': {
                    const pkt = typed as unknown as ScreenshotPacket;
                    if (pkt) {
                        const result: ScreenshotResult = {
                            filePath: pkt.path,
                            width: pkt.width,
                            height: pkt.height
                        };
                        this._enqueue(() => this.onScreenshot?.(result));
                    }
                    break;
                }
                case 'gpxStarted': {
                    const pkt = typed as unknown as GpxStartedPacket;
                    console.log(`[Controller] GPX started: mode=${pkt?.mode}, error=${pkt?.error}`);
                    break;
                }
                case 'gpxExported': {
                    const pkt = typed as unknown as GpxExportedPacket;
                    if (pkt) {
                        const result: GpxExportResult = {
                            filePath: pkt.path,
                            distanceKm: pkt.distance,
                            duration: pkt.duration,
                            error: pkt.error
                        };
                        this._enqueue(() => this.onGpxExported?.(result));
                    }
                    break;
                }
            }
        } catch (e) {
            console.warn('[ControllerClient] Invalid message:', e);
        }
    }

    private _emitMotion(
        ax: number, ay: number, az: number,
        steps: number, stepsCadence: number,
        ts: number,
    ): void {
        if (this.lastTimestamp === 0) {
            this.lastTimestamp = ts;
            return;
        }

        let dt: number;
        if (ts <= this.lastTimestamp) {
            dt = 1 / 60;
        } else {
            dt = (ts - this.lastTimestamp) / 1000;
        }
        this.lastTimestamp = ts;

        const intent = this.motion.update(ax, ay, az, steps, stepsCadence, dt, ts);
        this.lastIntent = intent;
        this.lastMotionTime = performance.now();
        this._enqueue(() => this.onMotion?.(intent));
    }

    // ── Main-thread dispatch ──

    private _enqueue(action: () => void): void {
        this.mainThreadQueue.push(action);
    }

    /**
     * Flush the callback queue on the calling thread.
     * **Must be called every frame** from your engine's update loop.
     *
     * Also emits a zeroed MotionIntent if no data has arrived recently (250 ms),
     * matching the C# Dispatch() behavior.
     */
    dispatch(): void {
        const now = performance.now();
        const elapsedMs = now - this.lastMotionTime;

        if (elapsedMs > 250) {
            this._enqueue(() => this.onMotion?.({ ...MOTION_INTENT_ZERO }));
            this.lastMotionTime = now;
        }

        this.actions.releaseStaleButtons(Date.now());

        while (this.mainThreadQueue.length > 0) {
            const action = this.mainThreadQueue.shift()!;
            action();
        }
    }

    // ── Cleanup ──
 
    /**
     * Disconnect and release all resources.
     */
    dispose(): void {
        this.disposed = true;
        this._closeSocket();
        this.mainThreadQueue.length = 0;
    }

    // ── Private Send Helper ──
    private _send(json: string): void {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            try {
                this.ws.send(json);
            } catch (e) {
                console.error('[ControllerClient] send error:', e);
            }
        }
    }

    // ── Screenshot APIs ──

    captureScreen(): void;
    captureScreen(windowMode: boolean): void;
    captureScreen(windowTitle: string): void;
    captureScreen(arg?: boolean | string): void {
        let mode = 'monitor';
        let title: string | undefined;

        if (typeof arg === 'boolean') {
            mode = arg ? 'window' : 'monitor';
        } else if (typeof arg === 'string') {
            mode = 'window';
            title = arg;
        }

        const json = JSON.stringify({
            packetType: 'captureScreen',
            timeStamp: Date.now(),
            payload: { mode, title }
        });
        this._send(json);
    }

    // ── GPX APIs ──

    private lastGpxLocationSendTime: number = 0;

    startGpx(): void;
    startGpx(manualLocation: boolean): void;
    startGpx(lat: number, lon: number): void;
    startGpx(lat: number, lon: number, manualLocation: boolean): void;
    startGpx(arg1?: boolean | number, arg2?: number, arg3?: boolean): void {
        let lat: number | undefined;
        let lon: number | undefined;
        let manualLocation: boolean | undefined;

        if (typeof arg1 === 'boolean') {
            manualLocation = arg1;
        } else if (typeof arg1 === 'number' && typeof arg2 === 'number') {
            lat = arg1;
            lon = arg2;
            manualLocation = arg3;
        }

        const json = JSON.stringify({
            packetType: 'gpxStart',
            timeStamp: Date.now(),
            payload: { lat, lon, manualLocation }
        });
        this._send(json);
    }

    updateGpxLocation(lat: number, lon: number): void {
        const now = Date.now();
        if (this.lastGpxLocationSendTime > 0 && (now - this.lastGpxLocationSendTime) < 500) {
            return;
        }
        this.lastGpxLocationSendTime = now;

        const json = JSON.stringify({
            packetType: 'gpxUpdateLocation',
            timeStamp: now,
            payload: { lat, lon }
        });
        this._send(json);
    }

    exportGpx(): void {
        const json = JSON.stringify({
            packetType: 'gpxExport',
            timeStamp: Date.now(),
            payload: {}
        });
        this._send(json);
    }
}
