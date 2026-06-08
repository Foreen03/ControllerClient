/**
 * JSON packet types received from the controller WebSocket server.
 * These mirror the C# Packet / MovementPacket / CommandPacket classes.
 */

/** Discriminator envelope — every packet has a `type` field. */
export interface TypedPacket {
    type: string;
}

/** Movement sensor data packet. */
export interface MovementPacket extends TypedPacket {
    type: 'movement';
    x: number;
    y: number;
    z: number;
    steps: number;
    stepsCadence: number;
    timestamp: number;
    buttons?: Record<string, boolean>;
}

/** Command / status packet. */
export interface CommandPacket extends TypedPacket {
    type: 'command';
    value: string;
    timestamp: number;
}

/** Union of all known packet types. */
export type Packet = MovementPacket | CommandPacket | ScreenshotPacket | GpxStartedPacket | GpxExportedPacket;

export interface ScreenshotPacket extends TypedPacket {
    type: 'screenshot';
    path: string;
    width: number;
    height: number;
}

export interface GpxStartedPacket extends TypedPacket {
    type: 'gpxStarted';
    mode: string;
    error?: string;
}

export interface GpxExportedPacket extends TypedPacket {
    type: 'gpxExported';
    path: string;
    distance: number;
    duration: string;
    error?: string;
}

