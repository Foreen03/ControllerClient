/**
 * Tuning parameters for the motion processor.
 * All defaults match the C# MotionSettings class.
 */
export class MotionSettings {
    // ── Steering ──
    /** Maximum tilt angle (normalized). Tilts beyond this map to ±1. */
    maxTilt: number = 0.6;
    /** Dead-zone threshold below which tilt is ignored. */
    deadZone: number = 0.05;
    /** Smoothing factor for steering (0 = no smoothing, 1 = instant). */
    steeringSmoothing: number = 0.12;
    /** Turn speed in degrees per second (informational, used by engine layer). */
    turnSpeedDeg: number = 120;

    // ── Movement ──
    /** Velocity impulse added per detected step. */
    stepImpulse: number = 0.4;
    /** Maximum forward velocity (clamped). */
    maxMove: number = 1.5;
    /** Exponential damping factor applied to forward velocity each frame. */
    moveDamping: number = 2.0;
}
