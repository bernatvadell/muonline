using System.Text.Json.Serialization;

namespace Client.Main.Models
{
    public class GateInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("Flag")]
        public int Flag { get; set; }
        [JsonPropertyName("Map")]
        public int Map { get; set; }
        [JsonPropertyName("X1")]
        public int X1 { get; set; }
        [JsonPropertyName("Y1")]
        public int Y1 { get; set; }
        [JsonPropertyName("X2")]
        public int X2 { get; set; }
        [JsonPropertyName("Y2")]
        public int Y2 { get; set; }
        [JsonPropertyName("Target")]
        public int Target { get; set; }
        [JsonPropertyName("Dir")]
        public int Dir { get; set; }
        [JsonPropertyName("Level")]
        public int Level { get; set; }
    }
}
