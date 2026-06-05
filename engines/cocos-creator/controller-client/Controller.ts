import { MotionIntent, MOTION_INTENT_ZERO } from './MotionIntent';
import { MotionSettings } from './MotionSettings';
import { MotionProcessor } from './MotionProcessor';
import { ActionState } from './ActionState';
import { MovementPacket, CommandPacket, TypedPacket } from './MotionPacket';

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
}
