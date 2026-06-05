import { MotionIntent, MOTION_INTENT_ZERO } from './MotionIntent';
import { MotionSettings } from './MotionSettings';

/**
 * Core motion processing algorithm.
 * Converts raw accelerometer + step-counter data into a MotionIntent.
 *
 * This is a direct port of the C# MotionProcessor class.
 */
export class MotionProcessor {
    private readonly settings: MotionSettings;

    private smoothedTurn: number = 0;
    private moveVelocity: number = 0;
    private lastStepCount: number = 0;

    constructor(settings?: MotionSettings) {
        this.settings = settings ?? new MotionSettings();
    }

    /**
     * Process one frame of sensor data.
     * @param ax        - Accelerometer X (lateral tilt)
     * @param ay        - Accelerometer Y
     * @param az        - Accelerometer Z
     * @param stepCount - Cumulative step count from sensor
     * @param stepsCadence - Steps-per-second cadence
     * @param deltaTime - Time since last update in seconds
     * @param timestamp - Packet timestamp in milliseconds
     * @returns Processed MotionIntent
     */
    update(
        ax: number,
        ay: number,
        az: number,
        stepCount: number,
        stepsCadence: number,
        deltaTime: number,
        timestamp: number,
    ): MotionIntent {
        const magnitude = Math.sqrt(ax * ax + ay * ay + az * az);
        if (magnitude < 0.0001) {
            return { ...MOTION_INTENT_ZERO };
        }

        // ── Steering ──
        const rawSteering = ax / magnitude;
        let targetSteering = 0;

        const absSteering = Math.abs(rawSteering);
        if (absSteering > this.settings.deadZone) {
            targetSteering =
                ((absSteering - this.settings.deadZone) / (1.0 - this.settings.deadZone)) *
                Math.sign(rawSteering);
        }

        targetSteering = clamp(targetSteering / this.settings.maxTilt, -1, 1);

        this.smoothedTurn = lerp(
            this.smoothedTurn,
            targetSteering,
            this.settings.steeringSmoothing,
        );

        // ── Steps → Forward ──
        const stepDelta = stepCount - this.lastStepCount;
        this.lastStepCount = stepCount;

        if (stepDelta > 0) {
            this.moveVelocity += stepDelta * this.settings.stepImpulse;
        }

        this.moveVelocity = Math.min(this.moveVelocity, this.settings.maxMove);
        this.moveVelocity = damp(this.moveVelocity, this.settings.moveDamping, deltaTime);

        return {
            move: this.moveVelocity,
            turn: this.smoothedTurn,
            timestamp,
            rawSteps: stepCount,
            stepsCadence,
        };
    }
}

// ── Helpers (match the C# implementations exactly) ──

function damp(value: number, damping: number, dt: number): number {
    return value * Math.exp(-damping * dt);
}

function lerp(a: number, b: number, t: number): number {
    return a + (b - a) * t;
}

function clamp(value: number, min: number, max: number): number {
    return Math.max(min, Math.min(max, value));
}
