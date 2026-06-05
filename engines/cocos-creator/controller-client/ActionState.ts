/**
 * Tracks the press/release state of named action buttons.
 * Automatically releases buttons that haven't been refreshed within a timeout.
 *
 * Direct port of the C# ActionState class.
 */
export class ActionState {
    private readonly states: Map<string, boolean> = new Map();
    private readonly lastUpdate: Map<string, number> = new Map();

    /**
     * Update button states from a snapshot received in a movement packet.
     * @param snapshot  - Map of button-name → pressed
     * @param timestamp - Packet timestamp (ms)
     */
    update(snapshot: Record<string, boolean>, timestamp: number): void {
        for (const key in snapshot) {
            this.states.set(key, snapshot[key]);
            this.lastUpdate.set(key, timestamp);
        }
    }

    /**
     * Query whether a named action button is currently pressed.
     */
    get(action: string): boolean {
        return this.states.get(action) ?? false;
    }

    /**
     * Release any buttons whose last update is older than `timeoutMs`.
     * Should be called each frame from the engine's update loop.
     * @param currentTimestamp - Current time in ms
     * @param timeoutMs        - Staleness threshold (default 150ms, matches C#)
     */
    releaseStaleButtons(currentTimestamp: number, timeoutMs: number = 150): void {
        for (const [key] of this.states) {
            const ts = this.lastUpdate.get(key);
            if (ts !== undefined && currentTimestamp - ts > timeoutMs) {
                this.states.set(key, false);
            }
        }
    }
}
