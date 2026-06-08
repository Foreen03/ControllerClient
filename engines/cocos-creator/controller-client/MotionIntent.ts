/**
 * Represents the processed motion intent derived from raw sensor data.
 * Move and Turn are normalized values ready to drive character movement.
 */
export interface MotionIntent {
    /** Forward intent, 0..1 */
    move: number;
    /** Steering intent, -1..1 (negative = left, positive = right) */
    turn: number;
    /** Original timestamp from the sensor packet (ms) */
    timestamp: number;
    /** Raw cumulative step count from the sensor */
    rawSteps: number;
    /** Steps per second cadence from the sensor */
    stepsCadence: number;
}

/** A zeroed-out MotionIntent, equivalent to C# `default(MotionIntent)`. */
export const MOTION_INTENT_ZERO: Readonly<MotionIntent> = Object.freeze({
    move: 0,
    turn: 0,
    timestamp: 0,
    rawSteps: 0,
    stepsCadence: 0,
});

export interface ScreenshotResult {
    filePath: string;
    width: number;
    height: number;
}

export interface GpxExportResult {
    filePath: string;
    distanceKm: number;
    duration: string;
    error?: string;
}

