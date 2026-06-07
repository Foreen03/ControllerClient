using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControllerClient
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(MovementPacket), "movement")]
    [JsonDerivedType(typeof(CommandPacket), "command")]
    [JsonDerivedType(typeof(ScreenshotPacket), "screenshot")]
    [JsonDerivedType(typeof(GpxStartedPacket), "gpxStarted")]
    [JsonDerivedType(typeof(GpxExportedPacket), "gpxExported")]
    public class Packet
    {
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }

    public sealed class MovementPacket : Packet
    {
        [JsonPropertyName("x")]
        public float X { get; set; }

        [JsonPropertyName("y")]
        public float Y { get; set; }

        [JsonPropertyName("z")]
        public float Z { get; set; }

        [JsonPropertyName("steps")]
        public int Steps { get; set; }

        [JsonPropertyName("stepsCadence")]
        public float StepsCadence { get; set; }

        [JsonPropertyName("buttons")]
        public Dictionary<string, bool> Buttons { get; set; }
    }

    public sealed class CommandPacket : Packet
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    public sealed class ScreenshotPacket : Packet
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }
    }

    public sealed class GpxStartedPacket : Packet
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }

    public sealed class GpxExportedPacket : Packet
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("duration")]
        public string Duration { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }
}
