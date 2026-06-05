using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ControllerClient
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(MovementPacket), "movement")]
    [JsonDerivedType(typeof(CommandPacket), "command")]
    [JsonDerivedType(typeof(ScreenshotPacket), "screenshot")]
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
}
